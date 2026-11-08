using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.IntegrationTests.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Projects;

/// <summary>P14 DF-07: "Bàn giao" takes an optional http(s) link and a message, shown on the client timeline.</summary>
public class DeliveryLinkTests(AppFactory factory) : TaskTestBase(factory)
{
    private async Task<(Guid ProjectId, Guid DeliverableId)> DeliverableAtAsync(params string[] path)
    {
        var projectId = await CreateProjectAsync();
        var deliverableId = await CreateDeliverableAsync(projectId, "Logo");
        foreach (var to in path)
            (await Client.PostAsJsonAsync($"/api/admin/deliverables/{deliverableId}/status", new { to })).EnsureSuccessStatusCode();
        return (projectId, deliverableId);
    }

    private Task<(Guid ProjectId, Guid DeliverableId)> ApprovedDeliverableAsync() =>
        DeliverableAtAsync("IN_PROGRESS", "IN_REVIEW", "APPROVED");

    private async Task<(string? Message, string Data)> LastStatusLogAsync(Guid projectId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.ActivityLogs
            .Where(a => a.ProjectId == projectId && a.Action == "deliverable.status_changed" && a.VisibleToClient)
            .OrderByDescending(a => a.Id).FirstAsync();
        return (log.ClientMessage, log.Data);
    }

    [Fact]
    public async Task Delivering_with_link_and_message_publishes_them_on_the_client_timeline()
    {
        await LoginAsync();
        var (projectId, deliverableId) = await ApprovedDeliverableAsync();
        var resp = await Client.PostAsJsonAsync($"/api/admin/deliverables/{deliverableId}/status",
            new { to = "DELIVERED", deliveryUrl = "https://drive.google.com/drive/folders/abc", clientMessage = "Bản final đây ạ" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (message, data) = await LastStatusLogAsync(projectId);
        message.Should().Be("Logo: Đã bàn giao\nBản final đây ạ\nhttps://drive.google.com/drive/folders/abc");
        JsonDocument.Parse(data).RootElement.GetProperty("deliveryUrl").GetString()
            .Should().Be("https://drive.google.com/drive/folders/abc");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://x.y/z")]
    [InlineData("drive.google.com/abc")]
    [InlineData("https://a.b/c d")]
    public async Task Delivering_rejects_a_non_http_link(string url)
    {
        await LoginAsync();
        var (projectId, deliverableId) = await ApprovedDeliverableAsync();
        var resp = await Client.PostAsJsonAsync($"/api/admin/deliverables/{deliverableId}/status",
            new { to = "DELIVERED", deliveryUrl = url });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.GetProperty("deliveryUrl")[0].GetString().Should().Be("Link phải bắt đầu bằng http:// hoặc https://");

        var d = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}")).GetProperty("deliverables")[0];
        d.GetProperty("status").GetString().Should().Be("APPROVED");
    }

    [Fact]
    public async Task Delivering_rejects_a_too_long_link_or_message()
    {
        await LoginAsync();
        var (_, deliverableId) = await ApprovedDeliverableAsync();

        var longUrl = await Client.PostAsJsonAsync($"/api/admin/deliverables/{deliverableId}/status",
            new { to = "DELIVERED", deliveryUrl = "https://x.y/" + new string('a', 490) });
        var longMessage = await Client.PostAsJsonAsync($"/api/admin/deliverables/{deliverableId}/status",
            new { to = "DELIVERED", clientMessage = new string('a', 501) });

        longUrl.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        longMessage.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await longMessage.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .GetProperty("clientMessage")[0].GetString().Should().Be("Tối đa 500 ký tự");
    }

    [Fact]
    public async Task Delivering_without_extras_keeps_the_plain_message()
    {
        await LoginAsync();
        var (projectId, deliverableId) = await ApprovedDeliverableAsync();
        var resp = await Client.PostAsJsonAsync($"/api/admin/deliverables/{deliverableId}/status",
            new { to = "DELIVERED", deliveryUrl = "  ", clientMessage = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (message, data) = await LastStatusLogAsync(projectId);
        message.Should().Be("Logo: Đã bàn giao");
        JsonDocument.Parse(data).RootElement.TryGetProperty("deliveryUrl", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Link_and_message_are_ignored_for_other_transitions()
    {
        await LoginAsync();
        var (projectId, deliverableId) = await DeliverableAtAsync("IN_PROGRESS");
        var resp = await Client.PostAsJsonAsync($"/api/admin/deliverables/{deliverableId}/status",
            new { to = "IN_REVIEW", deliveryUrl = "javascript:x", clientMessage = "Không hiện" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (message, _) = await LastStatusLogAsync(projectId);
        message.Should().Be("Logo: Chờ duyệt");
    }
}
