using System.Text.Json;
using System.Text.RegularExpressions;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Storage;

/// <summary>Errors = steps that failed and were skipped; StoredBytes/DatabaseBytes are null when they could not be read.</summary>
public sealed record StorageSweepReport(
    int OrphansDeleted, int DeletedImagesPurged, int StrayVariantsDeleted, int IncompleteWritesDeleted,
    int MissingObjects, int UnknownObjects, int FailedDeletes, int Errors, long FreedBytes, long? StoredBytes, long? DatabaseBytes)
{
    /// <summary>Something was deleted, or something went wrong — always recorded.</summary>
    public bool TookAction =>
        OrphansDeleted + DeletedImagesPurged + StrayVariantsDeleted + IncompleteWritesDeleted + FailedDeletes + Errors > 0;

    public bool HasFindings => TookAction || MissingObjects + UnknownObjects > 0;
}

/// <summary>
/// P10 storage hygiene (BR-86), run by StorageReconciliationService: lists the objects under "projects/" and "intake/" (plan 13) and repairs the
/// drift against the files rows — objects of soft-deleted rows (failed deletes are retried here), variants a live row no
/// longer keeps (discarded originals), objects without any row once older than <see cref="OrphanGrace"/> (an upload
/// commits within seconds of writing), and interrupted writes. Keys that do not look like an image variant are only
/// counted, never deleted. The database stays the quota source of truth.
/// Every step is guarded on its own: a failing call is counted in Errors and the remaining steps still run, so the
/// deletes already done are always recorded in storage.reconciled. That row is written when the sweep did something
/// (or failed) or when its missing/unknown counts differ from the last row — not daily for the same permanent finding.
/// </summary>
public sealed partial class StorageSweeper(
    AppDbContext db, IFileStorage storage, StorageAlertService alerts, ActivityWriter activity, IClock clock,
    ILogger<StorageSweeper> logger)
{
    public static readonly TimeSpan OrphanGrace = TimeSpan.FromHours(24);
    private const long DriftToleranceBytes = 1_048_576;
    /// <summary>Plan 13: intake photos keep their "intake/" key for life, also after they join a project.</summary>
    private static readonly string[] ImagesPrefixes = ["projects/", "intake/"];
    private const string ReconciledAction = "storage.reconciled";

    private sealed record LiveImage(string StorageKey, bool PreviewIsOriginal, DateTimeOffset? OriginalDeletedAt);

    private sealed record KnownRow(string StorageKey, string? OwnerType, DateTimeOffset? DeletedAt);

    public async Task<StorageSweepReport> RunAsync(CancellationToken ct = default)
    {
        var errors = 0;
        var cutoff = clock.UtcNow - OrphanGrace;

        // 1. Listed before the rows are read: an upload committing in between has fresh objects (spared by the grace
        //    period) and its rows are re-checked before anything is reported missing.
        IReadOnlyList<StoredObject>? objects = null;
        try
        {
            var listed = new List<StoredObject>();
            foreach (var prefix in ImagesPrefixes) listed.AddRange(await storage.ListAsync(prefix, ct));
            objects = listed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors++;
            LogStepFailure("list", ex);
        }

        // 2. Classify against the rows (only the columns and rows needed)
        List<StoredObject> purge = [], stray = [], orphans = [];
        List<string> missing = [];
        var unknown = 0;
        if (objects is not null)
        {
            try
            {
                var liveRows = await db.Files.AsNoTracking()
                    .Where(f => f.DeletedAt == null && f.OwnerType != null)
                    .Select(f => new LiveImage(f.StorageKey, f.PreviewIsOriginal, f.OriginalDeletedAt))
                    .ToListAsync(ct);
                var keep = liveRows.SelectMany(KeysOf).ToHashSet(StringComparer.Ordinal);
                var live = liveRows.Select(r => r.StorageKey).ToHashSet(StringComparer.Ordinal);

                var unmatched = new List<(StoredObject Object, string Prefix)>();
                foreach (var o in objects)
                {
                    if (keep.Contains(o.Key)) continue;
                    var match = VariantKeyPattern().Match(o.Key);
                    if (!match.Success)
                    {
                        unknown++;
                        continue;
                    }
                    var prefix = match.Groups["prefix"].Value;
                    if (live.Contains(prefix)) stray.Add(o);
                    else unmatched.Add((o, prefix));
                }

                // Rows behind the objects no live image claims: soft-deleted images, non-image files, or none (orphans)
                var prefixes = unmatched.Select(u => u.Prefix).Distinct().ToList();
                var known = prefixes.Count == 0
                    ? new Dictionary<string, KnownRow>(StringComparer.Ordinal)
                    : (await db.Files.AsNoTracking()
                        .Where(f => prefixes.Contains(f.StorageKey))
                        .Select(f => new KnownRow(f.StorageKey, f.OwnerType, f.DeletedAt))
                        .ToListAsync(ct)).ToDictionary(r => r.StorageKey, StringComparer.Ordinal);
                foreach (var (o, prefix) in unmatched)
                {
                    if (!known.TryGetValue(prefix, out var row))
                    {
                        if (o.LastModified < cutoff) orphans.Add(o);
                    }
                    else if (row.OwnerType == null) unknown++;
                    else if (row.DeletedAt != null) purge.Add(o);
                    // else: a live row committed after the first read — keep
                }

                var listed = objects.Select(o => o.Key).ToHashSet(StringComparer.Ordinal);
                missing = keep.Where(k => !listed.Contains(k)).ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors++;
                LogStepFailure("classify", ex);
                purge.Clear();
                stray.Clear();
                orphans.Clear();
                missing.Clear();
            }
        }

        // 3. Re-check "missing" keys: an upload may have written them after the listing and committed before step 2
        if (missing.Count > 0)
        {
            try
            {
                var present = new HashSet<string>(StringComparer.Ordinal);
                foreach (var folder in missing.Select(k => k[..(k.LastIndexOf('/') + 1)]).Distinct())
                    foreach (var o in await storage.ListAsync(folder, ct))
                        present.Add(o.Key);
                missing.RemoveAll(present.Contains);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors++;
                LogStepFailure("re-check missing", ex);
            }
        }

        // 4. Delete
        var doomed = purge.Concat(stray).Concat(orphans).ToList();
        var failed = new HashSet<string>(StringComparer.Ordinal);
        if (doomed.Count > 0)
        {
            try
            {
                failed.UnionWith(await storage.DeleteManyAsync(doomed.Select(o => o.Key).ToList(), ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors++;
                LogStepFailure("delete", ex);
                failed.UnionWith(doomed.Select(o => o.Key));   // unknown outcome: claim nothing, the next run retries
            }
        }
        int Gone(List<StoredObject> list) => list.Count(o => !failed.Contains(o.Key));

        // 5. Interrupted writes
        var incomplete = 0;
        try
        {
            incomplete = await storage.PurgeIncompleteAsync(cutoff, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors++;
            LogStepFailure("purge interrupted writes", ex);
        }

        // 6. Sizes
        long? storedBytes = null, databaseBytes = null;
        try
        {
            storedBytes = await storage.TotalBytesAsync("", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors++;
            LogStepFailure("total bytes", ex);
        }
        try
        {
            databaseBytes = await alerts.UsedBytesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors++;
            LogStepFailure("database bytes", ex);
        }

        var report = new StorageSweepReport(
            OrphansDeleted: Gone(orphans), DeletedImagesPurged: Gone(purge), StrayVariantsDeleted: Gone(stray),
            IncompleteWritesDeleted: incomplete, MissingObjects: missing.Count, UnknownObjects: unknown,
            FailedDeletes: failed.Count, Errors: errors,
            FreedBytes: doomed.Where(o => !failed.Contains(o.Key)).Sum(o => o.SizeBytes),
            StoredBytes: storedBytes, DatabaseBytes: databaseBytes);

        // 7. Audit row
        try
        {
            if (report.TookAction || await FindingsChangedAsync(report, ct))
            {
                activity.Write("storage", StorageAlert.ActivityEntityId, ReconciledAction, actorType: "SYSTEM", data: new
                {
                    orphansDeleted = report.OrphansDeleted, deletedImagesPurged = report.DeletedImagesPurged,
                    strayVariantsDeleted = report.StrayVariantsDeleted, incompleteWritesDeleted = report.IncompleteWritesDeleted,
                    missingObjects = report.MissingObjects, unknownObjects = report.UnknownObjects, failedDeletes = report.FailedDeletes,
                    errors = report.Errors, freedBytes = report.FreedBytes, storedBytes = report.StoredBytes,
                    databaseBytes = report.DatabaseBytes,
                });
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogStepFailure("activity", ex);
        }

        logger.LogInformation(
            "Storage sweep removed {Orphans} orphan(s), {Purged} object(s) of deleted images, {Stray} stray variant(s), {Incomplete} interrupted write(s); {Freed} bytes freed",
            report.OrphansDeleted, report.DeletedImagesPurged, report.StrayVariantsDeleted, report.IncompleteWritesDeleted, report.FreedBytes);
        if (report.MissingObjects + report.UnknownObjects + report.FailedDeletes + report.Errors > 0)
            logger.LogWarning(
                "Storage sweep: {Missing} expected object(s) missing, {Unknown} unrecognised object(s) left alone, {Failed} delete(s) failed, {Errors} step(s) failed",
                report.MissingObjects, report.UnknownObjects, report.FailedDeletes, report.Errors);
        if (report.StoredBytes is { } stored && report.DatabaseBytes is { } counted
            && Math.Abs(stored - counted) > DriftToleranceBytes)
            logger.LogWarning("Storage drift: provider holds {StoredBytes} bytes, database counts {DbBytes}", stored, counted);
        return report;
    }

    private static IEnumerable<string> KeysOf(LiveImage r) =>
        new ProjectFile { StorageKey = r.StorageKey, PreviewIsOriginal = r.PreviewIsOriginal, OriginalDeletedAt = r.OriginalDeletedAt }
            .StoredVariantKeys();

    /// <summary>Missing/unknown counts differ from the last storage.reconciled row (none = zero findings).</summary>
    private async Task<bool> FindingsChangedAsync(StorageSweepReport report, CancellationToken ct)
    {
        var last = await db.ActivityLogs.AsNoTracking()
            .Where(a => a.EntityType == "storage" && a.Action == ReconciledAction)
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Select(a => a.Data)
            .FirstOrDefaultAsync(ct);
        int missing = 0, unknown = 0;
        if (last is not null)
        {
            using var doc = JsonDocument.Parse(last);
            if (doc.RootElement.TryGetProperty("missingObjects", out var m)) missing = m.GetInt32();
            if (doc.RootElement.TryGetProperty("unknownObjects", out var u)) unknown = u.GetInt32();
        }
        return report.MissingObjects != missing || report.UnknownObjects != unknown;
    }

    private void LogStepFailure(string step, Exception ex) =>
        logger.LogWarning("Storage sweep step '{Step}' failed ({ExceptionType}); continuing", step, ex.GetType().Name);

    /// <summary>projects/{projectId}/{fileId}/{variant}.webp or intake/{submissionId}/{fileId}/{variant}.webp — the only layouts the sweep may delete.</summary>
    [GeneratedRegex(
        "^(?<prefix>(?:projects|intake)/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/(original|preview|thumb)\\.webp$",
        RegexOptions.CultureInvariant)]
    private static partial Regex VariantKeyPattern();
}
