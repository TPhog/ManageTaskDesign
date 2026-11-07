using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace FreelanceFlow.IntegrationTests.Smoke;

public class HealthCheckTests : IntegrationTestBase
{
    public HealthCheckTests(AppFactory factory) : base(factory) { }

    [Fact]
    public async Task Health_returns_200_when_db_alive()
    {
        var response = await Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("healthy");
    }
}
