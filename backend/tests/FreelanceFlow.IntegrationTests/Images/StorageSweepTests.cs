using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Features.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace FreelanceFlow.IntegrationTests.Images;

public class StorageSweepTests(AppFactory factory) : ImageTestBase(factory)
{
    private string PathOf(string key) => Path.Combine(Factory.FilesRoot, key.Replace('/', Path.DirectorySeparatorChar));

    private static string Key(Guid projectId, Guid fileId, string variant) => $"projects/{projectId}/{fileId}/{variant}.webp";

    private void WriteObject(string key, byte[] bytes, TimeSpan age)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
    }

    private async Task<StorageSweepReport> SweepAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<StorageSweeper>().RunAsync();
    }

    [Fact]
    public async Task Sweep_repairs_leftovers_orphans_stray_variants_and_interrupted_writes_but_spares_live_fresh_and_unknown_objects()
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án dọn kho " + Guid.NewGuid());
        var taskId = await CreateTaskAsync(projectId);
        var images = await UploadTaskImagesAsync(Client, taskId,
            TestImages.Png(60, 40), TestImages.Png(70, 50, new SKColor(200, 30, 30)),
            TestImages.Png(80, 60, new SKColor(30, 200, 30)), TestImages.Png(90, 70, new SKColor(30, 30, 200)));
        Guid Id(int i) => images[i].GetProperty("id").GetGuid();
        var (live, leftover, discarded, gappy) = (Id(0), Id(1), Id(2), Id(3));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // soft-deleted row whose object deletes failed / original discarded in the database whose delete failed
            await db.Files.Where(f => f.Id == leftover).ExecuteUpdateAsync(s => s.SetProperty(f => f.DeletedAt, DateTimeOffset.UtcNow));
            await db.Files.Where(f => f.Id == discarded).ExecuteUpdateAsync(s => s.SetProperty(f => f.OriginalDeletedAt, DateTimeOffset.UtcNow));
        }
        File.Delete(PathOf(Key(projectId, gappy, "thumb")));   // missing: reported, cannot be repaired
        var oldOrphan = Key(projectId, Guid.NewGuid(), "original");
        WriteObject(oldOrphan, [1, 2, 3], TimeSpan.FromHours(25));
        var freshOrphan = Key(projectId, Guid.NewGuid(), "original");
        WriteObject(freshOrphan, [4], TimeSpan.FromMinutes(5));
        var unknown = $"projects/{projectId}/ghi-chu.txt";
        WriteObject(unknown, [5], TimeSpan.FromDays(3));
        var oldTemp = PathOf(Key(projectId, live, "preview")) + ".tmp-0123456789abcdef0123456789abcdef";
        await File.WriteAllBytesAsync(oldTemp, [6]);
        File.SetLastWriteTimeUtc(oldTemp, DateTime.UtcNow.AddHours(-30));
        var usedBefore = (await UsageAsync(Client)).GetProperty("usedBytes").GetInt64();

        var report = await SweepAsync();

        report.DeletedImagesPurged.Should().Be(3);
        report.StrayVariantsDeleted.Should().Be(1);
        report.OrphansDeleted.Should().Be(1);
        report.IncompleteWritesDeleted.Should().Be(1);
        report.MissingObjects.Should().Be(1);
        report.UnknownObjects.Should().Be(1);
        report.FailedDeletes.Should().Be(0);
        report.FreedBytes.Should().BeGreaterThan(3);
        foreach (var variant in new[] { "original", "preview", "thumb" })
        {
            File.Exists(PathOf(Key(projectId, leftover, variant))).Should().BeFalse("leftover " + variant);
            File.Exists(PathOf(Key(projectId, live, variant))).Should().BeTrue("live " + variant);
        }
        File.Exists(PathOf(Key(projectId, discarded, "original"))).Should().BeFalse();
        File.Exists(PathOf(Key(projectId, discarded, "preview"))).Should().BeTrue();
        File.Exists(PathOf(oldOrphan)).Should().BeFalse();
        File.Exists(PathOf(freshOrphan)).Should().BeTrue("an upload may still be committing");
        File.Exists(PathOf(unknown)).Should().BeTrue("unrecognised keys are never deleted");
        File.Exists(oldTemp).Should().BeFalse();
        (await UsageAsync(Client)).GetProperty("usedBytes").GetInt64().Should().Be(usedBefore, "the database stays the quota source of truth");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entry = await db.ActivityLogs.AsNoTracking().Where(a => a.Action == "storage.reconciled").OrderByDescending(a => a.Id).FirstAsync();
            entry.ActorType.Should().Be("SYSTEM");
            var data = JsonDocument.Parse(entry.Data).RootElement;
            data.GetProperty("orphansDeleted").GetInt32().Should().Be(1);
            data.GetProperty("deletedImagesPurged").GetInt32().Should().Be(3);
            data.GetProperty("missingObjects").GetInt32().Should().Be(1);
        }

        var again = await SweepAsync();
        (again.OrphansDeleted + again.DeletedImagesPurged + again.StrayVariantsDeleted + again.IncompleteWritesDeleted).Should().Be(0);
        again.MissingObjects.Should().Be(1);
        File.Exists(PathOf(freshOrphan)).Should().BeTrue();
    }
}
