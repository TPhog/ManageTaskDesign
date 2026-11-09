using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Activity;
using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FreelanceFlow.IntegrationTests.Dashboard.DashboardTestHost;

namespace FreelanceFlow.IntegrationTests.Dashboard;

/// <summary>P11 BR-95: the client-activity feed. Entity ids are fixed so hrefs can be asserted.</summary>
public class DashboardClientActivityTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private const string SeedMarker = "ACT Seed Client";
    private static readonly Guid RequestId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid DeliverableId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid QuotationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid ProjectId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004");
    private static readonly Guid IntakeId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005");
    private static readonly DateTimeOffset T0 = new(2030, 5, 15, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Requires_a_designer_session() =>
        (await Client.GetAsync("/api/admin/dashboard/client-activity")).StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Lists_client_actions_and_accepted_quotations_newest_first_with_server_built_links()
    {
        var items = (await GetAsync()).GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("kind").GetString()).Should().Equal(
            "INTAKE_SUBMITTED", "CLIENT_PAYMENT_NOTICE", "QUOTATION_CLIENT_ACCEPTED",
            "QUOTATION_ACCEPTED", "DELIVERABLE_APPROVED", "REQUEST_CREATED");

        // R2 I2: the P12/P13 client statements, worded for the designer (the stored client_message is for the client)
        items[0].GetProperty("text").GetString().Should().Be("Khách mới gửi yêu cầu qua form");
        items[0].GetProperty("href").GetString().Should().Be("/admin/intake");
        items[0].GetProperty("clientName").ValueKind.Should().Be(JsonValueKind.Null);
        items[0].GetProperty("projectName").ValueKind.Should().Be(JsonValueKind.Null);
        items[1].GetProperty("text").GetString().Should()
            .Be("Khách báo đã chuyển 500 ₫ (phần còn lại) cho báo giá BG-2030-0001 (v1) — kiểm tra & xác nhận");
        items[1].GetProperty("href").GetString().Should().Be($"/admin/quotations/{QuotationId}");
        items[2].GetProperty("text").GetString().Should().Be("Khách đồng ý báo giá BG-2030-0001 (v1) — chờ bạn xác nhận");
        items[2].GetProperty("href").GetString().Should().Be($"/admin/quotations/{QuotationId}");

        items = items.Skip(3).ToList();
        items[0].GetProperty("text").GetString().Should().Be("Báo giá BG-2030-0001 (v1) đã được chấp nhận");
        items[0].GetProperty("href").GetString().Should().Be($"/admin/quotations/{QuotationId}");
        items[1].GetProperty("text").GetString().Should().Be("Khách đã duyệt Logo");
        items[1].GetProperty("href").GetString().Should().Be($"/admin/projects/{ProjectId}");
        items[2].GetProperty("text").GetString().Should().Be("Khách gửi yêu cầu mới");
        items[2].GetProperty("href").GetString().Should().Be($"/admin/requests/{RequestId}");
        items.Should().OnlyContain(i => i.GetProperty("clientName").GetString() == SeedMarker
                                        && i.GetProperty("projectName").GetString() == "ACT Logo");
    }

    [Fact]
    public async Task Limit_is_applied_and_clamped_without_error()
    {
        (await GetAsync(2)).GetProperty("items").GetArrayLength().Should().Be(2);
        (await GetAsync(0)).GetProperty("items").GetArrayLength().Should().Be(1);
        (await GetAsync(999)).GetProperty("items").GetArrayLength().Should().Be(6);
    }

    private async Task<JsonElement> GetAsync(int? limit = null)
    {
        var (client, _) = await CreateAsync(Factory, SeedAsync);
        var resp = await client.GetAsync("/api/admin/dashboard/client-activity" + (limit is null ? "" : $"?limit={limit}"));
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Clients.AnyAsync(c => c.Name == SeedMarker)) return;

        var client = new Client { Name = SeedMarker };
        var project = new Project { Id = ProjectId, ClientId = client.Id, Name = "ACT Logo", DesignType = "LOGO", Status = "IN_PROGRESS" };
        var quotation = new Quotation
        {
            Id = QuotationId, ProjectId = project.Id, Kind = QuotationKinds.Initial, Title = "ACT quote",
            Number = Quotation.FormatNumber("BG", 2030, 1), Status = QuotationStatuses.Accepted, ValidityDays = 7,
            Subtotal = 1_000, Total = 1_000, AcceptedAt = D(5, 15), PaymentStatus = PaymentStatuses.Unpaid,
        };
        ActivityLog Log(string actor, string entityType, Guid entityId, string action, int minutes, string? message = null,
            string data = "{}", bool anonymous = false) => new()
        {
            OccurredAt = T0.AddMinutes(minutes), ActorType = actor,
            ClientId = anonymous ? null : client.Id, ProjectId = anonymous ? null : project.Id,
            EntityType = entityType, EntityId = entityId, Action = action, ClientMessage = message, Data = data,
        };

        db.Clients.Add(client);
        db.Projects.Add(project);
        db.Quotations.Add(quotation);
        await db.SaveChangesAsync();
        db.ActivityLogs.AddRange(
            Log("CLIENT", "request", RequestId, "request.created", 0),
            Log("CLIENT", "deliverable", DeliverableId, "deliverable.client_approved", 1, "Khách đã duyệt Logo"),
            Log("DESIGNER", "request", Guid.NewGuid(), "request.created", 2),                // designer-made: excluded
            Log("CLIENT", "file", Guid.NewGuid(), "image.uploaded", 3),                      // not a feed action
            Log("DESIGNER", "quotation", QuotationId, "quotation.accepted", 4,
                "Báo giá BG-2030-0001 (v1) đã được chấp nhận"),   // the exact shape QuotationWorkflowEndpoints writes
            // the exact shapes PortalQuotationsEndpoints / PublicIntakeEndpoints write
            Log("CLIENT", "quotation", QuotationId, "quotation.client_accepted", 5,
                "Bạn đã đồng ý báo giá BG-2030-0001 — chờ nhà thiết kế xác nhận", """{"number":"BG-2030-0001","version":1}"""),
            Log("CLIENT", "quotation", QuotationId, "quotation.client_payment_notice", 6,
                "Bạn báo đã chuyển 500 ₫ (phần còn lại) cho báo giá BG-2030-0001 — chờ nhà thiết kế kiểm tra",
                """{"number":"BG-2030-0001","version":1,"purpose":"remaining","amount":500,"hasNote":false}"""),
            Log("CLIENT", "intake_submission", IntakeId, "intake.submitted", 7, data: """{"templateId":null,"imageCount":0}""",
                anonymous: true));
        await db.SaveChangesAsync();
    }
}
