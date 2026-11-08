using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Images;

/// <summary>Own class = own database. I1: a re-armed threshold must be claimed by exactly one concurrent evaluation.</summary>
public class StorageAlertRaceTests(AppFactory factory) : ImageTestBase(factory)
{
    private const long Quota = 1_000_000;

    [Fact]
    public async Task Two_concurrent_evaluations_after_a_rearm_raise_each_threshold_once()
    {
        await using var host = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.PostConfigure<StorageOptions>(o => o.QuotaBytes = Quota)));
        var admin = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        await LoginOnAsync(admin);
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án race");
        var taskId = await CreateTaskAsync(projectId);
        async Task<long> Used() => (await UsageAsync(admin)).GetProperty("usedBytes").GetInt64();
        async Task EvaluateAsync()
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<StorageAlertService>().EvaluateAsync();
        }

        var filler = await InsertFillerImageAsync(projectId, taskId, 860_000 - await Used());
        await EvaluateAsync();                                                   // first insert path: 50, 70, 85 raised
        (await admin.PostAsJsonAsync("/api/admin/images/bulk-delete", new { ids = new[] { filler } })).EnsureSuccessStatusCode();
        await InsertFillerImageAsync(projectId, taskId, 860_000 - await Used());  // above 85 % again, not evaluated yet

        int Count(string title) => Factory.Notifications.Sent.Count(n => n.Title == title);
        async Task<int> ThresholdActivities()
        {
            using var scope = Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ActivityLogs.AsNoTracking()
                .CountAsync(a => a.Action == "storage.threshold");
        }
        var notified85 = Count("Dung lượng ảnh đã dùng 85%");
        var notified70 = Count("Dung lượng ảnh đã dùng 70%");
        var activities = await ThresholdActivities();

        var gate = new TaskCompletionSource();
        var runs = Enumerable.Range(0, 2).Select(_ => Task.Run(async () => { await gate.Task; await EvaluateAsync(); })).ToList();
        gate.SetResult();
        await Task.WhenAll(runs);

        (await Factory.Notifications.WaitForAsync(n => n.Title == "Dung lượng ảnh đã dùng 85%", expectedAtLeast: notified85 + 1))
            .Should().HaveCount(notified85 + 1);
        await Task.Delay(500);
        Count("Dung lượng ảnh đã dùng 85%").Should().Be(notified85 + 1, "only the evaluation that claimed the re-arm notifies");
        Count("Dung lượng ảnh đã dùng 70%").Should().Be(notified70 + 1);
        (await ThresholdActivities()).Should().Be(activities + 3, "50, 70 and 85 are each raised exactly once");
    }
}
