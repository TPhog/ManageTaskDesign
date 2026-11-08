using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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

    [Fact]
    public async Task An_upload_racing_a_task_delete_never_leaves_a_live_image_on_the_deleted_task()
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án đua xoá " + Guid.NewGuid());
        var taskId = await CreateTaskAsync(projectId, "Task bị xoá giữa chừng");

        // Another transaction (standing in for DELETE /tasks) holds the task row; the upload must wait for it
        await using var conn = new NpgsqlConnection(Factory.DatabaseConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await using (var lockTask = new NpgsqlCommand("SELECT id FROM tasks WHERE id = @id FOR UPDATE", conn, tx))
        {
            lockTask.Parameters.AddWithValue("id", taskId);
            await lockTask.ExecuteScalarAsync();
        }

        var upload = Client.PostAsync($"/api/admin/tasks/{taskId}/images", TestImages.Form(TestImages.Png(50, 50, SKColors.Tomato)));
        var waited = false;
        for (var i = 0; i < 100 && !waited && !upload.IsCompleted; i++)
        {
            await using var probe = new NpgsqlConnection(Factory.DatabaseConnectionString);
            await probe.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT count(*) FROM pg_locks WHERE NOT granted", probe);
            waited = (long)(await cmd.ExecuteScalarAsync())! > 0;
            if (!waited) await Task.Delay(100);
        }
        waited.Should().BeTrue("the upload transaction waits on the task row before committing its images");

        await using (var delete = new NpgsqlCommand("DELETE FROM tasks WHERE id = @id", conn, tx))
        {
            delete.Parameters.AddWithValue("id", taskId);
            await delete.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();

        (await upload).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Files.AsNoTracking()
            .CountAsync(f => f.OwnerId == taskId)).Should().Be(0);
        var projectFolder = Path.Combine(Factory.FilesRoot, "projects", projectId.ToString());
        (Directory.Exists(projectFolder) ? Directory.EnumerateFiles(projectFolder, "*", SearchOption.AllDirectories) : [])
            .Should().BeEmpty("the staged objects are discarded");
    }
}
