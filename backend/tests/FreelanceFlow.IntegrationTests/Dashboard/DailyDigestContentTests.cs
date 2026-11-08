using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Tasks;
using FreelanceFlow.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FreelanceFlow.IntegrationTests.Dashboard.DashboardTestHost;

namespace FreelanceFlow.IntegrationTests.Dashboard;

/// <summary>P11 BR-94 content on a host whose IClock says today = 2030-05-15 (VN), 12:00.</summary>
public class DailyDigestContentTests(AppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Content_covers_overdue_today_tomorrow_deadlines_expiring_and_receivables()
    {
        var (_, services) = await CreateAsync(Factory, SeedAsync);
        using (var scope = services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<DailyDigestService>().RunAsync()).Should().Be(DigestOutcome.Sent);

        var n = (await Factory.Notifications.WaitForAsync(x => x.Title == "Nhắc việc ngày 15/05/2030")).Single();
        string Field(string name) => n.Fields!.Single(f => f.Name == name).Value;
        Field("Quá hạn").Should().Be("2");          // 05-14 and 05-10; the DONE task and the CANCELLED project's task are out
        Field("Hôm nay").Should().Be("1");
        Field("Ngày mai").Should().Be("1");
        Field("Công nợ").Should().Contain("4.000.000");   // the CANCELLED project's 9M is out
        n.Description.Should().Contain("DGC overdue").And.Contain("DGC Logo").And.Contain("BG-2030-0001");
        n.Description.Should().NotContain("DGC cancelled task").And.NotContain("DGC done task");
        n.Url.Should().Be("http://localhost/admin");
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Clients.AnyAsync(c => c.Name == "DGC Seed Client")) return;

        var client = new Client { Name = "DGC Seed Client" };
        var live = new Project { ClientId = client.Id, Name = "DGC Logo", DesignType = "LOGO", Status = "IN_PROGRESS", DueDate = D(5, 17) };
        var cancelled = new Project { ClientId = client.Id, Name = "DGC Cancelled", DesignType = "LOGO", Status = "CANCELLED", DueDate = D(5, 17) };
        ProjectTask T(Project p, string title, string status, DateOnly due) => new()
        {
            ProjectId = p.Id, Title = title, Status = status, DueDate = due, SortOrder = ProjectTask.SortStep,
        };
        Quotation Q(Project p, string status, long total, DateOnly? expiry, string? number) => new()
        {
            ProjectId = p.Id, Kind = QuotationKinds.Initial, Title = "DGC quote", Number = number, Status = status, ValidityDays = 7,
            Subtotal = total, Total = total, ExpiryDate = expiry, IssueDate = expiry?.AddDays(-7),
            AcceptedAt = status == QuotationStatuses.Accepted ? D(5, 1) : null,
            PaymentStatus = PaymentStatuses.Unpaid,
        };

        db.Clients.Add(client);
        db.Projects.AddRange(live, cancelled);
        db.Tasks.AddRange(
            T(live, "DGC overdue", "TODO", D(5, 14)),
            T(live, "DGC overdue 2", "IN_PROGRESS", D(5, 10)),
            T(live, "DGC done task", "DONE", D(5, 10)),
            T(live, "DGC today", "TODO", D(5, 15)),
            T(live, "DGC tomorrow", "TODO", D(5, 16)),
            T(cancelled, "DGC cancelled task", "TODO", D(5, 14)));
        db.Quotations.AddRange(
            Q(live, QuotationStatuses.Sent, 1_000_000, D(5, 16), "BG-2030-0001"),
            Q(live, QuotationStatuses.Accepted, 4_000_000, null, "BG-2030-0002"),
            Q(cancelled, QuotationStatuses.Accepted, 9_000_000, null, "BG-2030-0003"));
        await db.SaveChangesAsync();
    }
}
