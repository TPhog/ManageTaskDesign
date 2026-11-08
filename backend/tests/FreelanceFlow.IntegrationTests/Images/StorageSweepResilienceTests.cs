using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace FreelanceFlow.IntegrationTests.Images;

/// <summary>
/// P10 review: one failing storage call must not skip the rest of the sweep or its audit row. One class per case so
/// each gets its own database and files root (a sweep sees everything in storage).
/// </summary>
public abstract class StorageSweepResilienceTestBase(AppFactory factory) : ImageTestBase(factory)
{
    /// <summary>The real local storage with chosen calls failing or a first listing that misses some keys.</summary>
    protected sealed class FlakyStorage(IFileStorage inner) : IFileStorage
    {
        public bool FailPurge { get; init; }
        public bool FailTotal { get; init; }
        public bool FailDeleteMany { get; init; }
        public HashSet<string> HiddenFromFirstListing { get; init; } = [];
        private bool _listed;

        public Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default) =>
            inner.PutAsync(key, content, contentType, ct);
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default) => inner.OpenReadAsync(key, ct);
        public Task DeleteAsync(string key, CancellationToken ct = default) => inner.DeleteAsync(key, ct);

        public Task<long> TotalBytesAsync(string prefix, CancellationToken ct = default) =>
            FailTotal ? throw new IOException("disk gone") : inner.TotalBytesAsync(prefix, ct);

        public async Task<IReadOnlyList<StoredObject>> ListAsync(string prefix, CancellationToken ct = default)
        {
            var objects = await inner.ListAsync(prefix, ct);
            if (_listed) return objects;
            _listed = true;
            return objects.Where(o => !HiddenFromFirstListing.Contains(o.Key)).ToList();
        }

        public Task<IReadOnlyList<string>> DeleteManyAsync(IReadOnlyCollection<string> keys, CancellationToken ct = default) =>
            FailDeleteMany ? throw new IOException("delete refused") : inner.DeleteManyAsync(keys, ct);

        public Task<int> PurgeIncompleteAsync(DateTimeOffset olderThan, CancellationToken ct = default) =>
            FailPurge ? throw new UnauthorizedAccessException("lost+found") : inner.PurgeIncompleteAsync(olderThan, ct);
    }

    protected string PathOf(string key) => Path.Combine(Factory.FilesRoot, key.Replace('/', Path.DirectorySeparatorChar));

    protected async Task<(Guid ProjectId, Guid ImageId)> UploadAsync(bool softDelete, SKColor colour)
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án sweep lỗi " + Guid.NewGuid());
        var taskId = await CreateTaskAsync(projectId);
        var id = (await UploadTaskImagesAsync(Client, taskId, TestImages.Png(60, 40, colour)))[0].GetProperty("id").GetGuid();
        if (softDelete)
        {
            using var scope = Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Files.Where(f => f.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.DeletedAt, DateTimeOffset.UtcNow));
        }
        return (projectId, id);
    }

    protected async Task<StorageSweepReport> SweepAsync(Func<IFileStorage, FlakyStorage> wrap)
    {
        using var scope = Factory.Services.CreateScope();
        var storage = wrap(scope.ServiceProvider.GetRequiredService<IFileStorage>());
        return await ActivatorUtilities.CreateInstance<StorageSweeper>(scope.ServiceProvider, (IFileStorage)storage).RunAsync();
    }

    protected async Task<JsonElement> LastReconciledAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var entry = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ActivityLogs.AsNoTracking()
            .Where(a => a.Action == "storage.reconciled").OrderByDescending(a => a.Id).FirstAsync();
        return JsonDocument.Parse(entry.Data).RootElement.Clone();
    }
}

public class StorageSweepFailingPurgeTests(AppFactory factory) : StorageSweepResilienceTestBase(factory)
{
    [Fact]
    public async Task Failing_purge_and_size_calls_do_not_undo_the_deletes_or_drop_the_audit_row()
    {
        var (projectId, id) = await UploadAsync(softDelete: true, SKColors.Crimson);

        var report = await SweepAsync(s => new FlakyStorage(s) { FailPurge = true, FailTotal = true });

        report.DeletedImagesPurged.Should().Be(3);
        report.Errors.Should().Be(2);
        report.StoredBytes.Should().BeNull();
        report.DatabaseBytes.Should().NotBeNull();
        foreach (var variant in new[] { "original", "preview", "thumb" })
            File.Exists(PathOf($"projects/{projectId}/{id}/{variant}.webp")).Should().BeFalse(variant);
        var data = await LastReconciledAsync();
        data.GetProperty("deletedImagesPurged").GetInt32().Should().Be(3);
        data.GetProperty("errors").GetInt32().Should().Be(2);
    }
}

public class StorageSweepFailingDeleteTests(AppFactory factory) : StorageSweepResilienceTestBase(factory)
{
    [Fact]
    public async Task A_throwing_batch_delete_still_purges_interrupted_writes_and_is_recorded()
    {
        var (projectId, id) = await UploadAsync(softDelete: true, SKColors.DarkOrange);
        var oldTemp = PathOf($"projects/{projectId}/{id}/thumb.webp") + ".tmp-0123456789abcdef0123456789abcdef";
        await File.WriteAllBytesAsync(oldTemp, [1]);
        File.SetLastWriteTimeUtc(oldTemp, DateTime.UtcNow.AddHours(-30));

        var report = await SweepAsync(s => new FlakyStorage(s) { FailDeleteMany = true });

        report.DeletedImagesPurged.Should().Be(0);
        report.FailedDeletes.Should().Be(3);
        report.Errors.Should().Be(1);
        report.IncompleteWritesDeleted.Should().Be(1);
        report.FreedBytes.Should().Be(0);
        File.Exists(PathOf($"projects/{projectId}/{id}/original.webp")).Should().BeTrue("the next run retries");
        File.Exists(oldTemp).Should().BeFalse();
        var data = await LastReconciledAsync();
        data.GetProperty("failedDeletes").GetInt32().Should().Be(3);
        data.GetProperty("errors").GetInt32().Should().Be(1);
    }
}

public class StorageSweepLateUploadTests(AppFactory factory) : StorageSweepResilienceTestBase(factory)
{
    [Fact]
    public async Task An_upload_committed_after_the_listing_is_not_reported_missing()
    {
        var (projectId, id) = await UploadAsync(softDelete: false, SKColors.SeaGreen);
        var keys = new[] { "original", "preview", "thumb" }.Select(v => $"projects/{projectId}/{id}/{v}.webp").ToHashSet();

        var report = await SweepAsync(s => new FlakyStorage(s) { HiddenFromFirstListing = keys });

        report.MissingObjects.Should().Be(0);
        report.HasFindings.Should().BeFalse();
        File.Exists(PathOf($"projects/{projectId}/{id}/original.webp")).Should().BeTrue();
    }
}
