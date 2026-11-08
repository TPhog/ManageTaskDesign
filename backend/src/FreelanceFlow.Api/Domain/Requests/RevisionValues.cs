namespace FreelanceFlow.Api.Domain.Requests;

public static class RequestStatuses
{
    public const string New = "NEW";
    public const string Triaged = "TRIAGED";
    public const string Declined = "DECLINED";
    public static readonly string[] All = [New, Triaged, Declined];
}

public static class RequestSources
{
    public const string Portal = "PORTAL";
    public const string Zalo = "ZALO";
    public const string Email = "EMAIL";
    public const string Phone = "PHONE";
    public const string Other = "OTHER";

    /// <summary>REQ-02: channels a designer may record; PORTAL is the client's own channel (ck_requests_portal).</summary>
    public static readonly string[] Designer = [Zalo, Email, Phone, Other];
}

public static class RevisionItemTypes
{
    public const string Minor = "MINOR";
    public const string Major = "MAJOR";
    public const string ScopeChange = "SCOPE_CHANGE";
    public const string NewDeliverable = "NEW_DELIVERABLE";
    public static readonly string[] All = [Minor, Major, ScopeChange, NewDeliverable];
}

public static class RevisionBillings
{
    public const string FreeFix = "FREE_FIX";
    public const string InQuota = "IN_QUOTA";
    public const string Chargeable = "CHARGEABLE";
    public static readonly string[] All = [FreeFix, InQuota, Chargeable];
}

public static class RevisionWorkStatuses
{
    public const string Todo = "TODO";
    public const string InProgress = "IN_PROGRESS";
    public const string Done = "DONE";
    public const string Cancelled = "CANCELLED";
    public static readonly string[] All = [Todo, InProgress, Done, Cancelled];
}
