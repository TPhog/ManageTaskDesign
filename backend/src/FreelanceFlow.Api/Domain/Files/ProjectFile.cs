namespace FreelanceFlow.Api.Domain.Files;

public class ProjectFile
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? ProjectId { get; set; }
    public Guid? DeliverableId { get; set; }
    public Guid? RequestId { get; set; }
    public string Kind { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = "";
    public byte[] Sha256 { get; set; } = [];
    public bool VisibleToClient { get; set; }
    public string UploadedBy { get; set; } = "";
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
