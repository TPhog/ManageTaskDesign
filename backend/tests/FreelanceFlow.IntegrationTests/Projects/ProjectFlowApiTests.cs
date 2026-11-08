using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.IntegrationTests.Portal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Projects;

public class ProjectFlowApiTests(AppFactory factory) : PortalTestBase(factory)
{
    // ---------- helpers ----------

    private async Task<Guid> NewProjectAsync(string name = "Flow")
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/clients", new { name = "Flow Client " + Guid.NewGuid() });
        resp.EnsureSuccessStatusCode();
        var clientId = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return await CreateProjectAsync(clientId, $"{name} {Guid.NewGuid():N}");
    }

    private Task<JsonElement> FlowAsync(Guid projectId) =>
        Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/flow");

    private static List<string> Kinds(JsonElement items) =>
        items.EnumerateArray().Select(a => a.GetProperty("kind").GetString()!).ToList();

    private async Task ConfirmBriefAsync(Guid projectId)
    {
        var briefId = await GetLatestBriefIdAsync(projectId);
        (await Client.PutAsJsonAsync($"/api/admin/briefs/{briefId}", new { content = "Brief" })).EnsureSuccessStatusCode();
        (await Client.PostAsync($"/api/admin/briefs/{briefId}/confirm", null)).EnsureSuccessStatusCode();
    }

    private async Task AcceptAsync(Guid quotationId)
    {
        var rowVersion = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/quotations/{quotationId}"))
            .GetProperty("rowVersion").GetUInt32();
        var resp = await Client.PostAsJsonAsync($"/api/admin/quotations/{quotationId}/accept", new { rowVersion });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
    }

    /// <summary>Brief confirmed + INITIAL quotation (2.000.000 ₫, cọc 50%) sent and accepted, still UNPAID.</summary>
    private async Task<Guid> QuotedAsync(Guid projectId)
    {
        await ConfigureBankAsync();
        await ConfirmBriefAsync(projectId);
        var quotationId = await CreateSentQuotationAsync(projectId);
        await AcceptAsync(quotationId);
        return quotationId;
    }

    private async Task<uint> RowVersionAsync(Guid projectId) =>
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}")).GetProperty("rowVersion").GetUInt32();

    private async Task<HttpResponseMessage> StartAsync(Guid projectId, object tasks, uint? rowVersion = null) =>
        await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/start",
            new { rowVersion = rowVersion ?? await RowVersionAsync(projectId), tasks });

    private async Task<List<JsonElement>> TasksAsync(Guid projectId) =>
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?projectId={projectId}&pageSize=100"))
            .GetProperty("items").EnumerateArray().ToList();

    private async Task<JsonElement> ProjectAsync(Guid projectId) =>
        await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}");

    private async Task<JsonElement> DeliverableAsync(Guid projectId, Guid deliverableId) =>
        (await ProjectAsync(projectId)).GetProperty("deliverables").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == deliverableId);

    private async Task<List<(string Action, bool Visible, string? Message)>> ActivityAsync(Guid projectId) =>
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/activity?pageSize=100"))
            .GetProperty("items").EnumerateArray()
            .Select(a => (a.GetProperty("action").GetString()!, a.GetProperty("visibleToClient").GetBoolean(),
                a.GetProperty("clientMessage").GetString()))
            .ToList();

    private async Task MoveTaskAsync(Guid taskId, string status, double sortOrder) =>
        (await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/move", new { status, sortOrder })).EnsureSuccessStatusCode();

    /// <summary>Service created through the API; its template is set with the domain method (the API for it is Task 4).</summary>
    private async Task<Guid> ServiceWithTemplateAsync(string name, params string[] steps)
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/services",
            new { name, description = "", basePrice = 1_000_000L, defaultIncludedRounds = 2, extraRoundPrice = 200_000L });
        resp.EnsureSuccessStatusCode();
        var id = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Services.FirstAsync(s => s.Id == id)).SetTaskTemplate(steps);
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> DeliverableWithServiceAsync(Guid projectId, Guid serviceId, string name)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/deliverables", new { name, quantity = 1, serviceId });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>What the portal "Duyệt" stores (the portal endpoint is Task 5; the domain method is enough here).</summary>
    private async Task RecordClientApprovalAsync(Guid deliverableId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Deliverables.FirstAsync(d => d.Id == deliverableId))
            .RecordClientApproval(scope.ServiceProvider.GetRequiredService<IClock>());
        await db.SaveChangesAsync();
    }

    // ---------- flow ----------

    [Fact]
    public async Task New_project_flow_is_prepare_and_suggests_adding_a_deliverable()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();

        var flow = await FlowAsync(projectId);

        flow.GetProperty("stage").GetString().Should().Be("PREPARE");
        Kinds(flow.GetProperty("actions")).Should().Equal("ADD_DELIVERABLE", "CONFIRM_BRIEF", "CREATE_QUOTATION");
        flow.GetProperty("actions")[0].GetProperty("primary").GetBoolean().Should().BeTrue();
        flow.GetProperty("actions")[0].GetProperty("target").GetProperty("deliverableId").ValueKind.Should().Be(JsonValueKind.Null);
        Kinds(flow.GetProperty("warnings")).Should().Equal("BRIEF_NOT_CONFIRMED");
        (await Client.GetAsync($"/api/admin/projects/{Guid.NewGuid()}/flow")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Flow_reaches_execute_after_the_brief_and_an_accepted_quotation_and_suggests_starting()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();
        await CreateDeliverableAsync(projectId, "Logo");
        await QuotedAsync(projectId);

        var flow = await FlowAsync(projectId);

        flow.GetProperty("stage").GetString().Should().Be("EXECUTE");
        flow.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("status").GetString())
            .Should().Equal("done", "done", "current", "upcoming", "upcoming");
        Kinds(flow.GetProperty("actions")).Should().Equal("START_PROJECT", "COPY_PAYMENT_REMINDER");
        Kinds(flow.GetProperty("warnings")).Should().Equal("DEPOSIT_UNPAID");
        flow.GetProperty("warnings")[0].GetProperty("message").GetString().Should().StartWith("Chưa nhận cọc 1.000.000 ₫ của báo giá ");
    }

    [Fact]
    public async Task Flow_actions_list_open_projects_only()
    {
        await LoginAsync();
        var open = await NewProjectAsync("Open");
        var cancelled = await NewProjectAsync("Cancelled");
        await ChangeProjectStatusAsync(cancelled, new { to = "CANCELLED", reason = "Khách huỷ" });

        var list = (await Client.GetFromJsonAsync<JsonElement>("/api/admin/flow/actions")).EnumerateArray().ToList();

        var row = list.Single(p => p.GetProperty("projectId").GetGuid() == open);
        row.GetProperty("projectName").GetString().Should().StartWith("Open ");
        row.GetProperty("clientName").GetString().Should().StartWith("Flow Client ");
        row.GetProperty("stage").GetString().Should().Be("PREPARE");
        Kinds(row.GetProperty("actions")).Should().StartWith("ADD_DELIVERABLE");
        list.Should().NotContain(p => p.GetProperty("projectId").GetGuid() == cancelled);
    }

    // ---------- start ----------

    [Fact]
    public async Task Start_preview_proposes_the_service_template_for_not_started_deliverables()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();
        var serviceName = "Logo Flow " + Guid.NewGuid();
        var serviceId = await ServiceWithTemplateAsync(serviceName, "Moodboard", "Phác thảo");
        var logo = await DeliverableWithServiceAsync(projectId, serviceId, "Logo");
        var card = await CreateDeliverableAsync(projectId, "Danh thiếp");
        var running = await CreateDeliverableAsync(projectId, "Banner");
        await MoveDeliverableAsync(running, "IN_PROGRESS");

        var preview = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/start-preview"))
            .EnumerateArray().ToList();

        preview.Select(p => p.GetProperty("deliverableId").GetGuid()).Should().Equal(logo, card);
        preview[0].GetProperty("name").GetString().Should().Be("Logo");
        preview[0].GetProperty("serviceName").GetString().Should().Be(serviceName);
        preview[0].GetProperty("titles").EnumerateArray().Select(t => t.GetString()).Should().Equal("Moodboard", "Phác thảo");
        preview[1].GetProperty("serviceName").ValueKind.Should().Be(JsonValueKind.Null);
        preview[1].GetProperty("titles").GetArrayLength().Should().Be(0);
        (await Client.GetAsync($"/api/admin/projects/{Guid.NewGuid()}/start-preview")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Start_moves_the_project_and_its_deliverables_and_creates_the_tasks_in_one_go()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo");
        await CreateDeliverableAsync(projectId, "Banner");

        var resp = await StartAsync(projectId, new[] { new { deliverableId = logo, titles = new[] { " Phác thảo ", "Hoàn thiện" } } });

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("IN_PROGRESS");
        body.GetProperty("startedDeliverables").GetInt32().Should().Be(2);
        body.GetProperty("createdTasks").GetInt32().Should().Be(2);
        body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString())
            .Should().Equal("Chưa có báo giá gốc được khách chấp nhận.");

        var project = await ProjectAsync(projectId);
        project.GetProperty("status").GetString().Should().Be("IN_PROGRESS");
        project.GetProperty("deliverables").EnumerateArray().Select(d => d.GetProperty("status").GetString())
            .Should().OnlyContain(s => s == "IN_PROGRESS");
        var tasks = await TasksAsync(projectId);
        tasks.Select(t => t.GetProperty("title").GetString()).Should().BeEquivalentTo("Phác thảo", "Hoàn thiện");
        tasks.Should().OnlyContain(t => t.GetProperty("deliverableId").GetGuid() == logo && t.GetProperty("status").GetString() == "TODO");

        var activity = await ActivityAsync(projectId);
        activity.Should().Contain(("project.started", true, "Dự án chuyển sang: Đang thực hiện"));
        activity.Count(a => a.Action == "task.created").Should().Be(2);
        activity.Count(a => a.Action == "deliverable.status_changed").Should().Be(2);
    }

    [Fact]
    public async Task Start_rejects_a_deliverable_of_another_project_and_changes_nothing()
    {
        await LoginAsync();
        var projectA = await NewProjectAsync("A");
        var projectB = await NewProjectAsync("B");
        var logoA = await CreateDeliverableAsync(projectA, "Logo");
        var logoB = await CreateDeliverableAsync(projectB, "Logo B");

        var resp = await StartAsync(projectA, new[] { new { deliverableId = logoB, titles = new[] { "Phác thảo" } } });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .TryGetProperty("tasks[0].deliverableId", out _).Should().BeTrue();
        (await ProjectAsync(projectA)).GetProperty("status").GetString().Should().Be("DRAFT");
        (await DeliverableAsync(projectA, logoA)).GetProperty("status").GetString().Should().Be("NOT_STARTED");
        (await TasksAsync(projectA)).Should().BeEmpty();
    }

    [Fact]
    public async Task Start_of_an_already_started_project_is_an_invalid_transition()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();
        await CreateDeliverableAsync(projectId, "Logo");
        (await StartAsync(projectId, Array.Empty<object>())).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await StartAsync(projectId, Array.Empty<object>());

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public async Task Start_with_a_stale_row_version_is_a_concurrency_conflict()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo");
        var stale = await RowVersionAsync(projectId);
        (await Client.PutAsJsonAsync($"/api/admin/projects/{projectId}", new { name = "Đổi tên", designType = "LOGO" }))
            .EnsureSuccessStatusCode();

        var resp = await StartAsync(projectId, new[] { new { deliverableId = logo, titles = new[] { "Phác thảo" } } }, stale);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
        (await ProjectAsync(projectId)).GetProperty("status").GetString().Should().Be("DRAFT");
        (await DeliverableAsync(projectId, logo)).GetProperty("status").GetString().Should().Be("NOT_STARTED");
        (await TasksAsync(projectId)).Should().BeEmpty();
    }

    // ---------- review ----------

    [Fact]
    public async Task Submit_review_moves_to_in_review_with_a_message_for_the_client()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo");
        var banner = await CreateDeliverableAsync(projectId, "Banner");
        await MoveDeliverableAsync(logo, "IN_PROGRESS");
        await MoveDeliverableAsync(banner, "IN_PROGRESS");

        var resp = await Client.PostAsJsonAsync($"/api/admin/deliverables/{logo}/submit-review", new { });
        var custom = await Client.PostAsJsonAsync($"/api/admin/deliverables/{banner}/submit-review",
            new { clientMessage = "Anh xem giúp em bản banner nhé" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("IN_REVIEW");
        custom.StatusCode.Should().Be(HttpStatusCode.OK);
        var visible = (await ActivityAsync(projectId)).Where(a => a.Visible).Select(a => a.Message).ToList();
        visible.Should().Contain("Hạng mục Logo đã sẵn sàng để bạn duyệt").And.Contain("Anh xem giúp em bản banner nhé");
    }

    [Fact]
    public async Task Submit_review_of_a_not_started_deliverable_is_an_invalid_transition()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo");

        var resp = await Client.PostAsJsonAsync($"/api/admin/deliverables/{logo}/submit-review", new { });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("INVALID_TRANSITION");
        (await Client.PostAsJsonAsync($"/api/admin/deliverables/{Guid.NewGuid()}/submit-review", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Flow_suggests_submit_review_once_every_task_of_the_deliverable_is_done()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo");
        await QuotedAsync(projectId);
        (await StartAsync(projectId, new[] { new { deliverableId = logo, titles = new[] { "Phác thảo", "Hoàn thiện" } } }))
            .EnsureSuccessStatusCode();
        var tasks = await TasksAsync(projectId);

        await MoveTaskAsync(tasks[0].GetProperty("id").GetGuid(), "DONE", 1024);

        var half = await DeliverableAsync(projectId, logo);
        half.GetProperty("taskTotal").GetInt32().Should().Be(2);
        half.GetProperty("taskDone").GetInt32().Should().Be(1);
        half.GetProperty("progressPercent").GetInt32().Should().Be(50);
        Kinds((await FlowAsync(projectId)).GetProperty("actions")).Should().NotContain("SUBMIT_REVIEW");

        await MoveTaskAsync(tasks[1].GetProperty("id").GetGuid(), "DONE", 2048);

        var flow = await FlowAsync(projectId);
        var first = flow.GetProperty("actions")[0];
        first.GetProperty("kind").GetString().Should().Be("SUBMIT_REVIEW");
        first.GetProperty("label").GetString().Should().Be("Gửi duyệt: Logo");
        first.GetProperty("target").GetProperty("deliverableId").GetGuid().Should().Be(logo);
        (await DeliverableAsync(projectId, logo)).GetProperty("status").GetString().Should().Be("IN_PROGRESS"); // BR-01
    }

    [Fact]
    public async Task Confirm_approval_approves_the_deliverable_and_clears_the_client_decision()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo");
        await QuotedAsync(projectId);
        (await StartAsync(projectId, Array.Empty<object>())).EnsureSuccessStatusCode();
        await MoveDeliverableAsync(logo, "IN_REVIEW");
        await RecordClientApprovalAsync(logo);

        var before = await FlowAsync(projectId);
        before.GetProperty("stage").GetString().Should().Be("REVIEW");
        before.GetProperty("actions")[0].GetProperty("kind").GetString().Should().Be("CONFIRM_APPROVAL");
        before.GetProperty("actions")[0].GetProperty("target").GetProperty("deliverableId").GetGuid().Should().Be(logo);
        (await DeliverableAsync(projectId, logo)).GetProperty("clientDecision").GetString().Should().Be("APPROVED");

        var resp = await Client.PostAsJsonAsync($"/api/admin/deliverables/{logo}/confirm-approval", new { });

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("APPROVED");
        body.GetProperty("clientDecision").ValueKind.Should().Be(JsonValueKind.Null);
        Kinds((await FlowAsync(projectId)).GetProperty("actions")).Should().StartWith("DELIVER");
        (await ActivityAsync(projectId)).Should().Contain(("deliverable.status_changed", true, "Logo: Đã duyệt"));
        (await Client.PostAsJsonAsync($"/api/admin/deliverables/{logo}/confirm-approval", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Leaving_in_review_through_the_status_command_clears_the_client_decision()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo");
        await MoveDeliverableAsync(logo, "IN_PROGRESS", "IN_REVIEW");
        await RecordClientApprovalAsync(logo);

        await MoveDeliverableAsync(logo, "IN_PROGRESS");

        var d = await DeliverableAsync(projectId, logo);
        d.GetProperty("status").GetString().Should().Be("IN_PROGRESS");
        d.GetProperty("clientDecision").ValueKind.Should().Be(JsonValueKind.Null);
        d.GetProperty("progressPercent").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Completed_project_with_money_due_keeps_warning_balance_due()
    {
        await LoginAsync();
        var projectId = await NewProjectAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo");
        var quotationId = await QuotedAsync(projectId);
        (await StartAsync(projectId, Array.Empty<object>())).EnsureSuccessStatusCode();
        await MoveDeliverableAsync(logo, "IN_REVIEW", "APPROVED", "DELIVERED");
        await ChangeProjectStatusAsync(projectId, new { to = "COMPLETED", confirmWarnings = true });

        var flow = await FlowAsync(projectId);

        flow.GetProperty("stage").GetString().Should().Be("CLOSE");
        flow.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("status").GetString()).Should().OnlyContain(s => s == "done");
        var warning = flow.GetProperty("warnings").EnumerateArray().Single();
        warning.GetProperty("kind").GetString().Should().Be("BALANCE_DUE");
        warning.GetProperty("message").GetString().Should().Be("Khách còn nợ 2.000.000 ₫");
        var action = flow.GetProperty("actions").EnumerateArray().Single();
        action.GetProperty("kind").GetString().Should().Be("COPY_PAYMENT_REMINDER");
        action.GetProperty("target").GetProperty("quotationId").GetGuid().Should().Be(quotationId);
    }
}
