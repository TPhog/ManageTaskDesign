namespace FreelanceFlow.Api.Domain.Dashboard;

public enum DashboardPeriod { Month, Quarter, Year }

public sealed record DateWindow(DateOnly Start, DateOnly EndExclusive)
{
    public bool Contains(DateOnly d) => d >= Start && d < EndExclusive;
}

public sealed record PeriodWindows(DateWindow Current, DateWindow Previous, DateOnly PeriodStart, DateOnly PeriodEnd);

/// <summary>P11: month/quarter/year windows, "to date", compared with the same elapsed days of the previous period.</summary>
public static class DashboardPeriods
{
    public static bool TryParse(string? value, out DashboardPeriod period)
    {
        period = DashboardPeriod.Month;
        switch (value)
        {
            case null or "month": return true;
            case "quarter": period = DashboardPeriod.Quarter; return true;
            case "year": period = DashboardPeriod.Year; return true;
            default: return false;
        }
    }

    public static string Key(DashboardPeriod p) => p switch
    {
        DashboardPeriod.Quarter => "quarter",
        DashboardPeriod.Year => "year",
        _ => "month",
    };

    public static PeriodWindows Resolve(DashboardPeriod period, DateOnly today)
    {
        var months = period switch { DashboardPeriod.Quarter => 3, DashboardPeriod.Year => 12, _ => 1 };
        var start = period switch
        {
            DashboardPeriod.Quarter => new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1),
            DashboardPeriod.Year => new DateOnly(today.Year, 1, 1),
            _ => new DateOnly(today.Year, today.Month, 1),
        };
        var prevStart = start.AddMonths(-months);
        var elapsedDays = today.DayNumber - start.DayNumber + 1;
        // the previous window can never reach into the current period
        var prevEnd = DateOnly.FromDayNumber(Math.Min(start.DayNumber, prevStart.DayNumber + elapsedDays));
        return new PeriodWindows(
            new DateWindow(start, today.AddDays(1)),
            new DateWindow(prevStart, prevEnd),
            start,
            start.AddMonths(months).AddDays(-1));
    }
}
