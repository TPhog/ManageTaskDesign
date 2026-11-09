using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Tasks;

/// <summary>
/// P15 D4: when a recurring task goes DONE, the next occurrence is due on the first date matching the rule strictly
/// after the base date (see <see cref="ProjectTask.TrySpawnNext"/>). Weekdays is a mask Mon=1 … Sun=64.
/// </summary>
public sealed record RecurrenceRule(string Kind, int Weekdays, int? MonthDay, DateOnly? Until)
{
    public static readonly string[] Kinds = ["DAILY", "WEEKLY", "MONTHLY"];
    public const int AllWeekdays = 127;

    public static int WeekdayBit(DayOfWeek d) => 1 << (((int)d + 6) % 7);

    public static RecurrenceRule Create(string kind, int weekdays, int? monthDay, DateOnly? until)
    {
        if (!Kinds.Contains(kind))
            throw new DomainException("VALIDATION_FAILED", $"Kiểu lặp không hợp lệ: {kind}");
        return kind switch
        {
            "WEEKLY" when weekdays is < 1 or > AllWeekdays =>
                throw new DomainException("VALIDATION_FAILED", "Chọn ít nhất một thứ trong tuần"),
            "WEEKLY" => new RecurrenceRule(kind, weekdays, null, until),
            "MONTHLY" when monthDay is not (>= 1 and <= 31) =>
                throw new DomainException("VALIDATION_FAILED", "Ngày trong tháng phải từ 1 đến 31"),
            "MONTHLY" => new RecurrenceRule(kind, 0, monthDay, until),
            _ => new RecurrenceRule(kind, 0, null, until),
        };
    }

    /// <summary>First date matching the rule strictly after <paramref name="baseDate"/>; null when it is past Until.</summary>
    public DateOnly? NextAfter(DateOnly baseDate)
    {
        var next = Kind switch
        {
            "DAILY" => baseDate.AddDays(1),
            "WEEKLY" => NextWeekday(baseDate),
            "MONTHLY" => NextMonthDay(baseDate),
            _ => throw new DomainException("VALIDATION_FAILED", $"Kiểu lặp không hợp lệ: {Kind}"),
        };
        return Until is { } until && next > until ? null : next;
    }

    private DateOnly NextWeekday(DateOnly baseDate)
    {
        for (var i = 1; i <= 7; i++)
        {
            var candidate = baseDate.AddDays(i);
            if ((Weekdays & WeekdayBit(candidate.DayOfWeek)) != 0) return candidate;
        }
        throw new DomainException("VALIDATION_FAILED", "Chọn ít nhất một thứ trong tuần");
    }

    // monthDay 29–31 is clamped to the last day of a shorter month
    private DateOnly NextMonthDay(DateOnly baseDate)
    {
        var candidate = InMonth(baseDate.Year, baseDate.Month);
        if (candidate > baseDate) return candidate;
        var nextMonth = baseDate.AddMonths(1);
        return InMonth(nextMonth.Year, nextMonth.Month);
    }

    private DateOnly InMonth(int year, int month) =>
        new(year, month, Math.Min(MonthDay!.Value, DateTime.DaysInMonth(year, month)));
}
