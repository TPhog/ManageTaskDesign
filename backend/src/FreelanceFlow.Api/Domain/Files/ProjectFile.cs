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
    /// <summary>Bytes stored for this row — for images the sum of the stored variants (quota source of truth).</summary>
    public long SizeBytes { get; set; }
    /// <summary>Images: the object prefix "projects/{projectId}/{fileId}" (plan 13 intake: "intake/{submissionId}/{fileId}"); variants live under it.</summary>
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

    // P10
    /// <summary>Client photos stored once: the "preview" variant is served from the original object (never for designer images).</summary>
    public bool PreviewIsOriginal { get; set; }
    /// <summary>Set when the designer cleaned up the original (old completed project); preview and thumb stay.</summary>
    public DateTimeOffset? OriginalDeletedAt { get; set; }

    public static string ImageKeyPrefix(Guid projectId, Guid fileId) => $"projects/{projectId}/{fileId}";

    /// <summary>Plan 13: photos of a public intake submission; the key stays when the photo joins a project.</summary>
    public static string IntakeKeyPrefix(Guid submissionId, Guid fileId) => $"intake/{submissionId}/{fileId}";

    public string VariantKey(string variant) =>
        ImageVariants.All.Contains(variant)
            ? $"{StorageKey}/{variant}.webp"
            : throw new DomainException("VALIDATION_FAILED", "Biến thể ảnh không hợp lệ");

    public IEnumerable<string> VariantKeys() => ImageVariants.All.Select(VariantKey);

    /// <summary>The variant whose object is streamed for <paramref name="variant"/> (P10 alias and fallback).</summary>
    public string ServedVariant(string variant)
    {
        if (!ImageVariants.All.Contains(variant))
            throw new DomainException("VALIDATION_FAILED", "Biến thể ảnh không hợp lệ");
        if (variant == ImageVariants.Preview && PreviewIsOriginal) return ImageVariants.Original;
        if (variant == ImageVariants.Original && OriginalDeletedAt != null) return ImageVariants.Preview;
        return variant;
    }

    /// <summary>Keys of the objects this row needs in storage (the sweep keeps exactly these for a live row).</summary>
    public IEnumerable<string> StoredVariantKeys() => ImageVariants.All.Where(IsStored).Select(VariantKey);

    private bool IsStored(string variant) => variant switch
    {
        ImageVariants.Original => OriginalDeletedAt == null,
        ImageVariants.Preview => !PreviewIsOriginal,
        _ => true,
    };

    /// <summary>
    /// P10 cleanup of an old completed project: the original goes, preview and thumb stay. Returns the bytes freed
    /// (0 when already done). A client photo stored once has no other copy and is refused.
    /// </summary>
    public long DiscardOriginal(DateTimeOffset now)
    {
        if (PreviewIsOriginal)
            throw new DomainException("BUSINESS_RULE", "Ảnh khách gửi chỉ có một bản — không xoá riêng bản gốc");
        if (OriginalDeletedAt != null) return 0;
        var freed = OriginalBytes ?? 0;
        OriginalDeletedAt = now;
        SizeBytes = (PreviewBytes ?? 0) + (ThumbBytes ?? 0);
        return freed;
    }

    /// <summary>A processed image (spec §3/§6). Client images are REFERENCE, delivery FINAL, the rest PREVIEW.</summary>
    public static ProjectFile CreateImage(
        Guid? projectId, string ownerType, Guid ownerId, Guid? requestId, string uploadedBy, bool visibleToClient,
        string? originalName, int width, int height, long originalBytes, long previewBytes, long thumbBytes,
        byte[] sha256, DateTimeOffset now, bool previewIsOriginal = false)
    {
        if (!ImageOwnerTypes.All.Contains(ownerType))
            throw new DomainException("VALIDATION_FAILED", "Loại chủ sở hữu ảnh không hợp lệ");
        if (uploadedBy is not ("DESIGNER" or "CLIENT"))
            throw new DomainException("VALIDATION_FAILED", "Người tải ảnh không hợp lệ");
        if (previewIsOriginal && uploadedBy != "CLIENT")
            throw new DomainException("VALIDATION_FAILED", "Ảnh của designer luôn có bản xem riêng");
        var intake = ownerType == ImageOwnerTypes.IntakeSubmission;
        if (intake != (projectId is null))
            throw new DomainException("VALIDATION_FAILED", intake ? "Ảnh khách mới chưa thuộc dự án nào" : "Ảnh phải thuộc một dự án");
        if (intake && requestId is not null)
            throw new DomainException("VALIDATION_FAILED", "Ảnh khách mới không gắn với yêu cầu");
        if (width <= 0 || height <= 0 || originalBytes <= 0 || thumbBytes <= 0
            || (previewIsOriginal ? previewBytes != 0 : previewBytes <= 0))
            throw new DomainException("VALIDATION_FAILED", "Ảnh rỗng");

        var id = Guid.CreateVersion7();
        return new ProjectFile
        {
            Id = id,
            ProjectId = projectId,
            RequestId = requestId,
            Kind = ownerType switch
            {
                ImageOwnerTypes.ClientRequest or ImageOwnerTypes.IntakeSubmission => "REFERENCE",
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
            PreviewIsOriginal = previewIsOriginal,
            SizeBytes = originalBytes + previewBytes + thumbBytes,
            StorageKey = intake ? IntakeKeyPrefix(ownerId, id) : ImageKeyPrefix(projectId!.Value, id),
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
        if (OwnerType == ImageOwnerTypes.IntakeSubmission)
            throw new DomainException("BUSINESS_RULE", "Ảnh khách mới gửi không đổi chế độ hiển thị");
        VisibleToClient = visible;
    }

    /// <summary>BR-136: on conversion the submission's photos join the new project; the stored objects never move.</summary>
    public void AttachToProject(Guid projectId)
    {
        if (OwnerType != ImageOwnerTypes.IntakeSubmission)
            throw new DomainException("BUSINESS_RULE", "Chỉ ảnh khách mới gửi được gắn vào dự án khi chuyển đổi");
        if (ProjectId is not null) throw new DomainException("BUSINESS_RULE", "Ảnh đã thuộc một dự án");
        ProjectId = projectId;
    }

    private static string SanitizeName(string? name)
    {
        var fileName = Path.GetFileName((name ?? "").Replace('\\', '/'));
        var clean = new string(fileName.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length == 0) return "anh";
        return clean.Length > MaxOriginalName ? clean[..MaxOriginalName] : clean;
    }
}
