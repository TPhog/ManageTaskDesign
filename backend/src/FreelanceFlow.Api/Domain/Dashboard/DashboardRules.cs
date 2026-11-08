namespace FreelanceFlow.Api.Domain.Dashboard;

/// <summary>One month of the "giá trị đã chốt" chart (DSH-04); <c>Month</c> is "yyyy-MM".</summary>
public sealed record MonthTotal(string Month, long Total);

/// <summary>An ACCEPTED quotation reduced to what BR-90 needs: the date the designer entered (BR-36), kind and total.</summary>
public sealed record AcceptedAmount(DateOnly AcceptedAt, string Kind, long Total);

/// <summary>
/// BR-90 / DSH-01..04 rules. Dates are Vietnam calendar days (<c>IClock.TodayVn</c>); every window includes both ends.
/// </summary>
public static class DashboardRules
{
    /// <summary>BR-90 "Sắp hết hạn": SENT and expiry_date in [today, today + 3].</summary>
    public const int ExpiringSoonDays = 3;

    /// <summary>DSH-02 "deadline trong 7 ngày": due_date in [today, today + 7].</summary>
    public const int UpcomingProjectDays = 7;

    /// <summary>DSH-04 "6 tháng gần nhất", the current month included.</summary>
    public const int ChartMonths = 6;

    /// <summary>The deadline list is a short to-do list, not a report.</summary>
    public const int MaxUpcomingProjects = 10;

    /// <summary>P11 BR-91: the work schedule shows today … today + 6.</summary>
    public const int ScheduleDays = 7;
    public const int MaxScheduleOverdue = 10;
    public const int MaxScheduleTasksPerDay = 20;

    /// <summary>P11 BR-94: project deadlines and quotation expiries reported by the digest: [today, today + 3].</summary>
    public const int DigestDeadlineDays = 3;

    public static DateOnly ScheduleUntil(DateOnly today) => today.AddDays(ScheduleDays - 1);

    public static IReadOnlyList<DateOnly> ScheduleDates(DateOnly today) =>
        Enumerable.Range(0, ScheduleDays).Select(today.AddDays).ToList();

    /// <summary>Vietnam has no DST: UTC+7 all year.</summary>
    public static int VnHour(DateTimeOffset utc) => utc.ToOffset(TimeSpan.FromHours(7)).Hour;

    /// <summary>Projects still being worked on: COMPLETED and CANCELLED have no meaningful deadline.</summary>
    public static readonly string[] OpenProjectStatuses = ["DRAFT", "QUOTED", "IN_PROGRESS", "ON_HOLD"];

    public static DateOnly ExpiringSoonUntil(DateOnly today) => today.AddDays(ExpiringSoonDays);

    public static DateOnly UpcomingUntil(DateOnly today) => today.AddDays(UpcomingProjectDays);

    public static DateOnly MonthStart(DateOnly day) => new(day.Year, day.Month, 1);

    /// <summary>First day of the oldest chart month.</summary>
    public static DateOnly ChartStart(DateOnly today) => MonthStart(today).AddMonths(-(ChartMonths - 1));

    public static string MonthKey(DateOnly day) => $"{day.Year:0000}-{day.Month:00}";

    /// <summary>BR-90: Σ total accepted in the current month; <paramref name="kind"/> = ADDITIONAL for "Doanh thu phát sinh".</summary>
    public static long AcceptedInMonth(DateOnly today, IEnumerable<AcceptedAmount> accepted, string? kind = null)
    {
        var start = MonthStart(today);
        var end = start.AddMonths(1);
        return accepted
            .Where(a => a.AcceptedAt >= start && a.AcceptedAt < end && (kind is null || a.Kind == kind))
            .Sum(a => a.Total);
    }

    /// <summary>DSH-04: one entry per month, oldest first, months without an accepted quotation are 0.</summary>
    public static IReadOnlyList<MonthTotal> AcceptedByMonth(DateOnly today, IEnumerable<AcceptedAmount> accepted)
    {
        var start = ChartStart(today);
        var end = MonthStart(today).AddMonths(1);
        var sums = accepted
            .Where(a => a.AcceptedAt >= start && a.AcceptedAt < end)
            .GroupBy(a => MonthKey(a.AcceptedAt))
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Total));
        return Enumerable.Range(0, ChartMonths)
            .Select(i => MonthKey(start.AddMonths(i)))
            .Select(month => new MonthTotal(month, sums.GetValueOrDefault(month)))
            .ToList();
    }
}
