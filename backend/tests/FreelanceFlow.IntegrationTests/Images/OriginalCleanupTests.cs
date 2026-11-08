using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace FreelanceFlow.IntegrationTests.Images;

public class OriginalCleanupTests(AppFactory factory) : ImageTestBase(factory)
{
    private string VariantPath(Guid projectId, Guid id, string variant) =>
        Path.Combine(Factory.FilesRoot, "projects", projectId.ToString(), id.ToString(), variant + ".webp");

    /// <summary>COMPLETED at a chosen instant: status set directly + one backdated project.status_changed (the activity log is append-only).</summary>
    private async Task CompleteProjectAtAsync(Guid projectId, DateTimeOffset completedAt)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Projects.Where(p => p.Id == projectId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "COMPLETED"));
        var clientId = await db.Projects.Where(p => p.Id == projectId).Select(p => p.ClientId).SingleAsync();
        db.ActivityLogs.Add(new ActivityLog
        {
            OccurredAt = completedAt, ActorType = "DESIGNER", ClientId = clientId, ProjectId = projectId,
            EntityType = "project", EntityId = projectId, Action = "project.status_changed",
            Data = "{\"from\":\"IN_PROGRESS\",\"to\":\"COMPLETED\"}",
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Originals_of_long_completed_projects_are_previewed_then_deleted_on_confirmation_keeping_previews()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var old = await CreateProjectAsync(clientId, "Dự án cũ " + Guid.NewGuid());
        var recent = await CreateProjectAsync(clientId, "Dự án mới xong " + Guid.NewGuid());
        var active = await CreateProjectAsync(clientId, "Dự án đang làm " + Guid.NewGuid());
        var oldTask = await CreateTaskAsync(old);
        var oldImages = await UploadTaskImagesAsync(Client, oldTask, TestImages.Png(400, 300), TestImages.Png(300, 300, SKColors.Coral));
        var target = oldImages[0].GetProperty("id").GetGuid();
        var evidence = oldImages[1].GetProperty("id").GetGuid();
        await UploadTaskImagesAsync(Client, await CreateTaskAsync(recent), TestImages.Png(200, 100));
        await UploadTaskImagesAsync(Client, await CreateTaskAsync(active), TestImages.Png(200, 100, SKColors.Navy));
        var portal = await OpenPortalSessionAsync(token);
        var form = RequestForm("Ảnh tham khảo");
        form.Add(TestImages.Part(TestImages.Png(90, 90)), "files", "tham-khao.png");
        (await portal.PostAsync($"/api/portal/projects/{old}/requests", form)).StatusCode.Should().Be(HttpStatusCode.Created);
        var quotationId = await CreateSentQuotationAsync(old);
        var rowVersion = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{quotationId}")).GetProperty("rowVersion").GetUInt32();
        (await Client.PostAsJsonAsync($"/api/admin/quotations/{quotationId}/accept", new { rowVersion, evidenceFileId = evidence }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await CompleteProjectAtAsync(old, DateTimeOffset.UtcNow.AddMonths(-7));
        await CompleteProjectAtAsync(recent, DateTimeOffset.UtcNow.AddMonths(-1));
        long targetOriginal;
        using (var scope = Factory.Services.CreateScope())
            targetOriginal = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Files.AsNoTracking()
                .SingleAsync(f => f.Id == target)).OriginalBytes!.Value;

        var preview = await Client.GetFromJsonAsync<JsonElement>("/api/admin/storage/original-cleanup?olderThanMonths=6");
        preview.GetProperty("imageCount").GetInt32().Should().Be(1, "evidence (BR-87) and the client photo stored once are left out");
        preview.GetProperty("projectCount").GetInt32().Should().Be(1);
        preview.GetProperty("freedBytes").GetInt64().Should().Be(targetOriginal);
        var listed = preview.GetProperty("projects").EnumerateArray().Should().ContainSingle().Subject;
        listed.GetProperty("projectId").GetGuid().Should().Be(old);
        listed.GetProperty("imageCount").GetInt32().Should().Be(1);
        (await Client.GetFromJsonAsync<JsonElement>("/api/admin/storage/original-cleanup?olderThanMonths=24"))
            .GetProperty("imageCount").GetInt32().Should().Be(0);
        (await Client.GetAsync("/api/admin/storage/original-cleanup?olderThanMonths=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetAsync("/api/admin/storage/original-cleanup?olderThanMonths=121")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var stale = await Client.PostAsJsonAsync("/api/admin/storage/original-cleanup",
            new { olderThanMonths = 6, expectedImageCount = 2, expectedFreedBytes = targetOriginal });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
        var missing = await Client.PostAsJsonAsync("/api/admin/storage/original-cleanup", new { olderThanMonths = 6 });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("expectedImageCount", out _).Should().BeTrue();

        var usedBefore = (await UsageAsync(Client)).GetProperty("usedBytes").GetInt64();
        var run = await Client.PostAsJsonAsync("/api/admin/storage/original-cleanup",
            new { olderThanMonths = 6, expectedImageCount = 1, expectedFreedBytes = targetOriginal });
        run.StatusCode.Should().Be(HttpStatusCode.OK, await run.Content.ReadAsStringAsync());
        var result = await run.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("imageCount").GetInt32().Should().Be(1);
        result.GetProperty("projectCount").GetInt32().Should().Be(1);
        result.GetProperty("freedBytes").GetInt64().Should().Be(targetOriginal);

        (await UsageAsync(Client)).GetProperty("usedBytes").GetInt64().Should().Be(usedBefore - targetOriginal);
        File.Exists(VariantPath(old, target, "original")).Should().BeFalse();
        File.Exists(VariantPath(old, target, "preview")).Should().BeTrue();
        File.Exists(VariantPath(old, target, "thumb")).Should().BeTrue();
        File.Exists(VariantPath(old, evidence, "original")).Should().BeTrue();
        var served = await Client.GetAsync($"/api/admin/images/{target}/original");
        served.StatusCode.Should().Be(HttpStatusCode.OK);
        served.Headers.ETag!.Tag.Should().Be($"\"{target:N}-preview\"");
        (await served.Content.ReadAsByteArrayAsync()).Should().Equal(await File.ReadAllBytesAsync(VariantPath(old, target, "preview")));
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{oldTask}/images")).EnumerateArray()
            .Single(i => i.GetProperty("id").GetGuid() == target)
            .GetProperty("originalDeletedAt").ValueKind.Should().Be(JsonValueKind.String);
        (await Client.GetFromJsonAsync<JsonElement>("/api/admin/storage/original-cleanup?olderThanMonths=6"))
            .GetProperty("imageCount").GetInt32().Should().Be(0);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entry = await db.ActivityLogs.AsNoTracking().SingleAsync(a => a.ProjectId == old && a.Action == "image.originals_deleted");
            entry.EntityType.Should().Be("project");
            var data = JsonDocument.Parse(entry.Data).RootElement;
            data.GetProperty("imageCount").GetInt32().Should().Be(1);
            data.GetProperty("olderThanMonths").GetInt32().Should().Be(6);
            data.GetProperty("fileIds")[0].GetGuid().Should().Be(target);
        }
    }

    [Fact]
    public async Task Storage_usage_is_listed_per_project_with_completion_dates()
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var done = await CreateProjectAsync(clientId, "Dự án xong " + Guid.NewGuid());
        var active = await CreateProjectAsync(clientId, "Dự án làm " + Guid.NewGuid());
        var doneImages = await UploadTaskImagesAsync(Client, await CreateTaskAsync(done), TestImages.Png(300, 200), TestImages.Png(200, 200, SKColors.Plum));
        await UploadTaskImagesAsync(Client, await CreateTaskAsync(active), TestImages.Png(50, 50));
        var completedAt = DateTimeOffset.UtcNow.AddMonths(-2);
        await CompleteProjectAtAsync(done, completedAt);

        var rows = (await Client.GetFromJsonAsync<JsonElement>("/api/admin/storage/projects")).EnumerateArray().ToList();

        rows.Select(r => r.GetProperty("totalBytes").GetInt64()).Should().BeInDescendingOrder();
        var doneRow = rows.Single(r => r.GetProperty("projectId").GetGuid() == done);
        doneRow.GetProperty("projectStatus").GetString().Should().Be("COMPLETED");
        doneRow.GetProperty("imageCount").GetInt32().Should().Be(2);
        doneRow.GetProperty("totalBytes").GetInt64().Should().Be(doneImages.EnumerateArray().Sum(i => i.GetProperty("sizeBytes").GetInt64()));
        doneRow.GetProperty("originalsDeleted").GetInt32().Should().Be(0);
        doneRow.GetProperty("completedAt").GetDateTimeOffset().Should().BeCloseTo(completedAt, TimeSpan.FromSeconds(1));
        using (var scope = Factory.Services.CreateScope())
        {
            var ids = doneImages.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
            var originals = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Files.AsNoTracking()
                .Where(f => ids.Contains(f.Id)).SumAsync(f => f.OriginalBytes ?? 0);
            doneRow.GetProperty("originalBytes").GetInt64().Should().Be(originals);
        }
        rows.Single(r => r.GetProperty("projectId").GetGuid() == active).GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
