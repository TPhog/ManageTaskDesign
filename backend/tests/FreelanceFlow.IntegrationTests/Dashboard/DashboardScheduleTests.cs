using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FreelanceFlow.IntegrationTests.Dashboard.DashboardTestHost;

namespace FreelanceFlow.IntegrationTests.Dashboard;

/// <summary>P11 BR-91 on a host whose IClock says today = 2030-05-15 (VN).</summary>
public class DashboardScheduleTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private const string SeedMarker = "SCH Seed Client";

    [Fact]
    public async Task Requires_a_designer_session() =>
        (await Client.GetAsync("/api/admin/dashboard/schedule")).StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Days_always_has_seven_entries_starting_today()
    {
        var s = await GetAsync();
        s.GetProperty("today").GetString().Should().Be("2030-05-15");
        s.GetProperty("days").EnumerateArray().Select(d => d.GetProperty("date").GetString())
            .Should().Equal("2030-05-15", "2030-05-16", "2030-05-17", "2030-05-18", "2030-05-19", "2030-05-20", "2030-05-21");
    }

    [Fact]
    public async Task Overdue_bucket_lists_oldest_first_and_skips_done_and_cancelled_project_tasks()
    {
        var o = (await GetAsync()).GetProperty("overdue");
        o.GetProperty("total").GetInt32().Should().Be(2);
        o.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("dueDate").GetString())
            .Should().Equal("2030-05-01", "2030-05-14");
    }

    [Fact]
    public async Task Day_tasks_are_ordered_by_priority_and_capped_at_twenty()
    {
        var days = (await GetAsync()).GetProperty("days").EnumerateArray().ToList();
        days[0].GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("priority").GetString())
            .Should().Equal("URGENT", "LOW");
        days[1].GetProperty("tasks").GetArrayLength().Should().Be(20);
        days[1].GetProperty("hiddenTasks").GetInt32().Should().Be(5);
        days[6].GetProperty("tasks").GetArrayLength().Should().Be(1);   // 05-21 inside, 05-22 outside
    }

    [Fact]
    public async Task Deadlines_include_open_projects_and_undelivered_deliverables_only()
    {
        var s = await GetAsync();
        var d2 = s.GetProperty("days")[2].GetProperty("deadlines").EnumerateArray().ToList();
        d2.Should().ContainSingle(x => x.GetProperty("kind").GetString() == "DELIVERABLE");
        var d3 = s.GetProperty("days")[3].GetProperty("deadlines").EnumerateArray().ToList();
        d3.Should().ContainSingle(x => x.GetProperty("kind").GetString() == "PROJECT");
    }

    [Fact]
    public async Task Undated_open_tasks_are_counted_not_listed() =>
        (await GetAsync()).GetProperty("undatedOpenTasks").GetInt32().Should().Be(1);

    private async Task<JsonElement> GetAsync()
    {
        var (client, _) = await CreateAsync(Factory, SeedAsync);
        var resp = await client.GetAsync("/api/admin/dashboard/schedule");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Clients.AnyAsync(c => c.Name == SeedMarker)) return;

        var client = new Client { Name = SeedMarker };
        var p1 = new Project { ClientId = client.Id, Name = "SCH Logo", DesignType = "LOGO", Status = "IN_PROGRESS", DueDate = D(5, 18) };
        var p2 = new Project { ClientId = client.Id, Name = "SCH Cancelled", DesignType = "LOGO", Status = "CANCELLED", DueDate = D(5, 18) };
        ProjectTask T(Project p, string status, DateOnly? due, string priority = "MEDIUM", double sort = ProjectTask.SortStep, string? title = null) => new()
        {
            ProjectId = p.Id, Title = title ?? $"SCH {status} {due:yyyy-MM-dd}", Status = status, DueDate = due, Priority = priority, SortOrder = sort,
        };
        var del = new Deliverable { ProjectId = p1.Id, Name = "SCH Deliverable", Status = "IN_PROGRESS", DueDate = D(5, 17) };
        var delivered = new Deliverable { ProjectId = p1.Id, Name = "SCH Delivered", Status = "DELIVERED", DueDate = D(5, 17) };

        db.Clients.Add(client);
        db.Projects.AddRange(p1, p2);
        db.Deliverables.AddRange(del, delivered);
        db.Tasks.AddRange(
            T(p1, "TODO", D(5, 14)), T(p1, "TODO", D(5, 1)), T(p1, "DONE", D(5, 10)),
            T(p1, "TODO", D(5, 15), "LOW", 1), T(p1, "TODO", D(5, 15), "URGENT", 2),
            T(p1, "TODO", D(5, 21)), T(p1, "TODO", D(5, 22)),
            T(p1, "TODO", null),
            T(p2, "TODO", D(5, 15)));
        for (var i = 0; i < 25; i++) db.Tasks.Add(T(p1, "TODO", D(5, 16), "MEDIUM", i + 1, $"SCH many {i}"));
        await db.SaveChangesAsync();
    }
}
