namespace FreelanceFlow.Api.Infrastructure.Storage;

/// <summary>
/// File storage for uploads (spec §5). Keys are lowercase "projects/{projectId}/{fileId}/{variant}.webp".
/// Only LocalFileStorage exists today; a cloud provider can implement this later.
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
}
