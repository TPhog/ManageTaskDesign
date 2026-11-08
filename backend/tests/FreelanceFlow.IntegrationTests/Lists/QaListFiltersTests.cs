using System.Net;
using System.Text.Json;
using FreelanceFlow.IntegrationTests.Quotations;

namespace FreelanceFlow.IntegrationTests.Lists;

/// <summary>Plan 06c Task 1 (QA D-02/D-06/D-07/D-18/D-24): list filters and quotedIn on the project detail.</summary>
public class QaListFiltersTests(AppFactory factory) : QuotationTestBase(factory)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..10];

    private async Task<Guid> CreateClientAsync(string name, string? company = null)
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/clients", new { name, company });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateProjectForAsync(Guid clientId, string name)
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/projects", new { clientId, name, designType = "LOGO" });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateTaskAsync(Guid projectId, string title)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/tasks", new { title });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateRequestAsync(Guid projectId, string content)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/requests", new { content, source = "ZALO" });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("request").GetProperty("id").GetGuid();
    }

    private async Task<List<Guid>> IdsAsync(string url)
    {
        var page = await Client.GetFromJsonAsync<JsonElement>(url);
        return page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
    }

    private static uint Rv(JsonElement quotation) => quotation.GetProperty("rowVersion").GetUInt32();

    [Fact]
    public async Task Project_search_matches_the_project_name_or_the_client_name_and_company()
    {
        await LoginAsync();
        var tag = Tag();
        var byCompany = await CreateClientAsync($"Tiệm bánh {Guid.NewGuid():N}", $"Cty {tag}");
        var byName = await CreateClientAsync($"Khách {tag}");
        var other = await CreateClientAsync($"Khác {Guid.NewGuid():N}");
        var p1 = await CreateProjectForAsync(byCompany, "Logo");
        var p2 = await CreateProjectForAsync(byName, "Menu");
        var p3 = await CreateProjectForAsync(other, $"Biển hiệu {tag}");
        var p4 = await CreateProjectForAsync(other, "Không khớp");

        var ids = await IdsAsync($"/api/admin/projects?q={tag}&pageSize=100");

        ids.Should().BeEquivalentTo(new[] { p1, p2, p3 });
        ids.Should().NotContain(p4);
    }

    [Fact]
    public async Task Clients_can_be_listed_newest_first_for_pickers()
    {
        await LoginAsync();
        var tag = Tag();
        var a = await CreateClientAsync($"A {tag}");
        var b = await CreateClientAsync($"B {tag}");

        (await IdsAsync($"/api/admin/clients?q={tag}&sort=createdAt:desc")).Should().Equal(b, a);
        (await IdsAsync($"/api/admin/clients?q={tag}")).Should().Equal(a, b); // default stays by name
    }

    [Fact]
    public async Task Client_list_outstanding_follows_BR73_after_the_deposit()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (projectId, quotationId) = await CreateSendableDraftAsync(); // 2.500.000, deposit 50%
        var sent = await SendAsync(quotationId);
        var accepted = await Client.PostAsJsonAsync($"/api/admin/quotations/{quotationId}/accept", new { rowVersion = Rv(sent) });
        accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
        var paid = await Client.PostAsJsonAsync($"/api/admin/quotations/{quotationId}/payment-status",
            new { to = "DEPOSIT_PAID", date = "2026-11-10", rowVersion = Rv(await accepted.Content.ReadFromJsonAsync<JsonElement>()) });
        paid.StatusCode.Should().Be(HttpStatusCode.OK, await paid.Content.ReadAsStringAsync());
        var clientName = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}"))
            .GetProperty("client").GetProperty("name").GetString()!;

        var list = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/clients?q={Uri.EscapeDataString(clientName)}");

        list.GetProperty("items")[0].GetProperty("outstanding").GetInt64().Should().Be(1_250_000);
    }

    [Fact]
    public async Task Tasks_filter_by_search_text_and_by_client()
    {
        await LoginAsync();
        var tag = Tag();
        var c1 = await CreateClientAsync($"Task KH {Guid.NewGuid():N}");
        var c2 = await CreateClientAsync($"Task KH {Guid.NewGuid():N}");
        var p1 = await CreateProjectForAsync(c1, "Dự án 1");
        var p2 = await CreateProjectForAsync(c2, $"Dự án {tag}");
        var t1 = await CreateTaskAsync(p1, $"Phác thảo {tag}");
        var t2 = await CreateTaskAsync(p2, "Chốt màu");   // matches through its project name
        var t3 = await CreateTaskAsync(p1, "Không khớp");

        (await IdsAsync($"/api/admin/tasks?q={tag}&pageSize=100")).Should().BeEquivalentTo(new[] { t1, t2 });
        (await IdsAsync($"/api/admin/tasks?clientId={c1}&pageSize=100")).Should().BeEquivalentTo(new[] { t1, t3 });
    }

    [Fact]
    public async Task Requests_filter_by_search_text_and_by_client()
    {
        await LoginAsync();
        var tag = Tag();
        var c1 = await CreateClientAsync($"Req KH {tag}");
        var c2 = await CreateClientAsync($"Req KH {Guid.NewGuid():N}");
        var p1 = await CreateProjectForAsync(c1, "Dự án A");
        var p2 = await CreateProjectForAsync(c2, "Dự án B");
        var r1 = await CreateRequestAsync(p1, "Đổi màu chữ");        // matches through the client name
        var r2 = await CreateRequestAsync(p2, $"Thêm tagline {tag}"); // matches through the content
        var r3 = await CreateRequestAsync(p2, "Không khớp");

        (await IdsAsync($"/api/admin/requests?q={tag}&pageSize=100")).Should().BeEquivalentTo(new[] { r1, r2 });
        (await IdsAsync($"/api/admin/requests?clientId={c2}&pageSize=100")).Should().BeEquivalentTo(new[] { r2, r3 });
    }

    [Fact]
    public async Task Project_detail_lists_the_live_quotations_of_each_deliverable()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var projectId = await CreateProjectAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo", LogoServiceId);
        var sentId = await CreateQuotationAsync(projectId);
        var sent = await SendAsync(sentId);
        var draftId = await CreateQuotationAsync(projectId);

        var quotedIn = await QuotedInAsync(projectId, logo);

        quotedIn.Select(x => x.GetProperty("quotationId").GetGuid()).Should().Equal(sentId, draftId);
        quotedIn[0].GetProperty("status").GetString().Should().Be("SENT");
        quotedIn[0].GetProperty("number").GetString().Should().Be(sent.GetProperty("number").GetString());
        quotedIn[0].GetProperty("version").GetInt32().Should().Be(1);
        quotedIn[0].GetProperty("kind").GetString().Should().Be("INITIAL");
        quotedIn[1].GetProperty("status").GetString().Should().Be("DRAFT");
        quotedIn[1].GetProperty("number").ValueKind.Should().Be(JsonValueKind.Null);

        // REJECTED is no longer live
        (await Client.PostAsJsonAsync($"/api/admin/quotations/{sentId}/reject", new { reason = "Ngân sách không đủ", rowVersion = Rv(sent) }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await QuotedInAsync(projectId, logo)).Select(x => x.GetProperty("quotationId").GetGuid()).Should().Equal(draftId);
    }

    private async Task<List<JsonElement>> QuotedInAsync(Guid projectId, Guid deliverableId)
    {
        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}");
        return detail.GetProperty("deliverables").EnumerateArray()
            .First(d => d.GetProperty("id").GetGuid() == deliverableId)
            .GetProperty("quotedIn").EnumerateArray().ToList();
    }
}
