using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Services;

public class ServiceTaskTemplateTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private static string NewName() => "Template Service " + Guid.NewGuid();

    private async Task<JsonElement> CreateAsync(string name, string[] taskTemplate)
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/services", new
        {
            name, description = "", basePrice = 500_000L, defaultIncludedRounds = 2, extraRoundPrice = 100_000L, taskTemplate,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<string?> Steps(JsonElement service) =>
        service.GetProperty("taskTemplate").EnumerateArray().Select(s => s.GetString()).ToList();

    [Fact]
    public async Task Created_service_keeps_the_trimmed_template_in_order()
    {
        await LoginAsync();
        var name = NewName();

        var created = await CreateAsync(name, [" Moodboard ", "Phác thảo", "Hoàn thiện"]);

        Steps(created).Should().Equal("Moodboard", "Phác thảo", "Hoàn thiện");
        var listed = (await Client.GetFromJsonAsync<JsonElement>("/api/admin/services?includeInactive=true"))
            .EnumerateArray().Single(s => s.GetProperty("name").GetString() == name);
        Steps(listed).Should().Equal("Moodboard", "Phác thảo", "Hoàn thiện");
    }

    [Fact]
    public async Task Update_without_a_template_keeps_it_and_with_one_replaces_it()
    {
        await LoginAsync();
        var name = NewName();
        var id = (await CreateAsync(name, ["A", "B"])).GetProperty("id").GetGuid();

        var keep = await Client.PutAsJsonAsync($"/api/admin/services/{id}", new
        {
            name, description = "", basePrice = 600_000L, defaultIncludedRounds = 2, extraRoundPrice = 100_000L, active = true,
        });
        keep.StatusCode.Should().Be(HttpStatusCode.OK);
        Steps(await keep.Content.ReadFromJsonAsync<JsonElement>()).Should().Equal("A", "B");

        var replace = await Client.PutAsJsonAsync($"/api/admin/services/{id}", new
        {
            name, description = "", basePrice = 600_000L, defaultIncludedRounds = 2, extraRoundPrice = 100_000L, active = true,
            taskTemplate = new[] { "C" },
        });
        Steps(await replace.Content.ReadFromJsonAsync<JsonElement>()).Should().Equal("C");

        var clear = await Client.PutAsJsonAsync($"/api/admin/services/{id}", new
        {
            name, description = "", basePrice = 600_000L, defaultIncludedRounds = 2, extraRoundPrice = 100_000L, active = true,
            taskTemplate = Array.Empty<string>(),
        });
        Steps(await clear.Content.ReadFromJsonAsync<JsonElement>()).Should().BeEmpty();
    }

    [Fact]
    public async Task Invalid_templates_are_rejected()
    {
        await LoginAsync();
        var invalid = new[]
        {
            Enumerable.Range(1, 21).Select(i => $"Bước {i}").ToArray(),
            new[] { "  " },
            new[] { new string('x', 201) },
        };

        foreach (var taskTemplate in invalid)
        {
            var resp = await Client.PostAsJsonAsync("/api/admin/services", new
            {
                name = NewName(), description = "", basePrice = 500_000L, defaultIncludedRounds = 2, extraRoundPrice = 100_000L, taskTemplate,
            });

            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
                .TryGetProperty("taskTemplate", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Seeded_services_have_an_empty_template()
    {
        await LoginAsync();

        var services = (await Client.GetFromJsonAsync<JsonElement>("/api/admin/services?includeInactive=true")).EnumerateArray().ToList();

        services.Should().NotBeEmpty();
        services.Should().OnlyContain(s => s.GetProperty("taskTemplate").ValueKind == JsonValueKind.Array);
    }
}
