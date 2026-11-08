using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Dashboard;
using FreelanceFlow.Api.Domain.Quotations;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Dashboard;

/// <summary>P11 BR-93: money collected / accepted per period vs the previous period to date, plus receivables with aging.</summary>
internal static class MoneyInsightsEndpoints
{
    private const int MaxReceivableItems = 20;
    private const int MaxReceivableClients = 10;

    public static void MapMoneyInsightsEndpoints(this IEndpointRouteBuilder admin) => admin.MapGet("dashboard/money", Get);

    private static async Task<IResult> Get(string? period, AppDbContext db, IClock clock, CancellationToken ct)
    {
        if (!DashboardPeriods.TryParse(period, out var parsed))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["period"] = ["month | quarter | year"] });

        var today = clock.TodayVn;
        var w = DashboardPeriods.Resolve(parsed, today);
        var from = w.Previous.Start;

        var payments = await db.Quotations.AsNoTracking()
            .Where(q => q.Status == QuotationStatuses.Accepted && (q.DepositPaidAt >= from || q.PaidAt >= from))
            .Select(q => new PaymentRow(q.DepositPaidAt, q.PaidAt, q.DepositAmount, q.Total))
            .ToListAsync(ct);
        var collectedNow = DashboardMoneyRules.Collected(payments, w.Current);
        var collectedPrev = DashboardMoneyRules.Collected(payments, w.Previous);

        var accepted = await db.Quotations.AsNoTracking()
            .Where(q => q.Status == QuotationStatuses.Accepted && q.AcceptedAt >= from)
            .Select(q => new { At = q.AcceptedAt!.Value, q.Total })
            .ToListAsync(ct);
        var acceptedNow = accepted.Where(a => w.Current.Contains(a.At)).Sum(a => a.Total);
        var acceptedPrev = accepted.Where(a => w.Previous.Contains(a.At)).Sum(a => a.Total);

        // same filter as GET /api/admin/dashboard money.outstanding (BR-73, project not CANCELLED)
        var rows = await (
                from q in db.Quotations.AsNoTracking()
                join p in db.Projects.AsNoTracking() on q.ProjectId equals p.Id
                join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
                where q.Status == QuotationStatuses.Accepted && q.PaymentStatus != PaymentStatuses.Paid && p.Status != "CANCELLED"
                select new
                {
                    q.Id, q.Number, q.Version, q.Kind, ClientId = c.Id, ClientName = c.Name, ProjectId = p.Id, ProjectName = p.Name,
                    q.PaymentStatus, q.Total, q.DepositPercent, q.DepositAmount, AcceptedAt = q.AcceptedAt!.Value, q.DepositPaidAt,
                })
            .ToListAsync(ct);

        var all = rows.Select(r =>
            {
                var outstanding = DashboardMoneyRules.Outstanding(r.PaymentStatus, r.Total, r.DepositAmount);
                var agingFrom = DashboardMoneyRules.AgingFrom(r.PaymentStatus, r.AcceptedAt, r.DepositPaidAt);
                var days = DashboardMoneyRules.AgingDays(agingFrom, today);
                return new ReceivableItemDto(r.Id, r.Number, r.Version, r.Kind, r.ClientId, r.ClientName, r.ProjectId, r.ProjectName,
                    r.PaymentStatus, r.Total, r.DepositPercent, r.DepositAmount, outstanding, agingFrom, days,
                    DashboardMoneyRules.AgingBucket(days));
            })
            .OrderByDescending(i => i.AgingDays).ThenBy(i => i.Number, StringComparer.Ordinal)
            .ToList();

        var byClient = all.GroupBy(i => new { i.ClientId, i.ClientName })
            .Select(g => new ReceivableClientDto(g.Key.ClientId, g.Key.ClientName, g.Sum(i => i.Outstanding), g.Count()))
            .OrderByDescending(c => c.Outstanding).ThenBy(c => c.ClientName, StringComparer.Ordinal)
            .Take(MaxReceivableClients)
            .ToList();

        return Results.Ok(new MoneyInsightsDto(
            DashboardPeriods.Key(parsed), w.PeriodStart, w.PeriodEnd,
            new DateWindowDto(w.Current.Start, w.Current.EndExclusive),
            new DateWindowDto(w.Previous.Start, w.Previous.EndExclusive),
            new CollectedDto(collectedNow.Total, collectedPrev.Total, DashboardMoneyRules.DeltaPercent(collectedNow.Total, collectedPrev.Total),
                collectedNow.Deposits, collectedNow.Remainders),
            new AcceptedCompareDto(acceptedNow, acceptedPrev, DashboardMoneyRules.DeltaPercent(acceptedNow, acceptedPrev)),
            new ReceivablesDto(all.Sum(i => i.Outstanding), all.Take(MaxReceivableItems).ToList(), byClient,
                Math.Max(0, all.Count - MaxReceivableItems))));
    }
}
