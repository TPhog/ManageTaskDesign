using FreelanceFlow.Api.Domain.Files;

namespace FreelanceFlow.Api.Features.Images;

/// <summary>Designer-side image. URLs are permission-checked API paths that stream the file.</summary>
public sealed record ImageDto(
    Guid Id,
    Guid ProjectId,
    string OwnerType,
    Guid OwnerId,
    string OriginalName,
    int Width,
    int Height,
    long SizeBytes,
    bool VisibleToClient,
    string UploadedBy,
    DateTimeOffset CreatedAt,
    string ThumbUrl,
    string PreviewUrl,
    string OriginalUrl)
{
    /// <summary>P10: set once the original was cleaned up — OriginalUrl then streams the watermarked preview.</summary>
    public DateTimeOffset? OriginalDeletedAt { get; init; }
    /// <summary>P10: upload responses only — an identical image (same SHA-256) already in the project.</summary>
    public ImageDuplicateDto? DuplicateOf { get; init; }
}

/// <summary>P10 duplicate warning. SameUpload = an earlier file of the same request; TaskTitle only for live task images.</summary>
public sealed record ImageDuplicateDto(Guid Id, string OwnerType, Guid OwnerId, string? TaskTitle, DateTimeOffset CreatedAt, bool SameUpload);

public sealed record UpdateImageBody(bool? VisibleToClient);

public sealed record BulkDeleteBody(Guid[]? Ids);

public sealed record BulkDeleteResultDto(int Deleted, int Skipped, long FreedBytes);

public static class ImageDtos
{
    public static string AdminUrl(Guid id, string variant) => $"/api/admin/images/{id}/{variant}";

    public static string PortalUrl(Guid id, string variant) => $"/api/portal/images/{id}/{variant}";

    public static ImageDto ToAdmin(ProjectFile f) => new(
        f.Id, f.ProjectId!.Value, f.OwnerType!, f.OwnerId!.Value, f.OriginalName, f.Width ?? 0, f.Height ?? 0,
        f.SizeBytes, f.VisibleToClient, f.UploadedBy, f.CreatedAt,
        AdminUrl(f.Id, ImageVariants.Thumb), AdminUrl(f.Id, ImageVariants.Preview), AdminUrl(f.Id, ImageVariants.Original))
    {
        OriginalDeletedAt = f.OriginalDeletedAt,
    };
}
