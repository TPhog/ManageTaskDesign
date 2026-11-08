using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Tasks;
using FreelanceFlow.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FreelanceFlow.IntegrationTests.Dashboard.DashboardTestHost;

namespace FreelanceFlow.IntegrationTests.Dashboard;

/// <summary>P11 BR-94 gating: enabled flag, VN hour, once per day (also under concurrency). Each test uses its own VN day.</summary>
public class DailyDigestTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private const string SeedMarker = "DGT Seed Client";   // one overdue task due 2030-05-14

    [Fact]
    public async Task Disabled_digest_sends_nothing_and_does_not_claim_the_day()
    {
        var day = new DateOnly(2030, 5, 15);
        var services = await HostAsync(day, enabled: false);
        (await RunAsync(services)).Should().Be(DigestOutcome.Disabled);
        (await LastRunAsync(services)).Should().BeNull();
    }

    [Fact]
    public async Task Before_the_configured_hour_nothing_happens()
    {
        var services = await HostAsync(new DateOnly(2030, 5, 15), hour: 13);   // clock says 12:00 VN
        (await RunAsync(services)).Should().Be(DigestOutcome.TooEarly);
        (await LastRunAsync(services)).Should().BeNull();
    }

    [Fact]
    public async Task Sends_once_per_day_even_when_run_twice()
    {
        var day = new DateOnly(2030, 5, 15);
        var services = await HostAsync(day);
        (await RunAsync(services)).Should().Be(DigestOutcome.Sent);
        (await RunAsync(services)).Should().Be(DigestOutcome.AlreadyRan);
        var sent = await Factory.Notifications.WaitForAsync(n => n.Title == "Nhắc việc ngày 15/05/2030");
        sent.Should().HaveCount(1);
        (await LastRunAsync(services)).Should().Be(day);
    }

    [Fact]
    public async Task Concurrent_runs_on_a_fresh_day_send_exactly_once()
    {
        var services = await HostAsync(new DateOnly(2030, 6, 1));
        var outcomes = await Task.WhenAll(RunAsync(services), RunAsync(services), RunAsync(services));
        outcomes.Count(o => o == DigestOutcome.Sent).Should().Be(1);
        outcomes.Count(o => o == DigestOutcome.AlreadyRan).Should().Be(2);
        var sent = await Factory.Notifications.WaitForAsync(n => n.Title == "Nhắc việc ngày 01/06/2030");
        sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task Nothing_to_report_marks_the_day_but_enqueues_no_message()
    {
        var day = new DateOnly(2020, 1, 10);   // before every seeded date: no overdue, today, tomorrow, deadline or expiry
        var services = await HostAsync(day);
        (await RunAsync(services)).Should().Be(DigestOutcome.NothingToReport);
        (await LastRunAsync(services)).Should().Be(day);
        await Task.Delay(300);
        Factory.Notifications.Sent.Should().NotContain(n => n.Title == "Nhắc việc ngày 10/01/2020");
    }

    [Fact]
    public async Task Next_day_runs_again()
    {
        var first = await HostAsync(new DateOnly(2030, 7, 1));
        (await RunAsync(first)).Should().Be(DigestOutcome.Sent);
        var second = await HostAsync(new DateOnly(2030, 7, 2), resetState: false);
        (await RunAsync(second)).Should().Be(DigestOutcome.Sent);
        (await Factory.Notifications.WaitForAsync(n => n.Title == "Nhắc việc ngày 02/07/2030")).Should().HaveCount(1);
    }

    private async Task<IServiceProvider> HostAsync(DateOnly day, bool enabled = true, int hour = 8, bool resetState = true)
    {
        var (_, services) = await CreateAsync(Factory, SeedAsync, day);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            $"UPDATE settings SET digest_enabled = {(enabled ? "true" : "false")}, digest_hour_vn = {hour}");
        if (resetState) await db.Database.ExecuteSqlRawAsync("UPDATE dashboard_state SET digest_last_run_date = NULL");
        return services;
    }

    private static async Task<DigestOutcome> RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DailyDigestService>().RunAsync();
    }

    private static async Task<DateOnly?> LastRunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.DashboardStates.AsNoTracking().Select(s => s.DigestLastRunDate).FirstAsync();
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Clients.AnyAsync(c => c.Name == SeedMarker)) return;
        var client = new Client { Name = SeedMarker };
        var project = new Project { ClientId = client.Id, Name = "DGT Logo", DesignType = "LOGO", Status = "IN_PROGRESS" };
        db.Clients.Add(client);
        db.Projects.Add(project);
        db.Tasks.Add(new ProjectTask
        {
            ProjectId = project.Id, Title = "DGT overdue", Status = "TODO", DueDate = D(5, 14), SortOrder = ProjectTask.SortStep,
        });
        await db.SaveChangesAsync();
    }
}
