namespace FreelanceFlow.Api.Domain.Tasks;

/// <summary>Init-only (not positional) so EF can compose member access on the projection — same pattern as DeliverableRoundUsage.</summary>
public sealed record DeliverableTaskCount
{
    public Guid DeliverableId { get; init; }
    public int Total { get; init; }
    public int Done { get; init; }
}

/// <summary>Plan 06b F3: deliverable progress = share of its tasks that are DONE. Tasks are hard-deleted, so deleted ones never count.</summary>
public static class TaskProgress
{
    /// <summary>
    /// The only task-progress query (admin project detail + portal). Filter <paramref name="tasks"/> first (by project);
    /// a deliverable without tasks is absent. Pure LINQ: EF translates it, unit tests run it in memory.
    /// </summary>
    public static IQueryable<DeliverableTaskCount> PerDeliverable(IQueryable<ProjectTask> tasks) =>
        tasks.Where(t => t.DeliverableId != null)
            .GroupBy(t => t.DeliverableId)
            .Select(g => new DeliverableTaskCount
            {
                DeliverableId = g.Key!.Value,
                Total = g.Count(),
                Done = g.Count(t => t.Status == "DONE"),
            });

    /// <summary>Half-up integer percent; null when the deliverable has no task (nothing to measure).</summary>
    public static int? Percent(int total, int done) =>
        total <= 0 ? null : (int)((done * 200L + total) / (total * 2L));
}
