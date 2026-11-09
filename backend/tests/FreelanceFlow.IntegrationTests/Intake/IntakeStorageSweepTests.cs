using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Domain.Intake;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Intake;

/// <summary>Plan 13 × P10: intake photos live under "intake/" for life; the daily sweep knows that layout.</summary>
public class IntakeStorageSweepTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private string PathOf(string key) => Path.Combine(Factory.FilesRoot, key.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>An intake photo stored once (original + thumb) like ImageUploader writes a client photo.</summary>
    private async Task<ProjectFile> StoreIntakePhotoAsync(bool softDeleted = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var sub = IntakeSubmission.Create("Lan", "0909123456", null, null, null, [], "Cần thiết kế menu");
        var image = ProjectFile.CreateImage(null, ImageOwnerTypes.IntakeSubmission, sub.Id, null, "CLIENT", false, "menu.png",
            10, 10, 3, 0, 2, [1], DateTimeOffset.UtcNow, previewIsOriginal: true);
        foreach (var key in image.StoredVariantKeys()) await storage.PutAsync(key, [1, 2, 3], "image/webp");
        if (softDeleted) image.SoftDelete(DateTimeOffset.UtcNow);
        db.IntakeSubmissions.Add(sub);
        db.Files.Add(image);
        await db.SaveChangesAsync();
        return image;
    }

    private async Task<StorageSweepReport> SweepAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<StorageSweeper>().RunAsync();
    }

    [Fact]
    public async Task Sweep_keeps_live_intake_photos_purges_deleted_ones_and_removes_old_intake_orphans()
    {
        var live = await StoreIntakePhotoAsync();
        var deleted = await StoreIntakePhotoAsync(softDeleted: true);
        var orphan = $"intake/{Guid.NewGuid()}/{Guid.NewGuid()}/original.webp";
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(orphan))!);
        await File.WriteAllBytesAsync(PathOf(orphan), [9]);
        File.SetLastWriteTimeUtc(PathOf(orphan), DateTime.UtcNow.AddHours(-25));

        var report = await SweepAsync();

        report.MissingObjects.Should().Be(0, "live intake objects are listed, not reported missing");
        report.DeletedImagesPurged.Should().Be(2);
        report.OrphansDeleted.Should().Be(1);
        report.Errors.Should().Be(0);
        foreach (var key in live.StoredVariantKeys()) File.Exists(PathOf(key)).Should().BeTrue(key);
        foreach (var key in deleted.StoredVariantKeys()) File.Exists(PathOf(key)).Should().BeFalse(key);
        File.Exists(PathOf(orphan)).Should().BeFalse();
    }

    [Fact]
    public async Task Photo_joined_to_a_project_keeps_its_intake_key_and_its_objects()   // BR-136
    {
        await LoginAsync();
        var clientId = (await (await Client.PostAsJsonAsync("/api/admin/clients", new { name = "Sweep " + Guid.NewGuid() }))
            .Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var projectId = (await (await Client.PostAsJsonAsync("/api/admin/projects", new { clientId, name = "Menu", designType = "PRINT" }))
            .Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var image = await StoreIntakePhotoAsync();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Files.SingleAsync(f => f.Id == image.Id)).AttachToProject(projectId);
            await db.SaveChangesAsync();
        }

        var report = await SweepAsync();

        (report.MissingObjects, report.OrphansDeleted, report.StrayVariantsDeleted).Should().Be((0, 0, 0));
        foreach (var key in image.StoredVariantKeys()) File.Exists(PathOf(key)).Should().BeTrue(key);
    }
}
