using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Dashboard;
using FreelanceFlow.Api.Domain.Flow;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Features.Projects;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Dashboard;

/// <summary>P11 BR-92: one health row per running project, scored from the same FlowFacts the project page uses.</summary>
internal static class ProjectHealthEndpoints
{
    private const int MaxReturned = 12;

    public static void MapProjectHealthEndpoints(this IEndpointRouteBuilder admin) => admin.MapGet("dashboard/project-health", Get);

    private static async Task<IResult> Get(AppDbContext db, IClock clock, CancellationToken ct)
    {
        var today = clock.TodayVn;

        // running projects, plus COMPLETED ones that still carry an unpaid ACCEPTED quotation (DF-27)
        var candidates =
            from p in db.Projects.AsNoTracking()
            join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
            where p.Status == "IN_PROGRESS" || p.Status == "ON_HOLD" || p.Status == "QUOTED"
                  || (p.Status == "COMPLETED" && db.Quotations.Any(q => q.ProjectId == p.Id
                        && q.Status == QuotationStatuses.Accepted && q.PaymentStatus != PaymentStatuses.Paid))
            select new { p.Id, p.Name, p.Status, p.DueDate, ClientId = c.Id, ClientName = c.Name };

        var total = await candidates.CountAsync(ct);
        // single-user dataset: score every candidate so a risky undated project is never dropped before ranking
        var loaded = await candidates.ToListAsync(ct);

        var facts = await ProjectFlowReads.LoadFactsAsync(db, loaded.Select(x => x.Id).ToList(), today);

        var items = loaded
            .Where(x => facts.ContainsKey(x.Id))
            .Select(x =>
            {
                var f = facts[x.Id];
                var state = ProjectFlow.Evaluate(f, today);
                var h = ProjectHealth.Assess(f, state, x.DueDate, today);
                return new ProjectHealthItemDto(x.Id, x.Name, x.ClientId, x.ClientName, x.Status, state.Stage,
                    x.DueDate, h.DaysToDeadline, h.ProgressPercent, h.QuotaUsed, h.QuotaTotal, h.Risks, h.Score);
            })
            .OrderByDescending(i => i.RiskScore)
            .ThenBy(i => i.DueDate == null).ThenBy(i => i.DueDate)
            .ThenBy(i => i.ProjectName, StringComparer.Ordinal)
            .Take(MaxReturned)
            .ToList();

        return Results.Ok(new ProjectHealthDto(total, total - items.Count, items));
    }
}
