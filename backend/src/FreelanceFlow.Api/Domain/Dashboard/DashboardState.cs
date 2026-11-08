namespace FreelanceFlow.Api.Domain.Dashboard;

/// <summary>P11 singleton (id = 1): runtime state for the digest and the first-run checklist, kept out of settings so background writes never bump settings.xmin.</summary>
public class DashboardState
{
    public short Id { get; set; } = 1;
    public DateOnly? DigestLastRunDate { get; set; }
    public DateTimeOffset? SetupDismissedAt { get; set; }
}
