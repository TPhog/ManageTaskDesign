namespace FreelanceFlow.Api.Domain.Projects;

/// <summary>
/// Plan 12 BR-48: the single state the client sees for a deliverable. An open change request (a NEW request tied to it,
/// or a TODO/IN_PROGRESS revision item in one of its rounds) wins over everything while the work is not done.
/// </summary>
public static class ClientReviewStates
{
    public const string None = "NONE";
    public const string AwaitingClient = "AWAITING_CLIENT";
    public const string ClientApproved = "CLIENT_APPROVED";
    public const string ChangesInProgress = "CHANGES_IN_PROGRESS";

    public static string Of(string status, string? clientDecision, bool hasOpenChange) =>
        hasOpenChange && (status is "IN_PROGRESS" or "IN_REVIEW") ? ChangesInProgress
        : status != "IN_REVIEW" ? None
        : clientDecision == ClientDecisions.Approved ? ClientApproved
        : AwaitingClient;
}
