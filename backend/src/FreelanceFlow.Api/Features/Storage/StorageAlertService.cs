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

    /// <summary>
    /// Runs after a commit that changed usage (and daily). Own transaction: storage_alerts + activity storage.threshold;
    /// notifications are queued only once that commit succeeded.
    /// </summary>
    public async Task EvaluateAsync(CancellationToken ct = default)
    {
        var used = await UsedBytesAsync(ct);
        var alerts = await db.StorageAlerts.ToListAsync(ct);
        var active = alerts.Where(a => a.ClearedAt == null).Select(a => a.Threshold).ToHashSet();
        var changes = StorageThresholds.Evaluate(used, Quota, active);
        if (changes.Count == 0) return;

        var now = clock.UtcNow;
        var percent = StorageThresholds.Percent(used, Quota);
        foreach (var change in changes)
        {
            var row = alerts.FirstOrDefault(a => a.Threshold == change.Threshold);
            if (change.Raised)
            {
                if (row is null)
                {
                    row = new StorageAlert { Threshold = change.Threshold };
                    db.StorageAlerts.Add(row);
                }
                row.NotifiedAt = now;
                row.ClearedAt = null;
            }
            else if (row is not null)
            {
                row.ClearedAt = now;
            }
            activity.Write("storage", StorageAlert.ActivityEntityId, "storage.threshold", actorType: "SYSTEM",
                data: new { threshold = change.Threshold, raised = change.Raised, usedBytes = used, quotaBytes = Quota, percent });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // a concurrent evaluation recorded the same threshold first; it also sent the alert
            logger.LogInformation("Storage threshold evaluation lost a race; skipped");
            return;
        }

        foreach (var change in changes)
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
