using DotNet.Testcontainers.Builders;
using System.Collections.Concurrent;
using FreelanceFlow.Api.Infrastructure.Notifications;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace FreelanceFlow.IntegrationTests;

/// <summary>Test hook: runs once just before the next SaveChanges of any request (to simulate a concurrent commit).</summary>
public sealed class BeforeSaveHook : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
{
    private Func<Task>? _next;
    public void RunOnNextSave(Func<Task> action) => _next = action;

    public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
        Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var action = Interlocked.Exchange(ref _next, null);
        if (action != null) await action();
        return result;
    }
}

public sealed class RecordingNotificationSender : INotificationSender
{
    private readonly ConcurrentQueue<Notification> _sent = new();
    public IReadOnlyCollection<Notification> Sent => _sent.ToArray();

    public Task SendAsync(Notification n, CancellationToken ct)
    {
        _sent.Enqueue(n);
        return Task.CompletedTask;
    }

    /// <summary>Waits until at least one notification matching the predicate arrived (or 5s).</summary>
    public async Task<List<Notification>> WaitForAsync(Func<Notification, bool> match, int expectedAtLeast = 1)
    {
        for (var i = 0; i < 50; i++)
        {
            var hits = Sent.Where(match).ToList();
            if (hits.Count >= expectedAtLeast) return hits;
            await Task.Delay(100);
        }
        return Sent.Where(match).ToList();
    }
}

public sealed class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestUsername = "designer";
    public const string TestPassword = "testpassword1234";
    public const string TestDatabaseName = "freelanceflow_test";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase(TestDatabaseName)
        .WithUsername("ff")
        .WithPassword("changeme")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    /// <summary>Connection string of the Testcontainers database (valid after InitializeAsync).</summary>
    public string DatabaseConnectionString => _db.GetConnectionString();

    /// <summary>LocalFileStorage root of this factory (a fresh temp folder, deleted on dispose).</summary>
    public string FilesRoot { get; } = Path.Combine(Path.GetTempPath(), "ff-it-files", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting (host configuration) instead of ConfigureAppConfiguration: Program.cs reads the
        // connection string and RateLimit:LoginPerMinute before Build(), when later-added config
        // sources are not yet visible - that silently pointed the tests at the dev database.
        builder.UseEnvironment("Testing"); // skips the prod-only PublicBaseUrl startup check
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
        builder.UseSetting("Seed:DesignerUsername", TestUsername);
        builder.UseSetting("Seed:DesignerPassword", TestPassword);
        builder.UseSetting("App:PublicBaseUrl", "http://localhost");
        builder.UseSetting("RateLimit:LoginPerMinute", "1000");
        builder.UseSetting("RateLimit:PortalSessionPerMinute", "1000");
        builder.UseSetting("Notifications:Discord:WebhookUrl", ""); // never hit the network
        builder.UseSetting("Storage:ReconcileIntervalHours", "0"); // no background reconciliation in tests
        builder.UseSetting("Digest:CheckIntervalSeconds", "0"); // digest runs only when a test calls DailyDigestService

        // Real queue + dispatcher, but the sender only records
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<INotificationSender>();
            services.AddSingleton(Notifications);
            services.AddSingleton<INotificationSender>(Notifications);
            services.ConfigureDbContext<FreelanceFlow.Api.Data.AppDbContext>(opt => opt.AddInterceptors(BeforeSave));
            // never the real /data/files: each factory writes into its own temp folder
            services.PostConfigure<StorageOptions>(o => o.Local.RootPath = FilesRoot);
        });
    }

    /// <summary>Everything the notification dispatcher delivered (shared by all tests of the factory).</summary>
    public RecordingNotificationSender Notifications { get; } = new();

    public BeforeSaveHook BeforeSave { get; } = new();

    public async Task InitializeAsync() => await _db.StartAsync();

    public new async Task DisposeAsync()
    {
        await _db.StopAsync();
        await base.DisposeAsync();
        if (Directory.Exists(FilesRoot)) Directory.Delete(FilesRoot, recursive: true);
    }
}
