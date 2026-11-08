namespace FreelanceFlow.Api.Domain.Dashboard;

public sealed record DigestTask(string Title, string ProjectName, DateOnly DueDate);
public sealed record DigestDeadline(string Name, DateOnly Date);
public sealed record DigestQuotation(string? Number, DateOnly Expiry);

/// <summary>P11 BR-94: what the morning digest reports. Receivables are context only — they never trigger a message alone.</summary>
public sealed record DigestFacts(
    int OverdueCount, IReadOnlyList<DigestTask> Overdue,
    int TodayCount, IReadOnlyList<DigestTask> Today,
    int TomorrowCount, IReadOnlyList<DigestTask> Tomorrow,
    IReadOnlyList<DigestDeadline> ProjectDeadlines,
    IReadOnlyList<DigestQuotation> ExpiringQuotations,
    long ReceivablesTotal)
{
    public bool HasContent =>
        OverdueCount + TodayCount + TomorrowCount > 0 || ProjectDeadlines.Count > 0 || ExpiringQuotations.Count > 0;
}
