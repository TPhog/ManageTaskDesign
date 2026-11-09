using System.Net;
using System.Text.Json;
using FreelanceFlow.IntegrationTests.Portal;

namespace FreelanceFlow.IntegrationTests.Intake;

/// <summary>Plan 13: the portal "Đặt thiết kế mới" link reads the studio's public intake URL from /api/portal/me.</summary>
public class PortalIntakeLinkTests(AppFactory factory) : PortalTestBase(factory)
{
    [Fact]
    public async Task Me_shows_the_public_form_link_only_while_it_is_enabled()
    {
        await LoginAsync();
        var (_, token) = await CreateClientWithPortalLinkAsync();
        var portal = await OpenPortalSessionAsync(token);

        async Task<JsonElement> DesignerAsync() =>
            (await portal.GetFromJsonAsync<JsonElement>("/api/portal/me")).GetProperty("designer");

        (await DesignerAsync()).GetProperty("intakeUrl").ValueKind.Should().Be(JsonValueKind.Null);

        var on = await Client.PutAsJsonAsync("/api/admin/settings/intake", new { enabled = true });
        on.StatusCode.Should().Be(HttpStatusCode.OK);
        var slug = (await on.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();
        (await DesignerAsync()).GetProperty("intakeUrl").GetString().Should().Be($"http://localhost/f/{slug}");

        (await Client.PutAsJsonAsync("/api/admin/settings/intake", new { enabled = false })).EnsureSuccessStatusCode();
        (await DesignerAsync()).GetProperty("intakeUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
