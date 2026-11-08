using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Files;

public class ProjectFile
{
    public const int MaxOriginalName = 255;

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? ProjectId { get; set; }
    public Guid? DeliverableId { get; set; }
    public Guid? RequestId { get; set; }
    public string Kind { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "";
    /// <summary>Bytes stored for this row — for images the sum of the three variants (quota source of truth).</summary>
    public long SizeBytes { get; set; }
    /// <summary>Images: the object prefix "projects/{projectId}/{fileId}"; variants live under it.</summary>
    public string StorageKey { get; set; } = "";
    public byte[] Sha256 { get; set; } = [];
    public bool VisibleToClient { get; set; }
    public string UploadedBy { get; set; } = "";
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Plan 08 — images (null on non-image rows)
    public string? OwnerType { get; set; }
    public Guid? OwnerId { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long? OriginalBytes { get; set; }
    public long? PreviewBytes { get; set; }
    public long? ThumbBytes { get; set; }

    public static string ImageKeyPrefix(Guid projectId, Guid fileId) => $"projects/{projectId}/{fileId}";

    public string VariantKey(string variant) =>
        ImageVariants.All.Contains(variant)
            ? $"{StorageKey}/{variant}.webp"
            : throw new DomainException("VALIDATION_FAILED", "Biến thể ảnh không hợp lệ");

    public IEnumerable<string> VariantKeys() => ImageVariants.All.Select(VariantKey);

    /// <summary>A processed image (spec §3/§6). Client images are REFERENCE, delivery FINAL, the rest PREVIEW.</summary>
    public static ProjectFile CreateImage(
        Guid projectId, string ownerType, Guid ownerId, Guid? requestId, string uploadedBy, bool visibleToClient,
        string? originalName, int width, int height, long originalBytes, long previewBytes, long thumbBytes,
        byte[] sha256, DateTimeOffset now)
    {
        if (!ImageOwnerTypes.All.Contains(ownerType))
            throw new DomainException("VALIDATION_FAILED", "Loại chủ sở hữu ảnh không hợp lệ");
        if (uploadedBy is not ("DESIGNER" or "CLIENT"))
            throw new DomainException("VALIDATION_FAILED", "Người tải ảnh không hợp lệ");
        if (width <= 0 || height <= 0 || originalBytes <= 0 || previewBytes <= 0 || thumbBytes <= 0)
            throw new DomainException("VALIDATION_FAILED", "Ảnh rỗng");

        var id = Guid.CreateVersion7();
        return new ProjectFile
        {
            Id = id,
            ProjectId = projectId,
            RequestId = requestId,
            Kind = ownerType switch
            {
                ImageOwnerTypes.ClientRequest => "REFERENCE",
                ImageOwnerTypes.Deliverable => "FINAL",
                _ => "PREVIEW",
            },
            OwnerType = ownerType,
            OwnerId = ownerId,
            OriginalName = SanitizeName(originalName),
            ContentType = "image/webp",
            Width = width,
            Height = height,
            OriginalBytes = originalBytes,
            PreviewBytes = previewBytes,
            ThumbBytes = thumbBytes,
            SizeBytes = originalBytes + previewBytes + thumbBytes,
            StorageKey = ImageKeyPrefix(projectId, id),
            Sha256 = sha256,
            VisibleToClient = visibleToClient,
            UploadedBy = uploadedBy,
            CreatedAt = now,
        };
    }

    public void SoftDelete(DateTimeOffset now) => DeletedAt ??= now;

    /// <summary>Progress-update images are always shown; client images keep the client-only rule (07-security §4.4).</summary>
    public void SetVisibleToClient(bool visible)
    {
        if (OwnerType == ImageOwnerTypes.ProgressUpdate)
            throw new DomainException("BUSINESS_RULE", "Ảnh cập nhật tiến độ luôn hiển thị cho khách");
        if (OwnerType == ImageOwnerTypes.ClientRequest)
            throw new DomainException("BUSINESS_RULE", "Ảnh khách gửi không đổi chế độ hiển thị");
        VisibleToClient = visible;
    }

    private static string SanitizeName(string? name)
    {
        var fileName = Path.GetFileName((name ?? "").Replace('\\', '/'));
        var clean = new string(fileName.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length == 0) return "anh";
        return clean.Length > MaxOriginalName ? clean[..MaxOriginalName] : clean;
    }
}
