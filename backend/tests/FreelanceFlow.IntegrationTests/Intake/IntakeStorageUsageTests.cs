using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Intake;

/// <summary>
/// R2 M7: photos of a not-yet-converted intake submission count toward the quota but belong to no project, so the
/// storage page's per-project list never showed them. GET /api/admin/storage/intake reports them as one group.
/// </summary>
public class IntakeStorageUsageTests(AppFactory factory) : IntakeTestBase(factory)
{
    private async Task<JsonElement> UsageAsync() =>
        await Client.GetFromJsonAsync<JsonElement>("/api/admin/storage/intake");

    [Fact]
    public async Task Unconverted_intake_photos_are_reported_until_the_submission_is_removed()
    {
        var slug = await EnableIntakeAsync();
        var empty = await UsageAsync();
        empty.GetProperty("submissionCount").GetInt32().Should().Be(0);
        empty.GetProperty("imageCount").GetInt32().Should().Be(0);
        empty.GetProperty("totalBytes").GetInt64().Should().Be(0);

        var withPhotos = await SubmitMenuAsync(Anonymous(), slug, images: 2);
        await SubmitMenuAsync(Anonymous(), slug, images: 0);   // text only: not a storage group member

        var usage = await UsageAsync();
        usage.GetProperty("submissionCount").GetInt32().Should().Be(1);
        usage.GetProperty("imageCount").GetInt32().Should().Be(2);
        usage.GetProperty("totalBytes").GetInt64().Should().BePositive();

        (await Client.DeleteAsync($"/api/admin/intake/{withPhotos}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UsageAsync()).GetProperty("imageCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Requires_a_designer_session() =>
        (await Anonymous().GetAsync("/api/admin/storage/intake")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
