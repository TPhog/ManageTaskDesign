using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Tasks;

namespace FreelanceFlow.UnitTests.Domain;

public class TaskLarkUpgradesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero); // Sunday 2026-11-08
    private static readonly IClock Clock = new LarkFakeClock(Now);
    private static DateOnly D(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd");

    private static ProjectTask NewTask(string status = "TODO", DateOnly? start = null, DateOnly? due = null) =>
        ProjectTask.Create(Guid.NewGuid(), "Báo cáo tháng", "mô tả", status, "HIGH", start, due, null, 1024, Clock);

    // ---- ExternalLinks ----
    [Fact]
    public void Links_accept_http_and_https_and_fill_empty_title_with_host()
    {
        var links = ExternalLinks.Normalize([new ExternalLink(" ", "https://www.figma.com/file/abc"), new ExternalLink("Drive", "http://drive.google.com/x")], false);
        Assert.Equal("www.figma.com", links[0].Title);
        Assert.Equal("Drive", links[1].Title);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://x.com/a")]
    [InlineData("//x.com")]
    [InlineData("not a url")]
    [InlineData("https://user:pw@x.com/a")]
    public void Links_reject_non_http_or_userinfo(string url)
    {
        var ex = Assert.Throws<DomainException>(() => ExternalLinks.Normalize([new ExternalLink("x", url)], false));
        Assert.Equal("VALIDATION_FAILED", ex.Code);
    }

    [Fact]
    public void Links_reject_more_than_ten()
    {
        var many = Enumerable.Range(0, 11).Select(i => new ExternalLink("l" + i, $"https://x.com/{i}"));
        Assert.Throws<DomainException>(() => ExternalLinks.Normalize(many, false));
    }

    [Fact]
    public void Task_links_never_visible_to_client_but_deliverable_links_keep_the_flag()
    {
        var task = NewTask();
        task.SetLinks([new ExternalLink("a", "https://x.com/a", true)]);
        Assert.False(task.Links()[0].VisibleToClient);

        var d = new Deliverable();
        d.SetLinks([new ExternalLink("a", "https://x.com/a", true), new ExternalLink("b", "https://x.com/b", false)]);
        Assert.True(d.Links()[0].VisibleToClient);
        Assert.False(d.Links()[1].VisibleToClient);
    }

    // ---- RecurrenceRule.NextAfter ----
    [Fact] public void Daily_is_next_day() =>
        Assert.Equal(D("2026-11-09"), RecurrenceRule.Create("DAILY", 0, null, null).NextAfter(D("2026-11-08")));

    [Fact]
    public void Weekly_picks_next_selected_weekday_strictly_after_base()
    {
        var monWed = RecurrenceRule.Create("WEEKLY", RecurrenceRule.WeekdayBit(DayOfWeek.Monday) | RecurrenceRule.WeekdayBit(DayOfWeek.Wednesday), null, null);
        Assert.Equal(D("2026-11-09"), monWed.NextAfter(D("2026-11-06"))); // Fri -> Mon
        Assert.Equal(D("2026-11-11"), monWed.NextAfter(D("2026-11-09"))); // Mon -> Wed
        Assert.Equal(D("2026-11-16"), monWed.NextAfter(D("2026-11-11"))); // Wed -> next Mon
    }

    [Theory]
    [InlineData("2026-01-31", 31, "2026-02-28")] // clamp to last day of Feb
    [InlineData("2026-02-28", 31, "2026-03-31")]
    [InlineData("2026-11-20", 15, "2026-12-15")] // day already passed this month
    [InlineData("2026-11-10", 15, "2026-11-15")]
    public void Monthly_uses_day_of_month_and_clamps(string baseIso, int monthDay, string expected) =>
        Assert.Equal(D(expected), RecurrenceRule.Create("MONTHLY", 0, monthDay, null).NextAfter(D(baseIso)));

    [Fact]
    public void Until_stops_the_chain() =>
        Assert.Null(RecurrenceRule.Create("DAILY", 0, null, D("2026-11-08")).NextAfter(D("2026-11-08")));

    [Theory]
    [InlineData("WEEKLY", 0, null)]
    [InlineData("WEEKLY", 128, null)]
    [InlineData("MONTHLY", 0, null)]
    [InlineData("MONTHLY", 0, 32)]
    [InlineData("YEARLY", 0, null)]
    public void Invalid_rules_throw(string kind, int weekdays, int? monthDay) =>
        Assert.Throws<DomainException>(() => RecurrenceRule.Create(kind, weekdays, monthDay, null));

    // ---- TrySpawnNext ----
    [Fact]
    public void Spawn_late_completion_starts_from_today_and_copies_template()
    {
        var task = NewTask(due: D("2026-11-05"));
        task.AddChecklistItem("Xuất PDF", "ghi chú");
        task.ChecklistItems[0].Done = true;
        task.SetLinks([new ExternalLink("Figma", "https://figma.com/x")]);
        task.SetRecurrence(RecurrenceRule.Create("DAILY", 0, null, null));
        task.Move("DONE", 1024, Clock);

        var next = task.TrySpawnNext(Clock, 2048)!;

        Assert.Equal(D("2026-11-09"), next.DueDate); // max(due, today)=11-08 -> +1
        Assert.Equal("TODO", next.Status);
        Assert.Equal(task.Title, next.Title);
        Assert.Equal(task.Id, next.RecurrenceSourceId);
        Assert.Equal("DAILY", next.RecurrenceKind);
        Assert.Single(next.ChecklistItems);
        Assert.False(next.ChecklistItems[0].Done);
        Assert.Equal("ghi chú", next.ChecklistItems[0].Description);
        Assert.Equal(next.Id, next.ChecklistItems[0].TaskId);
        Assert.Single(next.Links());
        Assert.True(task.RecurrenceSpawned);
    }

    [Fact]
    public void Spawn_early_completion_counts_from_the_old_due_date_and_keeps_start_offset()
    {
        var mon = RecurrenceRule.WeekdayBit(DayOfWeek.Monday);
        var task = NewTask(start: D("2026-11-18"), due: D("2026-11-20")); // Fri
        task.SetRecurrence(RecurrenceRule.Create("WEEKLY", mon, null, null));
        task.Move("DONE", 1024, Clock);

        var next = task.TrySpawnNext(Clock, 2048)!;

        Assert.Equal(D("2026-11-23"), next.DueDate);
        Assert.Equal(D("2026-11-21"), next.StartDate); // 2 days before due, like the original
    }

    [Fact]
    public void Spawn_happens_once_and_only_when_done()
    {
        var task = NewTask(due: D("2026-11-08"));
        task.SetRecurrence(RecurrenceRule.Create("DAILY", 0, null, null));
        Assert.Null(task.TrySpawnNext(Clock, 1)); // still TODO

        task.Move("DONE", 1, Clock);
        Assert.NotNull(task.TrySpawnNext(Clock, 2));
        task.Move("TODO", 1, Clock);
        task.Move("DONE", 1, Clock);
        Assert.Null(task.TrySpawnNext(Clock, 3)); // already spawned
    }

    [Fact]
    public void Spawn_past_until_ends_the_chain_without_a_new_task()
    {
        var task = NewTask(due: D("2026-11-08"));
        task.SetRecurrence(RecurrenceRule.Create("DAILY", 0, null, D("2026-11-08")));
        task.Move("DONE", 1, Clock);
        Assert.Null(task.TrySpawnNext(Clock, 2));
        Assert.True(task.RecurrenceSpawned);
    }

    // ---- Blocked ----
    [Fact]
    public void Blocked_reason_only_while_blocked_and_cleared_when_leaving()
    {
        var task = NewTask();
        Assert.Throws<DomainException>(() => task.SetBlocked("chờ khách", null));

        task.Move("BLOCKED", 1, Clock);
        var blocker = Guid.NewGuid();
        task.SetBlocked("  chờ khách gửi logo  ", blocker);
        Assert.Equal("chờ khách gửi logo", task.BlockedReason);
        Assert.Equal(blocker, task.BlockedByTaskId);

        task.Move("IN_PROGRESS", 1, Clock);
        Assert.Null(task.BlockedReason);
        Assert.Null(task.BlockedByTaskId);
    }

    [Fact]
    public void Blocked_reason_over_limit_throws()
    {
        var task = NewTask("BLOCKED");
        Assert.Throws<DomainException>(() => task.SetBlocked(new string('x', 501), null));
    }

    // ---- Reschedule ----
    [Fact]
    public void Reschedule_shifts_start_with_due()
    {
        var task = NewTask(start: D("2026-11-10"), due: D("2026-11-12"));
        task.Reschedule(D("2026-11-20"));
        Assert.Equal(D("2026-11-20"), task.DueDate);
        Assert.Equal(D("2026-11-18"), task.StartDate);
    }

    [Fact]
    public void Reschedule_without_due_clamps_start_when_due_would_precede_it()
    {
        var task = NewTask(start: D("2026-11-10"));
        task.Reschedule(D("2026-11-05"));
        Assert.Equal(D("2026-11-05"), task.StartDate);
        Assert.Equal(D("2026-11-05"), task.DueDate);
    }

    // ---- TaskGrouping ----
    private static TaskGroupRow Row(string project, string priority, string status, string? due, double sort = 1, Guid? deliverable = null, string? deliverableName = null) =>
        new(Guid.NewGuid(), Guid.Parse(project == "A" ? "00000000-0000-0000-0000-00000000000a" : "00000000-0000-0000-0000-00000000000b"),
            project, "Khách", deliverable, deliverableName, priority, status, due == null ? null : D(due), sort, Now);

    [Fact]
    public void Group_by_due_buckets_in_order_and_done_goes_last()
    {
        var today = D("2026-11-08"); var weekEnd = D("2026-11-08");
        var rows = new[]
        {
            Row("A", "LOW", "TODO", null),
            Row("A", "LOW", "TODO", "2026-12-01"),
            Row("A", "LOW", "DONE", "2026-11-01"),
            Row("A", "LOW", "TODO", "2026-11-08"),
            Row("A", "LOW", "TODO", "2026-11-01"),
        };
        var (ids, groups) = TaskGrouping.Order("due", rows, today, weekEnd);
        Assert.Equal(["overdue", "today", "later", "none", "done"], groups.Select(g => g.Key));
        Assert.Equal(5, ids.Count);
        Assert.All(groups, g => Assert.Equal(1, g.Count));
    }

    [Fact]
    public void Group_by_priority_orders_urgent_first_and_keeps_inner_order_by_sort()
    {
        var a = Row("A", "LOW", "TODO", null, 2); var b = Row("A", "LOW", "TODO", null, 1); var c = Row("A", "URGENT", "TODO", null, 5);
        var (ids, groups) = TaskGrouping.Order("priority", [a, b, c], D("2026-11-08"), D("2026-11-08"));
        Assert.Equal(["URGENT", "LOW"], groups.Select(g => g.Key));
        Assert.Equal([c.Id, b.Id, a.Id], ids);
    }

    [Fact]
    public void Group_by_deliverable_uses_none_key_and_project_prefix()
    {
        var d = Guid.NewGuid();
        var (_, groups) = TaskGrouping.Order("deliverable", [Row("A", "LOW", "TODO", null, 1, d, "Logo"), Row("A", "LOW", "TODO", null)], D("2026-11-08"), D("2026-11-08"));
        Assert.Equal([d.ToString(), "none"], groups.Select(g => g.Key));
        Assert.Equal("A · Logo", groups[0].Label);
        Assert.Equal("Không gắn hạng mục", groups[1].Label);
    }

    // ---- TaskViewConfig ----
    [Fact]
    public void ViewConfig_validates_enums_and_round_trips()
    {
        var cfg = TaskViewConfig.Validate(new("list", "due", "week", null, null, null, null, null, " logo ", "month"));
        Assert.Equal("logo", cfg.Q);
        Assert.Equal(cfg, TaskViewConfig.Parse(cfg.ToJson()));
        Assert.Throws<DomainException>(() => TaskViewConfig.Validate(new("grid", "none", null, null, null, null, null, null, null, "month")));
        Assert.Throws<DomainException>(() => TaskViewConfig.Validate(new("list", "colour", null, null, null, null, null, null, null, "month")));
        Assert.Throws<DomainException>(() => TaskViewConfig.Validate(null));
    }
}

file class LarkFakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;
    public DateOnly TodayVn => DateOnly.FromDateTime(now.Date);
}
