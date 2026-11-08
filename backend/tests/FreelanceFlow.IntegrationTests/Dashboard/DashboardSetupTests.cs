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
        Done(s0, "SERVICE").Should().BeTrue();   // InitialCreate seeds 4 priced services
        Done(s0, "TASK_TEMPLATE").Should().BeFalse();
        Done(s0, "CLIENT").Should().BeFalse();

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
