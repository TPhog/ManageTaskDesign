using FreelanceFlow.Api.Domain.Flow;

namespace FreelanceFlow.Api.Domain.Dashboard;

public static class HealthRisks
{
    public const string Late = "LATE";
    public const string DepositUnpaid = "DEPOSIT_UNPAID";
    public const string BalanceDue = "BALANCE_DUE";
    public const string OpenRequests = "OPEN_REQUESTS";
    public const string TasksOverdue = "TASKS_OVERDUE";
    public const string QuotaExceeded = "QUOTA_EXCEEDED";

    public static int Weight(string risk) => risk switch
    {
        Late => 4,
        DepositUnpaid or BalanceDue => 3,
        TasksOverdue or OpenRequests => 2,
        _ => 1,
    };
}

public sealed record ProjectHealthResult(
    IReadOnlyList<string> Risks, int Score, int? ProgressPercent, int QuotaUsed, int QuotaTotal, int? DaysToDeadline);

/// <summary>P11 BR-92: one project's health from the same FlowFacts/FlowState the project page uses.</summary>
public static class ProjectHealth
{
    public static ProjectHealthResult Assess(FlowFacts facts, FlowState state, DateOnly? dueDate, DateOnly today)
    {
        var active = facts.Deliverables.Where(d => d.Status != "CANCELLED").ToList();
        var taskTotal = active.Sum(d => d.TaskTotal);
        var taskDone = active.Sum(d => d.TaskDone);
        int? progress = taskTotal == 0 ? null : (taskDone * 100 + taskTotal / 2) / taskTotal;   // half-up

        var risks = new List<string>();
        if (dueDate is { } due && due < today && facts.ProjectStatus != "COMPLETED") risks.Add(HealthRisks.Late);
        if (state.Warnings.Any(w => w.Kind == FlowWarningKinds.DepositUnpaid)) risks.Add(HealthRisks.DepositUnpaid);
        if (state.Warnings.Any(w => w.Kind == FlowWarningKinds.BalanceDue)) risks.Add(HealthRisks.BalanceDue);
        if (facts.NewRequestIds.Count > 0) risks.Add(HealthRisks.OpenRequests);
        if (facts.OverdueTasks > 0) risks.Add(HealthRisks.TasksOverdue);
        if (state.Warnings.Any(w => w.Kind == FlowWarningKinds.QuotaExceeded)) risks.Add(HealthRisks.QuotaExceeded);

        return new ProjectHealthResult(
            risks, risks.Sum(HealthRisks.Weight), progress,
            active.Sum(d => d.UsedRounds), active.Sum(d => d.IncludedRounds),
            dueDate is { } dd ? dd.DayNumber - today.DayNumber : null);
    }
}
