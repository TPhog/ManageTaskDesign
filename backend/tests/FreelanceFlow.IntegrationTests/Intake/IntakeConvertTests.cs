using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.RequestTemplates;
using FreelanceFlow.Api.Features.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Intake;

public class IntakeConvertTests(AppFactory factory) : IntakeTestBase(factory)
{
    private async Task<Guid> LinkMenuTemplateToNewServiceAsync()
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/services",
            new { name = "Thiết kế menu", description = "", basePrice = 1_500_000L, defaultIncludedRounds = 3, extraRoundPrice = 250_000L });
        var serviceId = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var menu = await db.RequestTemplates.FirstAsync(t => t.Name == "Menu cửa hàng");
        menu.Update(menu.Name, menu.Description, menu.DesignType, serviceId, menu.Active, menu.Fields().Cast<TemplateField?>().ToList());
        await db.SaveChangesAsync();
        return serviceId;
    }

    private Task<HttpResponseMessage> ConvertAsync(Guid id, JsonElement detail, Guid? existingClientId = null, Guid? serviceId = null,
        string? clientName = "Chị Lan", string designType = "PRINT") =>
        Client.PostAsJsonAsync($"/api/admin/intake/{id}/convert", new
        {
            existingClientId, clientName, projectName = "Menu cửa hàng — Chị Lan", designType, dueDate = "2026-12-01",
            serviceId, deliverableName = (string?)null, rowVersion = detail.GetProperty("rowVersion").GetUInt32(),
        });

    [Fact]
    public async Task One_click_creates_client_project_brief_and_deliverable()   // AC-INT9, BR-136
    {
        var slug = await EnableIntakeAsync();
        var serviceId = await LinkMenuTemplateToNewServiceAsync();
        var id = await SubmitMenuAsync(Anonymous(), slug);
        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/intake/{id}");
        detail.GetProperty("suggestedServiceId").GetGuid().Should().Be(serviceId);
        detail.GetProperty("suggestedServiceName").GetString().Should().Be("Thiết kế menu");
        var newCount = (await Client.GetFromJsonAsync<JsonElement>("/api/admin/intake/new-count")).GetProperty("count").GetInt32();

        var resp = await ConvertAsync(id, detail, serviceId: serviceId);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var (clientId, projectId, deliverableId) = (result.GetProperty("clientId").GetGuid(), result.GetProperty("projectId").GetGuid(),
            result.GetProperty("deliverableId").GetGuid());
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var client = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == clientId);
            (client.Name, client.Phone, client.ContactChannel).Should().Be(("Chị Lan", "0909 123 456", "Form khách mới"));
            var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId);
            (project.ClientId, project.DesignType, project.Status, project.DueDate).Should().Be((clientId, "PRINT", "DRAFT", (DateOnly?)new DateOnly(2026, 12, 1)));
            var brief = await db.ProjectBriefs.AsNoTracking().SingleAsync(b => b.ProjectId == projectId);
            (brief.Version, brief.Status, brief.Content).Should().Be((1, "DRAFT", detail.GetProperty("body").GetString()));
            var deliverable = await db.Deliverables.AsNoTracking().SingleAsync(d => d.ProjectId == projectId);
            (deliverable.Id, deliverable.ServiceId, deliverable.Name, deliverable.IncludedRounds, deliverable.ExtraRoundPrice)
                .Should().Be((deliverableId, (Guid?)serviceId, "Thiết kế menu", 3, 250_000L));
            (await db.ActivityLogs.AsNoTracking().AnyAsync(a => a.Action == "intake.converted" && a.EntityId == id)).Should().BeTrue();
        }
        var images = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/images?ownerType=INTAKE_SUBMISSION");
        images.GetArrayLength().Should().Be(1);
        var after = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/intake/{id}");
        after.GetProperty("status").GetString().Should().Be("CONVERTED");
        after.GetProperty("convertedProjectId").GetGuid().Should().Be(projectId);
        (await Client.GetFromJsonAsync<JsonElement>("/api/admin/intake/new-count")).GetProperty("count").GetInt32().Should().Be(newCount - 1);
        (await Client.DeleteAsync($"/api/admin/intake/{id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);   // converted stays

        // P10 × plan 13: the joined photo keeps its intake/ objects; the daily sweep neither misses nor deletes them
        using (var scope = Factory.Services.CreateScope())
        {
            var report = await scope.ServiceProvider.GetRequiredService<StorageSweeper>().RunAsync();
            (report.MissingObjects, report.OrphansDeleted, report.StrayVariantsDeleted).Should().Be((0, 0, 0));
        }
        (await Client.GetAsync(images[0].GetProperty("thumbUrl").GetString())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Converting_to_an_existing_client_creates_no_duplicate()
    {
        var slug = await EnableIntakeAsync();
        var existing = await Client.PostAsJsonAsync("/api/admin/clients", new { name = "Khách quen " + Guid.NewGuid() });
        var existingId = (await existing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var id = await SubmitMenuAsync(Anonymous(), slug, images: 0);
        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/intake/{id}");
        int Count() { using var s = Factory.Services.CreateScope(); return s.ServiceProvider.GetRequiredService<AppDbContext>().Clients.Count(); }
        var before = Count();

        var resp = await ConvertAsync(id, detail, existingClientId: existingId, clientName: null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("clientId").GetGuid().Should().Be(existingId);
        result.GetProperty("deliverableId").ValueKind.Should().Be(JsonValueKind.Null);   // no service chosen → no deliverable
        Count().Should().Be(before);
    }

    [Fact]
    public async Task Convert_validates_input_and_refuses_twice_or_stale()   // AC-INT9
    {
        var slug = await EnableIntakeAsync();
        var id = await SubmitMenuAsync(Anonymous(), slug, images: 0);
        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/intake/{id}");

        var bad = await ConvertAsync(id, detail, clientName: " ", designType: "FLYER");
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").EnumerateObject().Select(e => e.Name)
            .Should().BeEquivalentTo(["clientName", "designType"]);
        (await ConvertAsync(id, detail, existingClientId: Guid.NewGuid(), clientName: null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ConvertAsync(id, detail, serviceId: Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var stale = await Client.PostAsJsonAsync($"/api/admin/intake/{id}/convert", new
        {
            clientName = "Lan", projectName = "Menu", designType = "PRINT", rowVersion = detail.GetProperty("rowVersion").GetUInt32() + 1,
        });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");

        (await ConvertAsync(id, detail)).StatusCode.Should().Be(HttpStatusCode.OK);
        var again = await ConvertAsync(id, await Client.GetFromJsonAsync<JsonElement>($"/api/admin/intake/{id}"));
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("INVALID_TRANSITION");
    }
}
