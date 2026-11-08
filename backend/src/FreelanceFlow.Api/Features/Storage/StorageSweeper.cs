using System.Text.RegularExpressions;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Storage;

public sealed record StorageSweepReport(
    int OrphansDeleted, int DeletedImagesPurged, int StrayVariantsDeleted, int IncompleteWritesDeleted,
    int MissingObjects, int UnknownObjects, int FailedDeletes, long FreedBytes, long StoredBytes, long DatabaseBytes)
{
    public bool HasFindings =>
        OrphansDeleted + DeletedImagesPurged + StrayVariantsDeleted + IncompleteWritesDeleted
        + MissingObjects + UnknownObjects + FailedDeletes > 0;
}

/// <summary>
/// P10 storage hygiene (BR-86), run by StorageReconciliationService: lists the objects under "projects/" and repairs the
/// drift against the files rows — objects of soft-deleted rows (failed deletes are retried here), variants a live row no
/// longer keeps (discarded originals), objects without any row once older than <see cref="OrphanGrace"/> (an upload
/// commits within seconds of writing), and interrupted writes. Keys that do not look like an image variant are only
/// counted, never deleted. The database stays the quota source of truth.
/// </summary>
public sealed partial class StorageSweeper(
    AppDbContext db, IFileStorage storage, StorageAlertService alerts, ActivityWriter activity, IClock clock,
    ILogger<StorageSweeper> logger)
{
    public static readonly TimeSpan OrphanGrace = TimeSpan.FromHours(24);
    private const long DriftToleranceBytes = 1_048_576;
    private const string ImagesPrefix = "projects/";

    public async Task<StorageSweepReport> RunAsync(CancellationToken ct = default)
    {
        var cutoff = clock.UtcNow - OrphanGrace;
        // Listed before the rows are read: an upload committing in between has fresh objects and is spared by the grace period
        var objects = await storage.ListAsync(ImagesPrefix, ct);
        var rows = await db.Files.AsNoTracking().ToListAsync(ct);
        var images = rows.Where(f => f.OwnerType != null).ToList();
        var keep = images.Where(f => f.DeletedAt == null).SelectMany(f => f.StoredVariantKeys()).ToHashSet(StringComparer.Ordinal);
        var live = images.Where(f => f.DeletedAt == null).Select(f => f.StorageKey).ToHashSet(StringComparer.Ordinal);
        var deleted = images.Where(f => f.DeletedAt != null).Select(f => f.StorageKey).ToHashSet(StringComparer.Ordinal);
        var otherFiles = rows.Where(f => f.OwnerType == null).Select(f => f.StorageKey).ToHashSet(StringComparer.Ordinal);

        List<StoredObject> purge = [], stray = [], orphans = [];
        var unknown = 0;
        foreach (var o in objects)
        {
            if (keep.Contains(o.Key)) continue;
            var match = VariantKeyPattern().Match(o.Key);
            if (!match.Success || otherFiles.Contains(match.Groups["prefix"].Value))
            {
                unknown++;
                continue;
            }
            var prefix = match.Groups["prefix"].Value;
            if (live.Contains(prefix)) stray.Add(o);
            else if (deleted.Contains(prefix)) purge.Add(o);
            else if (o.LastModified < cutoff) orphans.Add(o);
        }

        var doomed = purge.Concat(stray).Concat(orphans).ToList();
        var failed = doomed.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await storage.DeleteManyAsync(doomed.Select(o => o.Key).ToList(), ct)).ToHashSet(StringComparer.Ordinal);
        int Gone(List<StoredObject> list) => list.Count(o => !failed.Contains(o.Key));
        var listed = objects.Select(o => o.Key).ToHashSet(StringComparer.Ordinal);
        var incomplete = await storage.PurgeIncompleteAsync(cutoff, ct);

        var report = new StorageSweepReport(
            OrphansDeleted: Gone(orphans), DeletedImagesPurged: Gone(purge), StrayVariantsDeleted: Gone(stray),
            IncompleteWritesDeleted: incomplete, MissingObjects: keep.Count(k => !listed.Contains(k)),
            UnknownObjects: unknown, FailedDeletes: failed.Count,
            FreedBytes: doomed.Where(o => !failed.Contains(o.Key)).Sum(o => o.SizeBytes),
            StoredBytes: await storage.TotalBytesAsync("", ct), DatabaseBytes: await alerts.UsedBytesAsync(ct));

        if (report.HasFindings)
        {
            activity.Write("storage", StorageAlert.ActivityEntityId, "storage.reconciled", actorType: "SYSTEM", data: new
            {
                orphansDeleted = report.OrphansDeleted, deletedImagesPurged = report.DeletedImagesPurged,
                strayVariantsDeleted = report.StrayVariantsDeleted, incompleteWritesDeleted = report.IncompleteWritesDeleted,
                missingObjects = report.MissingObjects, unknownObjects = report.UnknownObjects, failedDeletes = report.FailedDeletes,
                freedBytes = report.FreedBytes, storedBytes = report.StoredBytes, databaseBytes = report.DatabaseBytes,
            });
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "Storage sweep removed {Orphans} orphan(s), {Purged} object(s) of deleted images, {Stray} stray variant(s), {Incomplete} interrupted write(s); {Freed} bytes freed",
            report.OrphansDeleted, report.DeletedImagesPurged, report.StrayVariantsDeleted, report.IncompleteWritesDeleted, report.FreedBytes);
        if (report.MissingObjects + report.UnknownObjects + report.FailedDeletes > 0)
            logger.LogWarning(
                "Storage sweep: {Missing} expected object(s) missing, {Unknown} unrecognised object(s) left alone, {Failed} delete(s) failed",
                report.MissingObjects, report.UnknownObjects, report.FailedDeletes);
        if (Math.Abs(report.StoredBytes - report.DatabaseBytes) > DriftToleranceBytes)
            logger.LogWarning("Storage drift: provider holds {StoredBytes} bytes, database counts {DbBytes}",
                report.StoredBytes, report.DatabaseBytes);
        return report;
    }

    /// <summary>projects/{projectId}/{fileId}/{variant}.webp — the only layout the sweep may delete.</summary>
    [GeneratedRegex(
        "^(?<prefix>projects/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/(original|preview|thumb)\\.webp$",
        RegexOptions.CultureInvariant)]
    private static partial Regex VariantKeyPattern();
}
