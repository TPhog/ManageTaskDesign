using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Settings;

public class DigestSettingsTests(AppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Requires_a_designer_session() =>
        (await Client.GetAsync("/api/admin/settings/digest")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Defaults_to_on_at_eight_and_reports_whether_a_channel_exists()
    {
        await LoginAsync();
        var d = await Client.GetFromJsonAsync<JsonElement>("/api/admin/settings/digest");
        d.GetProperty("enabled").GetBoolean().Should().BeTrue();
        d.GetProperty("hourVn").GetInt32().Should().Be(8);
        d.GetProperty("channelConfigured").GetBoolean().Should().BeFalse();   // AppFactory blanks the webhook
    }

    [Fact]
    public async Task Update_persists_and_rejects_an_hour_outside_zero_to_23()
    {
        await LoginAsync();
        (await Client.PutAsJsonAsync("/api/admin/settings/digest", new { enabled = false, hourVn = 24 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var ok = await Client.PutAsJsonAsync("/api/admin/settings/digest", new { enabled = false, hourVn = 6 });
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var d = await Client.GetFromJsonAsync<JsonElement>("/api/admin/settings/digest");
        d.GetProperty("enabled").GetBoolean().Should().BeFalse();
        d.GetProperty("hourVn").GetInt32().Should().Be(6);
    }
}
