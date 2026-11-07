using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Projects;

public class ProjectBrief : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProjectId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string Content { get; set; } = "";
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // BR-27: CONFIRMED briefs are immutable (also enforced by DB trigger trg_brief_immutable)
    public void Confirm(IClock clock)
    {
        if (Status == "CONFIRMED")
            throw new DomainException("BRIEF_LOCKED", "Brief đã được xác nhận");
        Status = "CONFIRMED";
        ConfirmedAt = clock.UtcNow;
    }

    // BR-28: new version copies content from latest; version number set by caller after checking max
    public ProjectBrief CreateNewVersion(int nextVersion)
    {
        return new ProjectBrief
        {
            ProjectId = ProjectId,
            Version = nextVersion,
            Status = "DRAFT",
            Content = Content,
        };
    }
}
