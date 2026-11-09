using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Intake;

public class IntakeLinkSettingsTests(AppFactory factory) : IntakeTestBase(factory)
{
    [Fact]
    public async Task Form_answers_only_while_enabled_and_keeps_its_link()   // AC-INT1, BR-133
    {
        var slug = await EnableIntakeAsync();
        var on = await Client.GetFromJsonAsync<JsonElement>("/api/admin/settings/intake");
        on.GetProperty("enabled").GetBoolean().Should().BeTrue();
        slug.Should().HaveLength(22);
        on.GetProperty("url").GetString().Should().Be($"http://localhost/f/{slug}");
        (await Anonymous().GetAsync($"/api/public/intake/{slug}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var off = await Client.PutAsJsonAsync("/api/admin/settings/intake", new { enabled = false });
        (await off.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString().Should().Be(slug);
        (await Anonymous().GetAsync($"/api/public/intake/{slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await EnableIntakeAsync()).Should().Be(slug);   // off/on keeps the same link

        using var scope = Factory.Services.CreateScope();
        var logs = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ActivityLogs.AsNoTracking()
            .Where(a => a.Action == "settings.intake_updated").Select(a => a.Data).ToListAsync();
        logs.Should().HaveCountGreaterThanOrEqualTo(3).And.OnlyContain(d => !d.Contains(slug));   // the slug is never logged
    }

    [Fact]
    public async Task Regenerating_kills_the_old_link()   // AC-INT2
    {
        var old = await EnableIntakeAsync();

        var resp = await Client.PostAsync("/api/admin/settings/intake/regenerate", null);

        var fresh = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();
        fresh.Should().NotBe(old).And.HaveLength(22);
        (await Anonymous().GetAsync($"/api/public/intake/{old}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Anonymous().GetAsync($"/api/public/intake/{fresh}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Malformed_slug_and_anonymous_settings_are_refused()
    {
        (await Anonymous().GetAsync("/api/public/intake/not-a-slug")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Anonymous().GetAsync("/api/admin/settings/intake")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
