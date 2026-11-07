using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace FreelanceFlow.IntegrationTests;

public sealed class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestUsername = "designer";
    public const string TestPassword = "testpassword1234";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("freelanceflow_test")
        .WithUsername("ff")
        .WithPassword("changeme")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting (host configuration) instead of ConfigureAppConfiguration: Program.cs reads the
        // connection string and RateLimit:LoginPerMinute before Build(), when later-added config
        // sources are not yet visible - that silently pointed the tests at the dev database.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
        builder.UseSetting("Seed:DesignerUsername", TestUsername);
        builder.UseSetting("Seed:DesignerPassword", TestPassword);
        builder.UseSetting("App:PublicBaseUrl", "http://localhost");
        builder.UseSetting("RateLimit:LoginPerMinute", "1000");
    }

    public async Task InitializeAsync() => await _db.StartAsync();

    public new async Task DisposeAsync()
    {
        await _db.StopAsync();
        await base.DisposeAsync();
    }
}
