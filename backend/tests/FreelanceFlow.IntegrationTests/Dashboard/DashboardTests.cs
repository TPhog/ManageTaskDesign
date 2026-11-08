using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Dashboard;

/// <summary>
/// BR-90 / DSH-01..04 on a host whose IClock says today = 2030-05-15 (VN). Every test reads one scenario seeded
/// straight into this class's own database, so the expected numbers are absolute.
/// </summary>
public class DashboardTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private static readonly DateOnly Today = new(2030, 5, 15);
    private const string SeedMarker = "DSH Seed Client";

    private static DateOnly D(int month, int day, int year = 2030) => new(year, month, day);

    [Fact]
    public async Task Requires_a_designer_session()
    {
        var resp = await Client.GetAsync("/api/admin/dashboard");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Action_required_counts_new_requests_due_tasks_and_sent_quotations()   // DSH-01, BR-81/82, BR-90
    {
        var client = await FixedClockClientAsync();
        var a = (await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard")).GetProperty("actionRequired");

        a.GetProperty("newRequests").GetInt32().Should().Be(3);
        a.GetProperty("overdueTasks").GetInt32().Should().Be(2);           // DONE and CANCELLED-project tasks skipped
        a.GetProperty("dueTodayTasks").GetInt32().Should().Be(2);
        a.GetProperty("awaitingQuotations").GetInt32().Should().Be(3);     // the SENT one past expiry is EXPIRED
        a.GetProperty("expiringSoonQuotations").GetInt32().Should().Be(2); // expiry in [today, today+3]
    }

    [Fact]
    public async Task Projects_block_counts_statuses_and_lists_deadlines_within_seven_days()   // DSH-02
    {
        var client = await FixedClockClientAsync();
        var p = (await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard")).GetProperty("projects");

        p.GetProperty("inProgress").GetInt32().Should().Be(4);
        p.GetProperty("onHold").GetInt32().Should().Be(1);
        var due = p.GetProperty("dueIn7Days").EnumerateArray().ToList();
        due.Select(x => x.GetProperty("name").GetString()).Should().Equal("DSH Poster", "DSH Logo", "DSH Banner");
        due.Select(x => x.GetProperty("dueDate").GetString()).Should().Equal("2030-05-16", "2030-05-18", "2030-05-22");
        due[0].GetProperty("clientName").GetString().Should().Be(SeedMarker);
        due[0].GetProperty("status").GetString().Should().Be("ON_HOLD");
    }

    [Fact]
    public async Task Money_block_follows_br90_and_the_outstanding_query()   // DSH-03, BR-73, docs/04 §4.2
    {
        var client = await FixedClockClientAsync();
        var m = (await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard")).GetProperty("money");

        m.GetProperty("outstanding").GetInt64().Should().Be(6_300_000);          // 10M − 5M deposit + 1.3M; cancelled project excluded
        m.GetProperty("acceptedThisMonth").GetInt64().Should().Be(13_300_000);   // 10M + 1.3M + 2M (BR-90 has no project filter)
        m.GetProperty("additionalThisMonth").GetInt64().Should().Be(1_300_000);
    }

    [Fact]
    public async Task Chart_lists_six_months_oldest_first_with_zero_months()   // DSH-04
    {
        var client = await FixedClockClientAsync();
        var months = (await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard"))
            .GetProperty("acceptedByMonth").EnumerateArray().ToList();

        months.Select(x => x.GetProperty("month").GetString())
            .Should().Equal("2029-12", "2030-01", "2030-02", "2030-03", "2030-04", "2030-05");
        months.Select(x => x.GetProperty("total").GetInt64())
            .Should().Equal(3_000_000, 0, 0, 0, 8_000_000, 13_300_000);
    }

    [Fact]
    public async Task Numbers_match_the_filtered_lists_the_tiles_link_to()   // docs/08 §2.2 "Click từng chỉ số → danh sách đã lọc"
    {
        var client = await FixedClockClientAsync();
        var d = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
        async Task<int> Total(string url) => (await client.GetFromJsonAsync<JsonElement>(url)).GetProperty("total").GetInt32();

        (await client.GetFromJsonAsync<JsonElement>("/api/admin/requests/new-count")).GetProperty("count").GetInt32()
            .Should().Be(d.GetProperty("actionRequired").GetProperty("newRequests").GetInt32());
        (await Total("/api/admin/tasks?due=overdue")).Should().Be(d.GetProperty("actionRequired").GetProperty("overdueTasks").GetInt32());
        (await Total("/api/admin/tasks?due=today")).Should().Be(d.GetProperty("actionRequired").GetProperty("dueTodayTasks").GetInt32());
        (await Total("/api/admin/quotations?effectiveStatus=SENT")).Should().Be(d.GetProperty("actionRequired").GetProperty("awaitingQuotations").GetInt32());
        (await Total("/api/admin/projects?status=IN_PROGRESS")).Should().Be(d.GetProperty("projects").GetProperty("inProgress").GetInt32());
    }

    // ---------- helpers ----------

    /// <summary>Client of a derived host with today = 2030-05-15, logged in (GET only, so no CSRF headers); seeds once.</summary>
    private async Task<HttpClient> FixedClockClientAsync()
    {
        var host = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<IClock>(new FixedClock(Today))));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = AppFactory.TestUsername, password = AppFactory.TestPassword });
        resp.EnsureSuccessStatusCode();
        var cookies = resp.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToList();
        var admin = cookies.First(c => c.StartsWith("ff_admin=", StringComparison.Ordinal));
        var csrf = cookies.First(c => c.StartsWith("ff_csrf=", StringComparison.Ordinal));
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{admin}; {csrf}");
        await SeedAsync(host.Services);
        return client;
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Clients.AnyAsync(c => c.Name == SeedMarker)) return;

        var client = new Client { Name = SeedMarker };
        Project NewProject(string name, string status, DateOnly? due) =>
            new() { ClientId = client.Id, Name = name, DesignType = "LOGO", Status = status, DueDate = due };
        var p1 = NewProject("DSH Logo", "IN_PROGRESS", D(5, 18));
        var p2 = NewProject("DSH Banner", "IN_PROGRESS", D(5, 22));    // today + 7: inside
        var p3 = NewProject("DSH Poster", "ON_HOLD", D(5, 16));
        var p4 = NewProject("DSH Website", "IN_PROGRESS", D(5, 23));   // today + 8: outside
        var p5 = NewProject("DSH Done", "COMPLETED", D(5, 17));
        var p6 = NewProject("DSH Cancelled", "CANCELLED", D(5, 17));
        var p7 = NewProject("DSH Late", "IN_PROGRESS", D(5, 14));      // overdue: not "trong 7 ngày"

        ProjectTask NewTask(Project p, string status, DateOnly due) => new()
        {
            ProjectId = p.Id, Title = $"DSH task {status} {due:yyyy-MM-dd}", Status = status, DueDate = due,
            SortOrder = ProjectTask.SortStep,
        };
        ClientRequest NewRequest(Project p, string status) => new()
        {
            ProjectId = p.Id, Content = "DSH yêu cầu", Source = RequestSources.Zalo, CreatedBy = "DESIGNER", Status = status,
            DeclineReason = status == RequestStatuses.Declined ? "Ngoài phạm vi" : null,
        };

        var seq = 0;
        Quotation NewQuotation(Project p, string kind, string status, long total) => new()
        {
            ProjectId = p.Id, Kind = kind, Title = $"DSH {kind} {status}",
            Number = status == QuotationStatuses.Draft ? null : Quotation.FormatNumber("BG", 2030, ++seq),
            Status = status, ValidityDays = 7, Subtotal = total, Total = total,
        };
        static Quotation AsSent(Quotation q, DateOnly expiry)
        {
            q.IssueDate = expiry.AddDays(-7);
            q.ExpiryDate = expiry;
            return q;
        }
        static Quotation AsAccepted(Quotation q, DateOnly acceptedAt, string payment, int depositPercent = 0)
        {
            q.AcceptedAt = acceptedAt;
            q.PaymentStatus = payment;
            q.DepositPercent = depositPercent;
            q.DepositAmount = Money.Percent(q.Total, depositPercent);
            if (payment == PaymentStatuses.DepositPaid) q.DepositPaidAt = acceptedAt;
            if (payment == PaymentStatuses.Paid) q.PaidAt = acceptedAt;
            return q;
        }
        static Quotation AsRejected(Quotation q)
        {
            q.RejectedAt = D(5, 2);
            q.RejectedReason = "Khách chọn bên khác";
            return q;
        }

        const string initial = QuotationKinds.Initial;
        const string additional = QuotationKinds.Additional;
        db.Clients.Add(client);
        db.Projects.AddRange(p1, p2, p3, p4, p5, p6, p7);
        db.Tasks.AddRange(
            NewTask(p1, "TODO", D(5, 14)),          // overdue
            NewTask(p1, "IN_PROGRESS", D(5, 1)),    // overdue
            NewTask(p1, "DONE", D(5, 10)),          // DONE never counts
            NewTask(p1, "TODO", D(5, 15)),          // today
            NewTask(p2, "REVIEW", D(5, 15)),        // today
            NewTask(p1, "TODO", D(5, 16)),          // tomorrow
            NewTask(p6, "TODO", D(5, 10)),          // BR-82: cancelled project
            NewTask(p6, "TODO", D(5, 15)));
        db.ClientRequests.AddRange(
            NewRequest(p1, RequestStatuses.New),
            NewRequest(p1, RequestStatuses.New),
            NewRequest(p2, RequestStatuses.New),
            NewRequest(p1, RequestStatuses.Triaged),
            NewRequest(p2, RequestStatuses.Declined));
        db.Quotations.AddRange(
            AsSent(NewQuotation(p1, initial, QuotationStatuses.Sent, 4_000_000), D(5, 17)),      // awaiting + expiring
            AsSent(NewQuotation(p2, initial, QuotationStatuses.Sent, 2_000_000), D(5, 15)),      // expires today: still SENT
            AsSent(NewQuotation(p1, additional, QuotationStatuses.Sent, 600_000), D(5, 25)),     // awaiting only
            AsSent(NewQuotation(p4, initial, QuotationStatuses.Sent, 900_000), D(5, 14)),        // EXPIRED on read
            NewQuotation(p2, initial, QuotationStatuses.Draft, 700_000),
            AsAccepted(NewQuotation(p1, initial, QuotationStatuses.Accepted, 10_000_000), D(5, 3), PaymentStatuses.DepositPaid, 50),
            AsAccepted(NewQuotation(p1, additional, QuotationStatuses.Accepted, 1_300_000), D(5, 10), PaymentStatuses.Unpaid),
            AsAccepted(NewQuotation(p2, initial, QuotationStatuses.Accepted, 8_000_000), D(4, 20), PaymentStatuses.Paid),
            AsAccepted(NewQuotation(p6, initial, QuotationStatuses.Accepted, 2_000_000), D(5, 5), PaymentStatuses.Unpaid),   // cancelled project
            AsAccepted(NewQuotation(p2, initial, QuotationStatuses.Accepted, 3_000_000), D(12, 1, 2029), PaymentStatuses.Paid),
            AsAccepted(NewQuotation(p1, initial, QuotationStatuses.Accepted, 999), D(11, 30, 2029), PaymentStatuses.Paid),   // before the chart
            AsAccepted(NewQuotation(p1, initial, QuotationStatuses.Accepted, 5), D(6, 2), PaymentStatuses.Paid),             // future date
            AsRejected(NewQuotation(p1, additional, QuotationStatuses.Rejected, 750_000)));
        await db.SaveChangesAsync();
    }

    private sealed class FixedClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow => new(today.ToDateTime(new TimeOnly(5, 0)), TimeSpan.Zero);
        public DateOnly TodayVn => today;
    }
}
