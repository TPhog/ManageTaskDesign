using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Catalog;
using FreelanceFlow.Api.Domain.Clients;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Dashboard;

public class DashboardSetupTests(AppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Requires_a_designer_session()
    {
        (await Client.GetAsync("/api/admin/dashboard/setup")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.PostAsync("/api/admin/dashboard/setup/dismiss", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Items_turn_done_as_the_studio_is_configured_and_dismiss_is_idempotent()
    {
        await LoginAsync();
        var s0 = await Client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/setup");
        s0.GetProperty("total").GetInt32().Should().Be(6);
        s0.GetProperty("dismissed").GetBoolean().Should().BeFalse();
        s0.GetProperty("complete").GetBoolean().Should().BeFalse();
        // InitialCreate seeds a placeholder studio name and 4 sample-priced services: neither counts as done
        Done(s0, "STUDIO").Should().BeFalse();
        Done(s0, "SERVICE").Should().BeFalse();
        Done(s0, "TASK_TEMPLATE").Should().BeFalse();
        Done(s0, "CLIENT").Should().BeFalse();

        var settings = await Client.GetFromJsonAsync<JsonElement>("/api/admin/settings");
        var put0 = await Client.PutAsJsonAsync("/api/admin/settings", new
        {
            displayName = "Studio Của Tôi", contactEmail = "me@example.com", contactPhone = (string?)null, logoFileId = (Guid?)null,
            bankBin = (string?)null, bankAccountNo = (string?)null, bankAccountName = (string?)null,
            rowVersion = settings.GetProperty("rowVersion").GetInt64(),
        });
        put0.EnsureSuccessStatusCode();
        var services = await Client.GetFromJsonAsync<JsonElement>("/api/admin/services");
        var first = (services.ValueKind == JsonValueKind.Array ? services : services.GetProperty("items")).EnumerateArray().First();
        var put1 = await Client.PutAsJsonAsync($"/api/admin/services/{first.GetProperty("id").GetGuid()}", new
        {
            name = first.GetProperty("name").GetString(), description = (string?)null, basePrice = 2_500_000L,
            defaultIncludedRounds = 2, extraRoundPrice = 500_000L, active = true, taskTemplate = (string[]?)null,
        });
        put1.EnsureSuccessStatusCode();
        var s00 = await Client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/setup");
        Done(s00, "STUDIO").Should().BeTrue();
        Done(s00, "SERVICE").Should().BeTrue();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var svc = new Service { Name = "Logo", BasePrice = 3_000_000, Active = true };
            svc.SetTaskTemplate(["Phác thảo", "Chốt"]);
            db.Services.Add(svc);
            db.Clients.Add(new Client { Name = "Setup Client" });
            await db.SaveChangesAsync();
        }
        var put = await Client.PutAsJsonAsync("/api/admin/settings/watermark", new { enabled = true, text = (string?)null, opacityPercent = 18 });
        put.EnsureSuccessStatusCode();

        var s1 = await Client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/setup");
        Done(s1, "SERVICE").Should().BeTrue();
        Done(s1, "TASK_TEMPLATE").Should().BeTrue();
        Done(s1, "WATERMARK").Should().BeTrue();
        Done(s1, "CLIENT").Should().BeTrue();

        (await Client.PostAsync("/api/admin/dashboard/setup/dismiss", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.PostAsync("/api/admin/dashboard/setup/dismiss", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/setup"))
            .GetProperty("dismissed").GetBoolean().Should().BeTrue();
    }

    private static bool Done(JsonElement setup, string key) =>
        setup.GetProperty("items").EnumerateArray().First(i => i.GetProperty("key").GetString() == key).GetProperty("done").GetBoolean();
}
