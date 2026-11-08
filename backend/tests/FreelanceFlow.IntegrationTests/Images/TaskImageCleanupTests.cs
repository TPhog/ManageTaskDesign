using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace FreelanceFlow.IntegrationTests.Images;

public class TaskImageCleanupTests(AppFactory factory) : ImageTestBase(factory)
{
    private string VariantPath(Guid projectId, Guid id, string variant) =>
        Path.Combine(Factory.FilesRoot, "projects", projectId.ToString(), id.ToString(), variant + ".webp");

    [Fact]
    public async Task Deleting_a_task_deletes_its_images_but_keeps_quotation_evidence()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án xoá task " + Guid.NewGuid());
        var taskId = await CreateTaskAsync(projectId, "Task có ảnh");
        var images = await UploadTaskImagesAsync(Client, taskId, TestImages.Png(50, 40), TestImages.Png(60, 40, new SKColor(10, 120, 60)));
        var plain = images[0].GetProperty("id").GetGuid();
        var plainSize = images[0].GetProperty("sizeBytes").GetInt64();
        var evidence = images[1].GetProperty("id").GetGuid();
        var quotationId = await CreateSentQuotationAsync(projectId);
        var rowVersion = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{quotationId}")).GetProperty("rowVersion").GetUInt32();
        (await Client.PostAsJsonAsync($"/api/admin/quotations/{quotationId}/accept", new { rowVersion, evidenceFileId = evidence }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var usedBefore = (await UsageAsync(Client)).GetProperty("usedBytes").GetInt64();

        (await Client.DeleteAsync($"/api/admin/tasks/{taskId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await UsageAsync(Client)).GetProperty("usedBytes").GetInt64().Should().Be(usedBefore - plainSize);
        foreach (var variant in new[] { "original", "preview", "thumb" })
        {
            File.Exists(VariantPath(projectId, plain, variant)).Should().BeFalse(variant);
            File.Exists(VariantPath(projectId, evidence, variant)).Should().BeTrue("BR-87 evidence stays: " + variant);
        }
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/storage/images?projectId={projectId}"))
            .GetProperty("total").GetInt32().Should().Be(1);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Files.AsNoTracking().SingleAsync(f => f.Id == plain)).DeletedAt.Should().NotBeNull();
        (await db.Files.AsNoTracking().SingleAsync(f => f.Id == evidence)).DeletedAt.Should().BeNull();
        (await db.ActivityLogs.AsNoTracking().CountAsync(a => a.EntityId == plain && a.Action == "image.deleted")).Should().Be(1);
        var deleted = await db.ActivityLogs.AsNoTracking().SingleAsync(a => a.EntityId == taskId && a.Action == "task.deleted");
        var data = JsonDocument.Parse(deleted.Data).RootElement;
        data.GetProperty("imagesDeleted").GetInt32().Should().Be(1);
        data.GetProperty("imagesKept").GetInt32().Should().Be(1);
    }
}
