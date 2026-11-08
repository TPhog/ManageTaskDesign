using System.Security.Cryptography;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Storage;

/// <summary>
/// P10 (BR-89): manual cleanup of originals of old completed projects; preview and thumb stay. A project's completion
/// is the latest project.status_changed entry (COMPLETED is only reached through POST /projects/{id}/status, so for a
/// COMPLETED project that entry is the move to COMPLETED), else projects.updated_at.
/// </summary>
public static class OriginalCleanup
{
    public const int MinMonths = 1;
    public const int MaxMonths = 120;
    /// <summary>image.originals_deleted lists at most this many ids; fileCount always has the full number.</summary>
    public const int MaxLoggedFileIds = 200;

    public sealed class OriginalCleanupCandidate
    {
        public Guid FileId { get; init; }
        public long OriginalBytes { get; init; }
        public Guid ProjectId { get; init; }
        public string ProjectName { get; init; } = "";
        public Guid ClientId { get; init; }
        public DateTimeOffset CompletedAt { get; init; }
    }

    /// <summary>Live images of projects completed before the cutoff whose original is still stored, excluding client photos stored once and quotation evidence (BR-87).</summary>
    public static Task<List<OriginalCleanupCandidate>> CandidatesAsync(AppDbContext db, DateTimeOffset cutoff, CancellationToken ct) =>
        (from f in db.Files.AsNoTracking()
         join p in db.Projects on f.ProjectId equals (Guid?)p.Id
         let completedAt = db.ActivityLogs
             .Where(a => a.ProjectId == p.Id && a.Action == "project.status_changed")
             .Max(a => (DateTimeOffset?)a.OccurredAt) ?? p.UpdatedAt
         where p.Status == "COMPLETED" && completedAt < cutoff
               && f.DeletedAt == null && f.OwnerType != null
               && f.OriginalDeletedAt == null && !f.PreviewIsOriginal
               && !db.Quotations.Any(q => q.EvidenceFileId == f.Id)
         orderby completedAt, f.Id
         select new OriginalCleanupCandidate
         {
             FileId = f.Id, OriginalBytes = f.OriginalBytes ?? 0, ProjectId = p.Id, ProjectName = p.Name,
             ClientId = p.ClientId, CompletedAt = completedAt,
         })
        .ToListAsync(ct);

    public static Task<Dictionary<Guid, DateTimeOffset>> CompletedAtAsync(AppDbContext db, List<Guid> projectIds, CancellationToken ct) =>
        (from p in db.Projects.AsNoTracking()
         where projectIds.Contains(p.Id) && p.Status == "COMPLETED"
         select new
         {
             p.Id,
             CompletedAt = db.ActivityLogs
                 .Where(a => a.ProjectId == p.Id && a.Action == "project.status_changed")
                 .Max(a => (DateTimeOffset?)a.OccurredAt) ?? p.UpdatedAt,
         })
        .ToDictionaryAsync(x => x.Id, x => x.CompletedAt, ct);

    public static OriginalCleanupPreviewDto Summarize(int months, DateTimeOffset cutoff, IReadOnlyList<OriginalCleanupCandidate> rows)
    {
        var projects = rows.GroupBy(r => r.ProjectId)
            .Select(g => new OriginalCleanupProjectDto(g.Key, g.First().ProjectName, g.First().CompletedAt, g.Count(), g.Sum(r => r.OriginalBytes)))
            .OrderByDescending(p => p.FreedBytes).ThenBy(p => p.ProjectId)
            .ToList();
        return new OriginalCleanupPreviewDto(months, cutoff, projects.Count, rows.Count, rows.Sum(r => r.OriginalBytes),
            Digest(rows.Select(r => r.FileId)), projects);
    }

    /// <summary>Stable fingerprint of an id set: SHA-256 over the sorted ids (big-endian bytes), lowercase hex.</summary>
    public static string Digest(IEnumerable<Guid> ids)
    {
        var sorted = ids.Distinct().Order().ToList();
        var buffer = new byte[sorted.Count * 16];
        for (var i = 0; i < sorted.Count; i++) sorted[i].TryWriteBytes(buffer.AsSpan(i * 16, 16), bigEndian: true, out _);
        return Convert.ToHexStringLower(SHA256.HashData(buffer));
    }

    /// <summary>
    /// Inside the run's transaction: locks the rows of <paramref name="ids"/> (FOR UPDATE) that are still eligible —
    /// live, original stored, not a client photo stored once, not quotation evidence (BR-87, re-checked here).
    /// </summary>
    public static Task<List<Guid>> LockEligibleAsync(AppDbContext db, Guid[] ids, CancellationToken ct) =>
        db.Database.SqlQuery<Guid>($"""
            SELECT f.id AS "Value" FROM files f
            WHERE f.id = ANY({ids}) AND f.deleted_at IS NULL AND f.original_deleted_at IS NULL
              AND NOT f.preview_is_original
              AND NOT EXISTS (SELECT 1 FROM quotations q WHERE q.evidence_file_id = f.id)
            FOR UPDATE OF f
            """).ToListAsync(ct);
}
