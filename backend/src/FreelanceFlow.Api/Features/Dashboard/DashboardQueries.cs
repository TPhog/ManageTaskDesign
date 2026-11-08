using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Quotations;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Dashboard;

/// <summary>Queries shared by the dashboard and the daily digest so both always report the same number.</summary>
internal static class DashboardQueries
{
    /// <summary>docs/04 §4.2 / BR-73: outstanding amount of ACCEPTED quotations whose project is not CANCELLED, summed in SQL.</summary>
    public static Task<long> OutstandingTotalAsync(AppDbContext db, CancellationToken ct = default) =>
        (from q in db.Quotations.AsNoTracking()
         join p in db.Projects.AsNoTracking() on q.ProjectId equals p.Id
         where q.Status == QuotationStatuses.Accepted && p.Status != "CANCELLED"
         select q.PaymentStatus == PaymentStatuses.Paid ? 0L
             : q.PaymentStatus == PaymentStatuses.DepositPaid ? q.Total - q.DepositAmount
             : q.Total)
        .SumAsync(ct);
}
