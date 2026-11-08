using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FreelanceFlow.IntegrationTests.Dashboard.DashboardTestHost;

namespace FreelanceFlow.IntegrationTests.Dashboard;

/// <summary>P11 BR-92 on a host whose IClock says today = 2030-05-15 (VN).</summary>
public class DashboardProjectHealthTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private const string SeedMarker = "PHL Seed Client";

    [Fact]
    public async Task Requires_a_designer_session() =>
        (await Client.GetAsync("/api/admin/dashboard/project-health")).StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Rows_are_ranked_by_risk_then_deadline_and_carry_the_flow_facts()
    {
        var h = await GetAsync();
        var items = h.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("projectName").GetString()).Take(4)
            .Should().Equal("PHL Late", "PHL Deposit", "PHL Done", "PHL Ok");

        var late = items[0];
        late.GetProperty("risks").EnumerateArray().Select(r => r.GetString()).Should().BeEquivalentTo("LATE", "TASKS_OVERDUE");
        late.GetProperty("riskScore").GetInt32().Should().Be(6);
        late.GetProperty("progressPercent").GetInt32().Should().Be(50);
        late.GetProperty("daysToDeadline").GetInt32().Should().Be(-5);
        late.GetProperty("quotaTotal").GetInt32().Should().Be(2);
        late.GetProperty("quotaUsed").GetInt32().Should().Be(0);

        items[1].GetProperty("risks").EnumerateArray().Select(r => r.GetString()).Should().Equal("DEPOSIT_UNPAID");
        items[2].GetProperty("risks").EnumerateArray().Select(r => r.GetString()).Should().Equal("BALANCE_DUE");
        items[2].GetProperty("status").GetString().Should().Be("COMPLETED");
        items[3].GetProperty("risks").GetArrayLength().Should().Be(0);
        items[3].GetProperty("stage").GetString().Should().Be("EXECUTE");
    }

    [Fact]
    public async Task Paid_completed_cancelled_and_draft_projects_are_not_listed()
    {
        var h = await GetAsync();
        var names = h.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("projectName").GetString()).ToList();
        names.Should().NotContain(["PHL Done Paid", "PHL Cancelled", "PHL Draft"]);
    }

    [Fact]
    public async Task Returns_twelve_rows_and_reports_how_many_are_hidden()
    {
        var h = await GetAsync();
        h.GetProperty("items").GetArrayLength().Should().Be(12);
        h.GetProperty("total").GetInt32().Should().Be(18);   // 4 scenario projects + 14 filler
        h.GetProperty("hidden").GetInt32().Should().Be(6);
    }

    private async Task<JsonElement> GetAsync()
    {
        var (client, _) = await CreateAsync(Factory, SeedAsync);
        var resp = await client.GetAsync("/api/admin/dashboard/project-health");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Clients.AnyAsync(c => c.Name == SeedMarker)) return;

        var client = new Client { Name = SeedMarker };
        Project P(string name, string status, DateOnly? due) =>
            new() { ClientId = client.Id, Name = name, DesignType = "LOGO", Status = status, DueDate = due };
        var late = P("PHL Late", "IN_PROGRESS", D(5, 10));
        var deposit = P("PHL Deposit", "IN_PROGRESS", D(5, 30));
        var ok = P("PHL Ok", "IN_PROGRESS", D(6, 30));
        var done = P("PHL Done", "COMPLETED", null);
        var donePaid = P("PHL Done Paid", "COMPLETED", null);
        var cancelled = P("PHL Cancelled", "CANCELLED", D(5, 1));
        var draft = P("PHL Draft", "DRAFT", D(5, 1));

        var seq = 0;
        Quotation Accepted(Project p, string payment, int depositPercent)
        {
            var q = new Quotation
            {
                ProjectId = p.Id, Kind = QuotationKinds.Initial, Title = "PHL quote",
                Number = Quotation.FormatNumber("BG", 2030, ++seq), Status = QuotationStatuses.Accepted, ValidityDays = 7,
                Subtotal = 10_000_000, Total = 10_000_000, AcceptedAt = D(5, 1), PaymentStatus = payment, DepositPercent = depositPercent,
            };
            q.DepositAmount = Money.Percent(q.Total, depositPercent);
            if (payment != PaymentStatuses.Unpaid) q.DepositPaidAt = D(5, 2);
            if (payment == PaymentStatuses.Paid) q.PaidAt = D(5, 3);
            return q;
        }

        static Deliverable RunningDeliverable(Project p) =>
            new() { ProjectId = p.Id, Name = "Logo", Status = "IN_PROGRESS", IncludedRounds = 2 };
        static ProjectBrief ConfirmedBrief(Project p) =>
            new() { ProjectId = p.Id, Version = 1, Status = "CONFIRMED", Content = "Brief", ConfirmedAt = DateTimeOffset.UnixEpoch };
        var lateDel = new Deliverable { ProjectId = late.Id, Name = "Logo", Status = "IN_PROGRESS", IncludedRounds = 2 };
        db.Clients.Add(client);
        db.Projects.AddRange(late, deposit, ok, done, donePaid, cancelled, draft);
        db.Deliverables.AddRange(lateDel, RunningDeliverable(deposit), RunningDeliverable(ok));
        // a confirmed brief + an ACCEPTED initial quotation put a project into the EXECUTE stage (where payment warnings apply)
        db.ProjectBriefs.AddRange(ConfirmedBrief(deposit), ConfirmedBrief(ok));
        db.Tasks.AddRange(
            new ProjectTask { ProjectId = late.Id, DeliverableId = lateDel.Id, Title = "PHL a", Status = "DONE", DueDate = D(5, 9), SortOrder = ProjectTask.SortStep },
            new ProjectTask { ProjectId = late.Id, DeliverableId = lateDel.Id, Title = "PHL b", Status = "TODO", DueDate = D(5, 12), SortOrder = ProjectTask.SortStep });
        db.Quotations.AddRange(
            Accepted(deposit, PaymentStatuses.Unpaid, 50),
            Accepted(ok, PaymentStatuses.Paid, 50),
            Accepted(done, PaymentStatuses.DepositPaid, 50),
            Accepted(donePaid, PaymentStatuses.Paid, 50));
        for (var i = 0; i < 14; i++) db.Projects.Add(P($"PHL Filler {i:00}", "IN_PROGRESS", null));
        await db.SaveChangesAsync();
    }
}
