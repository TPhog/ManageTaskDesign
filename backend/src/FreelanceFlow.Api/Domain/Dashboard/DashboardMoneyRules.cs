namespace FreelanceFlow.Api.Domain.Dashboard;

public sealed record PaymentRow(DateOnly? DepositPaidAt, DateOnly? PaidAt, long DepositAmount, long Total);

public sealed record CollectedAmount(long Deposits, long Remainders)
{
    public long Total => Deposits + Remainders;
}

/// <summary>P11 BR-93: money collected per period, receivables aging. Money is long VND; percentages are display only.</summary>
public static class DashboardMoneyRules
{
    /// <summary>Deposit on <c>deposit_paid_at</c>, the remainder (total − deposit) on <c>paid_at</c>; ACCEPTED quotations only (caller filters).</summary>
    public static CollectedAmount Collected(IEnumerable<PaymentRow> rows, DateWindow window)
    {
        long deposits = 0, remainders = 0;
        foreach (var r in rows)
        {
            if (r.DepositPaidAt is { } d && window.Contains(d)) deposits += r.DepositAmount;
            if (r.PaidAt is { } p && window.Contains(p)) remainders += r.Total - r.DepositAmount;
        }
        return new CollectedAmount(deposits, remainders);
    }

    public static double? DeltaPercent(long current, long previous) =>
        previous == 0 ? null : Math.Round((current - previous) * 100.0 / previous, 1, MidpointRounding.AwayFromZero);

    /// <summary>BR-73 for an ACCEPTED quotation (same as Quotation.OutstandingAmount / ProjectFlow.Outstanding).</summary>
    public static long Outstanding(string paymentStatus, long total, long depositAmount) => paymentStatus switch
    {
        "PAID" => 0,
        "DEPOSIT_PAID" => total - depositAmount,
        _ => total,
    };

    public static DateOnly AgingFrom(string paymentStatus, DateOnly acceptedAt, DateOnly? depositPaidAt) =>
        paymentStatus == "DEPOSIT_PAID" ? depositPaidAt ?? acceptedAt : acceptedAt;

    public static int AgingDays(DateOnly from, DateOnly today) => Math.Max(0, today.DayNumber - from.DayNumber);

    public static string AgingBucket(int days) => days <= 7 ? "0-7" : days <= 30 ? "8-30" : "31+";
}
