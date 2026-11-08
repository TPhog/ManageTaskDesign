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
