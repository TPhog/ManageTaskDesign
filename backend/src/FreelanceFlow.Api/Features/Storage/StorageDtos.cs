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
