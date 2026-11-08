using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FreelanceFlow.IntegrationTests.Dashboard.DashboardTestHost;

namespace FreelanceFlow.IntegrationTests.Dashboard;

/// <summary>P11 BR-93 on a host whose IClock says today = 2030-05-15 (VN).</summary>
public class DashboardMoneyInsightsTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private const string SeedMarker = "MNY Seed Client";

    [Fact]
    public async Task Requires_a_designer_session() =>
        (await Client.GetAsync("/api/admin/dashboard/money")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Month_collected_splits_deposits_and_remainders_and_compares_with_the_previous_month_to_date()
    {
        var m = await GetAsync("month");
        var c = m.GetProperty("collected");
        c.GetProperty("current").GetInt64().Should().Be(7_000_000);            // A remainder 5M (05-10) + B deposit 2M (05-03)
        c.GetProperty("currentDeposits").GetInt64().Should().Be(2_000_000);
        c.GetProperty("currentRemainders").GetInt64().Should().Be(5_000_000);
        // previous window = 01-15/04: A's deposit (04-28) is outside, only E (PaidAt 04-05, 3M) is inside
        c.GetProperty("previous").GetInt64().Should().Be(3_000_000);
        c.GetProperty("deltaPercent").GetDouble().Should().Be(133.3);
        m.GetProperty("current").GetProperty("endExclusive").GetString().Should().Be("2030-05-16");
        m.GetProperty("previous").GetProperty("endExclusive").GetString().Should().Be("2030-04-16");
    }

    [Fact]
    public async Task Quarter_and_year_use_their_own_windows()
    {
        var q = await GetAsync("quarter");
        q.GetProperty("current").GetProperty("start").GetString().Should().Be("2030-04-01");
        q.GetProperty("previous").GetProperty("start").GetString().Should().Be("2030-01-01");
        q.GetProperty("previous").GetProperty("endExclusive").GetString().Should().Be("2030-02-15");
        q.GetProperty("collected").GetProperty("current").GetInt64().Should().Be(15_000_000);
        q.GetProperty("collected").GetProperty("previous").GetInt64().Should().Be(0);
        q.GetProperty("collected").GetProperty("deltaPercent").ValueKind.Should().Be(JsonValueKind.Null);

        var y = await GetAsync("year");
        y.GetProperty("current").GetProperty("start").GetString().Should().Be("2030-01-01");
        y.GetProperty("previous").GetProperty("start").GetString().Should().Be("2029-01-01");
        y.GetProperty("collected").GetProperty("current").GetInt64().Should().Be(15_000_999);
    }

    [Fact]
    public async Task Unknown_period_is_a_400()
    {
        var (client, _) = await CreateAsync(Factory, SeedAsync);
        (await client.GetAsync("/api/admin/dashboard/money?period=week")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Receivables_match_the_dashboard_outstanding_and_are_aged()
    {
        var (client, _) = await CreateAsync(Factory, SeedAsync);
        var m = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/money");
        var dash = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
        var r = m.GetProperty("receivables");
        r.GetProperty("total").GetInt64().Should().Be(3_300_000);
        r.GetProperty("total").GetInt64().Should().Be(dash.GetProperty("money").GetProperty("outstanding").GetInt64());

        var items = r.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(2);                                            // D (cancelled project) is excluded
        items[0].GetProperty("paymentStatus").GetString().Should().Be("DEPOSIT_PAID");
        items[0].GetProperty("outstanding").GetInt64().Should().Be(2_000_000);
        items[0].GetProperty("agingFrom").GetString().Should().Be("2030-05-03");
        items[0].GetProperty("agingDays").GetInt32().Should().Be(12);
        items[0].GetProperty("agingBucket").GetString().Should().Be("8-30");
        items[1].GetProperty("agingFrom").GetString().Should().Be("2030-05-10");
        items[1].GetProperty("agingDays").GetInt32().Should().Be(5);
        items[1].GetProperty("agingBucket").GetString().Should().Be("0-7");

        var byClient = r.GetProperty("byClient").EnumerateArray().ToList();
        byClient.Should().ContainSingle();
        byClient[0].GetProperty("outstanding").GetInt64().Should().Be(3_300_000);
        byClient[0].GetProperty("count").GetInt32().Should().Be(2);
        r.GetProperty("hiddenItems").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Accepted_compare_matches_the_dashboard_for_the_month()
    {
        var (client, _) = await CreateAsync(Factory, SeedAsync);
        var m = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/money?period=month");
        var dash = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
        var a = m.GetProperty("accepted");
        a.GetProperty("current").GetInt64().Should().Be(7_300_000);             // B 4M + C 1.3M + D 2M (BR-36 ignores the project)
        a.GetProperty("current").GetInt64().Should().Be(dash.GetProperty("money").GetProperty("acceptedThisMonth").GetInt64());
        a.GetProperty("previous").GetInt64().Should().Be(3_000_000);            // E, accepted 01/04
    }

    private async Task<JsonElement> GetAsync(string period)
    {
        var (client, _) = await CreateAsync(Factory, SeedAsync);
        var resp = await client.GetAsync($"/api/admin/dashboard/money?period={period}");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Clients.AnyAsync(c => c.Name == SeedMarker)) return;

        var client = new Client { Name = SeedMarker };
        var live = new Project { ClientId = client.Id, Name = "MNY Live", DesignType = "LOGO", Status = "IN_PROGRESS" };
        var cancelled = new Project { ClientId = client.Id, Name = "MNY Cancelled", DesignType = "LOGO", Status = "CANCELLED" };

        var seq = 0;
        Quotation Q(Project p, long total, int depositPercent, string payment, DateOnly acceptedAt, DateOnly? depositPaidAt, DateOnly? paidAt)
        {
            var q = new Quotation
            {
                ProjectId = p.Id, Kind = QuotationKinds.Initial, Title = "MNY quote",
                Number = Quotation.FormatNumber("BG", 2030, ++seq), Status = QuotationStatuses.Accepted, ValidityDays = 7,
                Subtotal = total, Total = total, AcceptedAt = acceptedAt, PaymentStatus = payment, DepositPercent = depositPercent,
                DepositPaidAt = depositPaidAt, PaidAt = paidAt,
            };
            q.DepositAmount = Money.Percent(total, depositPercent);
            return q;
        }

        db.Clients.Add(client);
        db.Projects.AddRange(live, cancelled);
        db.Quotations.AddRange(
            Q(live, 10_000_000, 50, PaymentStatuses.Paid, D(4, 20), D(4, 28), D(5, 10)),            // A
            Q(live, 4_000_000, 50, PaymentStatuses.DepositPaid, D(5, 2), D(5, 3), null),            // B
            Q(live, 1_300_000, 0, PaymentStatuses.Unpaid, D(5, 10), null, null),                    // C
            Q(cancelled, 2_000_000, 0, PaymentStatuses.Unpaid, D(5, 5), null, null),                // D
            Q(live, 3_000_000, 0, PaymentStatuses.Paid, D(4, 1), null, D(4, 5)),                    // E
            Q(live, 999, 0, PaymentStatuses.Paid, D(2, 20), null, D(3, 1)));                        // F
        await db.SaveChangesAsync();
    }
}
