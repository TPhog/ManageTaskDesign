using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure.Notifications;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Images;

/// <summary>Own class = own database: the storage alerts of this scenario are not shared with other tests.</summary>
public class StorageQuotaTests(AppFactory factory) : ImageTestBase(factory)
{
    private const long Quota = 1_000_000;

    private async Task<List<int>> ActiveThresholdsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.StorageAlerts.AsNoTracking().Where(a => a.ClearedAt == null)
            .OrderBy(a => a.Threshold).Select(a => a.Threshold).ToListAsync();
    }

    private static async Task EvaluateOnAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();   // what the daily reconciliation does
        await scope.ServiceProvider.GetRequiredService<StorageAlertService>().EvaluateAsync();
    }

    [Fact]
    public async Task Thresholds_alert_once_block_uploads_at_90_percent_and_rearm_after_cleanup()
    {
        await using var host = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.PostConfigure<StorageOptions>(o => o.QuotaBytes = Quota)));
        var admin = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        await LoginOnAsync(admin);
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án hạn mức");
        var taskId = await CreateTaskAsync(projectId);
        async Task<long> Used() => (await UsageAsync(admin)).GetProperty("usedBytes").GetInt64();
        int Count(string title) => Factory.Notifications.Sent.Count(n => n.Title == title);

        await InsertFillerImageAsync(projectId, taskId, 705_000 - await Used());                   // 70.5 %
        await UploadTaskImagesAsync(admin, taskId, TestImages.Png(16, 16));                          // 50 + 70 raised
        var seventy = await Factory.Notifications.WaitForAsync(n => n.Title == "Dung lượng ảnh đã dùng 70%");
        seventy.Should().ContainSingle().Which.Severity.Should().Be(NotificationSeverity.Warning);
        (await ActiveThresholdsAsync()).Should().Equal(50, 70);
        await UploadTaskImagesAsync(admin, taskId, TestImages.Png(16, 16));                          // still < 85 %: nothing new

        var filler90 = await InsertFillerImageAsync(projectId, taskId, 910_000 - await Used());     // 91 %
        await EvaluateOnAsync(host);
        (await Factory.Notifications.WaitForAsync(n => n.Title == "Đã chặn tải ảnh mới — dung lượng 90%"))
            .Should().ContainSingle().Which.Severity.Should().Be(NotificationSeverity.High);
        Count("Dung lượng ảnh đã dùng 85%").Should().Be(1);
        Count("Dung lượng ảnh đã dùng 70%").Should().Be(1, "each threshold alerts once on the way up");

        var blocked = await admin.PostAsync($"/api/admin/tasks/{taskId}/images", TestImages.Form(TestImages.Png(16, 16)));
        blocked.StatusCode.Should().Be(HttpStatusCode.InsufficientStorage);
        (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("STORAGE_FULL");
        var usage = await UsageAsync(admin);
        usage.GetProperty("uploadsBlocked").GetBoolean().Should().BeTrue();
        usage.GetProperty("level").GetString().Should().Be("BLOCKED");
        usage.GetProperty("quotaBytes").GetInt64().Should().Be(Quota);

        var bulk = await admin.PostAsJsonAsync("/api/admin/images/bulk-delete", new { ids = new[] { filler90 } });
        bulk.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ActiveThresholdsAsync()).Should().Equal([50, 70], "85 and 90 clear below 83 % / 88 %");
        await UploadTaskImagesAsync(admin, taskId, TestImages.Png(16, 16));                          // allowed again

        await InsertFillerImageAsync(projectId, taskId, 860_000 - await Used());                     // 86 % again
        await EvaluateOnAsync(host);
        (await Factory.Notifications.WaitForAsync(n => n.Title == "Dung lượng ảnh đã dùng 85%", expectedAtLeast: 2))
            .Should().HaveCount(2, "a cleared threshold re-arms");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ActivityLogs.AsNoTracking().CountAsync(a => a.Action == "storage.threshold" && a.ActorType == "SYSTEM"))
            .Should().BeGreaterThanOrEqualTo(7, "raised 50, 70, 85, 90 · cleared 85, 90 · raised 85");
    }
}
