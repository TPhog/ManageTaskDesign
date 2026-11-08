using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Portal;

/// <summary>Plan 06b F2: the client's "Duyệt" only records an opinion; the designer confirms (BR-01).</summary>
public class PortalDeliverableApprovalTests(AppFactory factory) : PortalTestBase(factory)
{
    private sealed record Arranged(HttpClient Portal, Guid ProjectId, Guid DeliverableId, string Name);

    private async Task<Arranged> ArrangeAsync(params string[] moveTo)
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Duyet " + Guid.NewGuid().ToString("N"));
        var name = "Logo-" + Guid.NewGuid().ToString("N")[..8];
        var deliverableId = await CreateDeliverableAsync(projectId, name);
        if (moveTo.Length > 0) await MoveDeliverableAsync(deliverableId, moveTo);
        return new Arranged(await OpenPortalSessionAsync(token), projectId, deliverableId, name);
    }

    private static Task<HttpResponseMessage> ApproveAsync(HttpClient portal, Guid deliverableId) =>
        portal.PostAsync($"/api/portal/deliverables/{deliverableId}/approve", null);

    private static async Task<JsonElement> PortalDeliverableAsync(HttpClient portal, Guid projectId, Guid deliverableId) =>
        (await portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{projectId}"))
            .GetProperty("deliverables").EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == deliverableId);

    private async Task<List<(string Action, string ActorType)>> AdminActivityAsync(Guid projectId) =>
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/activity?pageSize=100"))
            .GetProperty("items").EnumerateArray()
            .Select(a => (a.GetProperty("action").GetString()!, a.GetProperty("actorType").GetString()!))
            .ToList();

    [Fact]
    public async Task Client_approval_is_recorded_without_changing_the_status()
    {
        var s = await ArrangeAsync("IN_PROGRESS", "IN_REVIEW");

        var resp = await ApproveAsync(s.Portal, s.DeliverableId);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("IN_REVIEW");
        body.GetProperty("clientDecision").GetString().Should().Be("APPROVED");
        body.GetProperty("clientDecisionAt").ValueKind.Should().Be(JsonValueKind.String);

        var d = await PortalDeliverableAsync(s.Portal, s.ProjectId, s.DeliverableId);
        d.GetProperty("status").GetString().Should().Be("IN_REVIEW");
        d.GetProperty("clientDecision").GetString().Should().Be("APPROVED");
        var timeline = await s.Portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{s.ProjectId}/timeline");
        timeline.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("message").GetString())
            .Should().Contain($"Khách đã duyệt {s.Name}");
        (await AdminActivityAsync(s.ProjectId)).Should().Contain(("deliverable.client_approved", "CLIENT"));
    }

    [Fact]
    public async Task Approving_twice_is_idempotent_and_logged_once()
    {
        var s = await ArrangeAsync("IN_PROGRESS", "IN_REVIEW");

        (await ApproveAsync(s.Portal, s.DeliverableId)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApproveAsync(s.Portal, s.DeliverableId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await AdminActivityAsync(s.ProjectId)).Count(a => a.Action == "deliverable.client_approved").Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("IN_PROGRESS")]
    [InlineData("IN_PROGRESS,IN_REVIEW,APPROVED")]
    public async Task Approval_outside_in_review_is_an_invalid_transition(string path)
    {
        var s = await ArrangeAsync(path.Length == 0 ? [] : path.Split(','));

        var resp = await ApproveAsync(s.Portal, s.DeliverableId);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("INVALID_TRANSITION");
        (await PortalDeliverableAsync(s.Portal, s.ProjectId, s.DeliverableId))
            .GetProperty("clientDecision").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Client_B_gets_404_approving_a_deliverable_of_A()   // docs/09 §4 isolation
    {
        var a = await ArrangeAsync("IN_PROGRESS", "IN_REVIEW");
        var (_, tokenB) = await CreateClientWithPortalLinkAsync();
        var portalB = await OpenPortalSessionAsync(tokenB);

        var resp = await ApproveAsync(portalB, a.DeliverableId);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(a.Name);
        (await PortalDeliverableAsync(a.Portal, a.ProjectId, a.DeliverableId))
            .GetProperty("clientDecision").ValueKind.Should().Be(JsonValueKind.Null);
        (await ApproveAsync(portalB, Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Approval_without_the_csrf_token_is_forbidden()
    {
        var s = await ArrangeAsync("IN_PROGRESS", "IN_REVIEW");
        s.Portal.DefaultRequestHeaders.Remove("X-CSRF-Token");

        var resp = await ApproveAsync(s.Portal, s.DeliverableId);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Designer_cookie_cannot_approve_on_the_portal()
    {
        var s = await ArrangeAsync("IN_PROGRESS", "IN_REVIEW");

        var resp = await Client.PostAsync($"/api/portal/deliverables/{s.DeliverableId}/approve", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Portal_project_shows_task_progress_per_deliverable()
    {
        var s = await ArrangeAsync("IN_PROGRESS");
        var banner = await CreateDeliverableAsync(s.ProjectId, "Banner");
        foreach (var title in new[] { "SECRET-TASK-1", "SECRET-TASK-2" })
            (await Client.PostAsJsonAsync($"/api/admin/projects/{s.ProjectId}/tasks", new { title, deliverableId = s.DeliverableId }))
                .EnsureSuccessStatusCode();
        var taskId = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={s.ProjectId}"))
            .GetProperty("items")[0].GetProperty("id").GetGuid();
        (await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/move", new { status = "DONE", sortOrder = 1 }))
            .EnsureSuccessStatusCode();

        var resp = await s.Portal.GetAsync($"/api/portal/projects/{s.ProjectId}");

        var raw = await resp.Content.ReadAsStringAsync();
        raw.Should().NotContain("SECRET-TASK");   // TSK-05: only the percentage leaves the designer side
        var deliverables = JsonDocument.Parse(raw).RootElement.GetProperty("deliverables").EnumerateArray().ToList();
        deliverables.Single(d => d.GetProperty("id").GetGuid() == s.DeliverableId).GetProperty("progressPercent").GetInt32().Should().Be(50);
        deliverables.Single(d => d.GetProperty("id").GetGuid() == banner).GetProperty("progressPercent").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Approving_in_an_archived_project_is_404()
    {
        var s = await ArrangeAsync("IN_PROGRESS", "IN_REVIEW");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Projects.Where(p => p.Id == s.ProjectId).ExecuteUpdateAsync(u => u.SetProperty(p => p.ArchivedAt, DateTimeOffset.UtcNow));
        }

        (await ApproveAsync(s.Portal, s.DeliverableId)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Designer_status_change_clears_a_decision_committed_after_the_row_was_loaded()   // B1 race
    {
        var s = await ArrangeAsync("IN_PROGRESS", "IN_REVIEW");
        Factory.BeforeSave.RunOnNextSave(async () =>
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Deliverables.Where(d => d.Id == s.DeliverableId).ExecuteUpdateAsync(u => u
                .SetProperty(d => d.ClientDecision, "APPROVED").SetProperty(d => d.ClientDecisionAt, DateTimeOffset.UtcNow));
        });

        var resp = await Client.PostAsJsonAsync($"/api/admin/deliverables/{s.DeliverableId}/status", new { to = "IN_PROGRESS" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        using var check = Factory.Services.CreateScope();
        var row = await check.ServiceProvider.GetRequiredService<AppDbContext>().Deliverables.AsNoTracking()
            .SingleAsync(d => d.Id == s.DeliverableId);
        row.Status.Should().Be("IN_PROGRESS");
        row.ClientDecision.Should().BeNull();
        row.ClientDecisionAt.Should().BeNull();
    }

    [Fact]
    public async Task Concurrent_double_approve_logs_and_notifies_once()
    {
        var s = await ArrangeAsync("IN_PROGRESS", "IN_REVIEW");

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ApproveAsync(s.Portal, s.DeliverableId)));

        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        (await AdminActivityAsync(s.ProjectId)).Count(a => a.Action == "deliverable.client_approved").Should().Be(1);
        await Factory.Notifications.WaitForAsync(n => n.Fields!.Any(f => f.Name == "Hạng mục" && f.Value == s.Name));
        await Task.Delay(300);
        Factory.Notifications.Sent.Count(n => n.Fields!.Any(f => f.Name == "Hạng mục" && f.Value == s.Name)).Should().Be(1);
    }

    [Fact]
    public async Task Client_approval_notifies_the_designer_once()
    {
        await LoginAsync();
        var clientName = "Khách Duyệt " + Guid.NewGuid();
        var (clientId, token) = await CreateClientWithPortalLinkAsync(clientName);
        var projectName = "Dự án Duyệt " + Guid.NewGuid();
        var projectId = await CreateProjectAsync(clientId, projectName);
        var deliverableId = await CreateDeliverableAsync(projectId, "Logo chính");
        await MoveDeliverableAsync(deliverableId, "IN_PROGRESS", "IN_REVIEW");
        var portal = await OpenPortalSessionAsync(token);

        (await ApproveAsync(portal, deliverableId)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ApproveAsync(portal, deliverableId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var hits = await Factory.Notifications.WaitForAsync(n => n.Fields!.Any(f => f.Name == "Dự án" && f.Value == projectName));
        var n = hits.Should().ContainSingle().Subject;
        n.Title.Should().Be("Khách đã duyệt hạng mục");
        n.Url.Should().Be($"http://localhost/admin/projects/{projectId}?tab=deliverables");
        n.Fields.Should().Contain([new NotificationField("Khách", clientName), new NotificationField("Hạng mục", "Logo chính")]);
    }
}
