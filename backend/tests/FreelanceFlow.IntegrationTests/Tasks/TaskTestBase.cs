using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Tasks;

/// <summary>Designer-side helpers for task tests; call <see cref="IntegrationTestBase.LoginAsync"/> first.</summary>
public abstract class TaskTestBase(AppFactory factory) : IntegrationTestBase(factory)
{
    /// <summary>Today in Vietnam according to the host's IClock — the same value the due filters use.</summary>
    protected DateOnly TodayVn => Factory.Services.GetRequiredService<IClock>().TodayVn;

    protected static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

    protected async Task<Guid> CreateProjectAsync(string name = "Task Project")
    {
        var clientResp = await Client.PostAsJsonAsync("/api/admin/clients", new { name = "Task Client " + Guid.NewGuid() });
        clientResp.EnsureSuccessStatusCode();
        var clientId = (await clientResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var resp = await Client.PostAsJsonAsync("/api/admin/projects", new { clientId, name, designType = "LOGO" });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected async Task<Guid> CreateDeliverableAsync(Guid projectId, string name = "Logo")
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/deliverables",
            new { name, quantity = 1, includedRounds = 2, extraRoundPrice = 0 });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected async Task<JsonElement> CreateTaskAsync(Guid projectId, object body)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/tasks", body);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected async Task<JsonElement> MoveAsync(Guid taskId, string status, double sortOrder)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/tasks/{taskId}/move", new { status, sortOrder });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected async Task<List<Guid>> ListIdsAsync(string query)
    {
        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks?{query}");
        return page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
    }

    protected async Task<List<string>> ActionsAsync(Guid projectId)
    {
        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/activity?pageSize=100");
        return page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()!).ToList();
    }
}
