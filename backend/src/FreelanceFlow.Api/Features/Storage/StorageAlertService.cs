using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Notifications;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Features.Storage;

/// <summary>Spec §2: usage from the database, upload blocking at 90 %, threshold alerts once per crossing.</summary>
public sealed class StorageAlertService(
    AppDbContext db, IOptions<StorageOptions> storageOptions, IOptions<AppSettings> appSettings, ActivityWriter activity,
    INotifier notifier, IClock clock, ILogger<StorageAlertService> logger)
{
    private long Quota => storageOptions.Value.QuotaBytes;

    public Task<long> UsedBytesAsync(CancellationToken ct = default) =>
        db.Files.Where(f => f.DeletedAt == null).SumAsync(f => f.SizeBytes, ct);

    public async Task<StorageUsageDto> UsageAsync(CancellationToken ct = default)
    {
        var used = await UsedBytesAsync(ct);
        return new StorageUsageDto(used, Quota, StorageThresholds.Percent(used, Quota), StorageThresholds.Level(used, Quota),
            StorageThresholds.UploadsBlocked(used, Quota), StorageThresholds.All);
    }

    public async Task EnsureUploadsAllowedAsync(CancellationToken ct = default)
    {
        if (StorageThresholds.UploadsBlocked(await UsedBytesAsync(ct), Quota))
            throw new DomainException("STORAGE_FULL",
                "Dung lượng lưu trữ đã đạt 90% — xoá bớt ảnh trong Cài đặt › Dung lượng để tải ảnh mới");
    }

    /// <summary>Plan 13: the public intake form takes photos only below 80 % (text is always accepted).</summary>
    public async Task<bool> IntakePhotosBlockedAsync(CancellationToken ct = default) =>
        StorageThresholds.IntakePhotosBlocked(await UsedBytesAsync(ct), Quota);

    /// <summary>
    /// Runs after a commit that changed usage (and daily). Own transaction: storage_alerts + activity storage.threshold;
    /// notifications are queued only once that commit succeeded.
    /// </summary>
    public async Task EvaluateAsync(CancellationToken ct = default)
    {
        var used = await UsedBytesAsync(ct);
        var alerts = await db.StorageAlerts.AsNoTracking().ToListAsync(ct);
        var active = alerts.Where(a => a.ClearedAt == null).Select(a => a.Threshold).ToHashSet();
        var changes = StorageThresholds.Evaluate(used, Quota, active);
        if (changes.Count == 0) return;

        var now = clock.UtcNow;
        var percent = StorageThresholds.Percent(used, Quota);
        // Each change is claimed atomically (first insert = PK, re-arm / clear = conditional UPDATE): only the
        // evaluation that wins writes the activity and notifies, so concurrent evaluations never double-alert.
        var claimedChanges = new List<ThresholdChange>();
        foreach (var change in changes)
        {
            var row = alerts.FirstOrDefault(a => a.Threshold == change.Threshold);
            if (!change.Raised && row is null) continue;
            if (await TryClaimAsync(change, row, used, percent, now, ct)) claimedChanges.Add(change);
            else logger.LogInformation("Storage threshold {Threshold} was handled by a concurrent evaluation; skipped", change.Threshold);
        }

        foreach (var change in claimedChanges)
        {
            if (!change.Raised)
            {
                logger.LogInformation("Storage dropped below the {Threshold}% threshold ({Percent}% used)", change.Threshold, percent);
                continue;
            }
            logger.LogWarning("Storage reached the {Threshold}% threshold ({Percent}% used)", change.Threshold, percent);
            if (!StorageThresholds.NotifyAt.Contains(change.Threshold)) continue;
            try
            {
                await notifier.EnqueueAsync(StorageAlertMessages.For(change.Threshold, used, Quota, appSettings.Value.PublicBaseUrl), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Could not enqueue the storage notification ({ExceptionType})", ex.GetType().Name);
            }
        }
    }

    private async Task<bool> TryClaimAsync(
        ThresholdChange change, StorageAlert? row, long used, double percent, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            if (change.Raised && row is null)
            {
                db.StorageAlerts.Add(new StorageAlert { Threshold = change.Threshold, NotifiedAt = now });   // PK collision = lost race
            }
            else
            {
                var t = change.Threshold;
                var affected = change.Raised
                    ? await db.StorageAlerts.Where(a => a.Threshold == t && a.ClearedAt != null)
                        .ExecuteUpdateAsync(s => s.SetProperty(a => a.ClearedAt, (DateTimeOffset?)null).SetProperty(a => a.NotifiedAt, now), ct)
                    : await db.StorageAlerts.Where(a => a.Threshold == t && a.ClearedAt == null)
                        .ExecuteUpdateAsync(s => s.SetProperty(a => a.ClearedAt, (DateTimeOffset?)now), ct);
                if (affected != 1) return false;
            }
            activity.Write("storage", StorageAlert.ActivityEntityId, "storage.threshold", actorType: "SYSTEM",
                data: new { threshold = change.Threshold, raised = change.Raised, usedBytes = used, quotaBytes = Quota, percent });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            return false;
        }
    }

    /// <summary>Request path: an evaluation failure must never fail an upload or delete that already committed.</summary>
    public async Task EvaluateSafelyAsync(CancellationToken ct = default)
    {
        try
        {
            await EvaluateAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Storage threshold evaluation failed ({ExceptionType})", ex.GetType().Name);
        }
    }
}
