namespace FreelanceFlow.Api.Features.Storage;

public sealed record StorageUsageDto(
    long UsedBytes, long QuotaBytes, double Percent, string Level, bool UploadsBlocked, IReadOnlyList<int> Thresholds);

public sealed record StorageImageDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string ProjectStatus,
    string ClientName,
    string OwnerType,
    Guid OwnerId,
    string? TaskTitle,
    string OriginalName,
    int Width,
    int Height,
    long SizeBytes,
    bool VisibleToClient,
    string UploadedBy,
    DateTimeOffset CreatedAt,
    string ThumbUrl,
    string PreviewUrl,
    DateTimeOffset? OriginalDeletedAt);

public sealed record StorageImagePageDto(IReadOnlyList<StorageImageDto> Items, int Page, int PageSize, int Total, long TotalBytes);

/// <summary>P10 Settings › Dung lượng › Theo dự án. CompletedAt only for COMPLETED projects.</summary>
public sealed record StorageProjectDto(
    Guid ProjectId, string ProjectName, string ClientName, string ProjectStatus, DateTimeOffset? CompletedAt,
    int ImageCount, long TotalBytes, long OriginalBytes, int OriginalsDeleted);

public sealed record OriginalCleanupProjectDto(Guid ProjectId, string ProjectName, DateTimeOffset CompletedAt, int ImageCount, long FreedBytes);

public sealed record OriginalCleanupPreviewDto(
    int OlderThanMonths, DateTimeOffset Cutoff, int ProjectCount, int ImageCount, long FreedBytes,
    IReadOnlyList<OriginalCleanupProjectDto> Projects);

/// <summary>The preview's numbers are sent back so a run never acts on figures the designer did not see.</summary>
public sealed record OriginalCleanupBody(int? OlderThanMonths, int? ExpectedImageCount, long? ExpectedFreedBytes);

public sealed record OriginalCleanupResultDto(int ProjectCount, int ImageCount, long FreedBytes);
