namespace FreelanceFlow.Api.Infrastructure.Storage;

/// <summary>
/// File storage for uploads (spec §5). Keys are lowercase "projects/{projectId}/{fileId}/{variant}.webp".
/// Only LocalFileStorage exists today; the P10 sweep methods map 1:1 to S3/R2 calls.
/// </summary>
public interface IFileStorage
{
    Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default);

    /// <summary>null when the file does not exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);

    /// <summary>Idempotent: deleting a missing file succeeds.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Bytes stored under the prefix ("" = everything) — daily reconciliation against the database.</summary>
    Task<long> TotalBytesAsync(string prefix, CancellationToken ct = default);

    /// <summary>Committed objects under the prefix ("" = everything); never in-progress writes. R2: ListObjectsV2.</summary>
    Task<IReadOnlyList<StoredObject>> ListAsync(string prefix, CancellationToken ct = default);

    /// <summary>Idempotent like DeleteAsync. Returns the keys that could not be deleted (empty = all gone). R2: DeleteObjects.</summary>
    Task<IReadOnlyList<string>> DeleteManyAsync(IReadOnlyCollection<string> keys, CancellationToken ct = default);

    /// <summary>
    /// Removes interrupted writes older than <paramref name="olderThan"/> (local: ".tmp-" files and empty image folders;
    /// R2: aborted multipart uploads). Returns the number of interrupted writes removed.
    /// </summary>
    Task<int> PurgeIncompleteAsync(DateTimeOffset olderThan, CancellationToken ct = default);
}

/// <summary>One stored object (P10 sweep). LastModified drives the orphan grace period.</summary>
public sealed record StoredObject(string Key, long SizeBytes, DateTimeOffset LastModified);
