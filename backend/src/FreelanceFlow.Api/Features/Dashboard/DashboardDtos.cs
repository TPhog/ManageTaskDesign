using FreelanceFlow.Api.Domain.Dashboard;

namespace FreelanceFlow.Api.Features.Dashboard;

/// <summary>docs/05 §3.12. <c>dueIn7Days[].status</c> is an additive field for the badge.</summary>
public sealed record DashboardDto(
    DashboardActionRequiredDto ActionRequired,
    DashboardProjectsDto Projects,
    DashboardMoneyDto Money,
    IReadOnlyList<MonthTotal> AcceptedByMonth);

public sealed record DashboardActionRequiredDto(
    int NewRequests, int OverdueTasks, int DueTodayTasks, int AwaitingQuotations, int ExpiringSoonQuotations);

public sealed record DashboardProjectsDto(int InProgress, int OnHold, IReadOnlyList<DashboardDueProjectDto> DueIn7Days);

public sealed record DashboardDueProjectDto(Guid Id, string Name, string ClientName, DateOnly DueDate, string Status);

public sealed record DashboardMoneyDto(long Outstanding, long AcceptedThisMonth, long AdditionalThisMonth);

// ---- Plan 11 ----

public sealed record ScheduleTaskDto(Guid Id, string Title, string Status, string Priority, DateOnly DueDate, double SortOrder,
    Guid ProjectId, string ProjectName, string ClientName);

public sealed record ScheduleDeadlineDto(string Kind, Guid Id, Guid ProjectId, string Name, string ProjectName, string ClientName, DateOnly Date);

public sealed record ScheduleDayDto(DateOnly Date, IReadOnlyList<ScheduleTaskDto> Tasks, int HiddenTasks, IReadOnlyList<ScheduleDeadlineDto> Deadlines);

public sealed record ScheduleOverdueDto(int Total, IReadOnlyList<ScheduleTaskDto> Items);

public sealed record ScheduleDto(DateOnly Today, int UndatedOpenTasks, ScheduleOverdueDto Overdue, IReadOnlyList<ScheduleDayDto> Days);

public sealed record ProjectHealthItemDto(Guid ProjectId, string ProjectName, Guid ClientId, string ClientName, string Status, string Stage,
    DateOnly? DueDate, int? DaysToDeadline, int? ProgressPercent, int QuotaUsed, int QuotaTotal, IReadOnlyList<string> Risks, int RiskScore);

public sealed record ProjectHealthDto(int Total, int Hidden, IReadOnlyList<ProjectHealthItemDto> Items);

public sealed record DateWindowDto(DateOnly Start, DateOnly EndExclusive);

public sealed record CollectedDto(long Current, long Previous, double? DeltaPercent, long CurrentDeposits, long CurrentRemainders);

public sealed record AcceptedCompareDto(long Current, long Previous, double? DeltaPercent);

public sealed record ReceivableItemDto(Guid QuotationId, string? Number, int Version, string Kind, Guid ClientId, string ClientName,
    Guid ProjectId, string ProjectName, string PaymentStatus, long Total, int DepositPercent, long DepositAmount, long Outstanding,
    DateOnly AgingFrom, int AgingDays, string AgingBucket);

public sealed record ReceivableClientDto(Guid ClientId, string ClientName, long Outstanding, int Count);

public sealed record ReceivablesDto(long Total, IReadOnlyList<ReceivableItemDto> Items, IReadOnlyList<ReceivableClientDto> ByClient, int HiddenItems);

public sealed record MoneyInsightsDto(string Period, DateOnly PeriodStart, DateOnly PeriodEnd, DateWindowDto Current, DateWindowDto Previous,
    CollectedDto Collected, AcceptedCompareDto Accepted, ReceivablesDto Receivables);

public sealed record ClientActivityItemDto(long Id, DateTimeOffset OccurredAt, string Kind, string Text, string? ClientName, string? ProjectName, string Href);

public sealed record ClientActivityDto(IReadOnlyList<ClientActivityItemDto> Items);
