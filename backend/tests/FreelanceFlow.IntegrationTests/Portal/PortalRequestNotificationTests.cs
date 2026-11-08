using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Infrastructure.Notifications;

namespace FreelanceFlow.IntegrationTests.Portal;

public class PortalRequestNotificationTests(AppFactory factory) : PortalTestBase(factory)
{
    [Fact]
    public async Task Client_request_notifies_the_designer_once_with_client_project_deliverable_and_link()
    {
        await LoginAsync();
        var clientName = "Khách Discord " + Guid.NewGuid();
        var (clientId, token) = await CreateClientWithPortalLinkAsync(clientName);
        var projectName = "Dự án Discord " + Guid.NewGuid();
        var projectId = await CreateProjectAsync(clientId, projectName);
        var logo = await CreateDeliverableAsync(projectId, "Logo chính");
        var portal = await OpenPortalSessionAsync(token);

        var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests",
            RequestForm("Đổi màu\nchữ sang   xanh navy", logo));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var requestId = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var hits = await Factory.Notifications.WaitForAsync(n => n.Fields!.Any(f => f.Name == "Dự án" && f.Value == projectName));
        var n = hits.Should().ContainSingle().Subject;
        n.Title.Should().Be("Yêu cầu mới từ khách");
        n.Description.Should().Be("Đổi màu chữ sang xanh navy");
        n.Url.Should().Be($"http://localhost/admin/requests?id={requestId}");
        n.Fields.Should().Contain([new NotificationField("Khách", clientName), new NotificationField("Hạng mục", "Logo chính")]);
    }

    [Fact]
    public async Task Long_content_is_cut_to_200_chars_and_designer_recorded_requests_do_not_notify()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectName = "Dự án dài " + Guid.NewGuid();
        var projectId = await CreateProjectAsync(clientId, projectName);
        var portal = await OpenPortalSessionAsync(token);

        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/requests",
            new { content = "Ghi hộ qua Zalo", source = "ZALO" })).EnsureSuccessStatusCode();
        (await portal.PostAsync($"/api/portal/projects/{projectId}/requests", RequestForm(new string('x', 500))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var hits = await Factory.Notifications.WaitForAsync(n => n.Fields!.Any(f => f.Value == projectName));
        await Task.Delay(300); // give a (wrong) second notification time to arrive
        var all = Factory.Notifications.Sent.Where(n => n.Fields!.Any(f => f.Value == projectName)).ToList();
        all.Should().ContainSingle();
        all[0].Description.Should().Be(new string('x', 200) + "…");
        all[0].Fields!.Should().NotContain(f => f.Name == "Hạng mục");
    }
}
