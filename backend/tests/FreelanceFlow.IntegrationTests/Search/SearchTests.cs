using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Search;

/// <summary>docs/08 §2.1 quick search. Each test uses its own random token so tests in this class never see each other's rows.</summary>
public class SearchTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private static string Token() => "k" + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task Requires_a_designer_session()
    {
        var resp = await Client.GetAsync("/api/admin/search?q=abc");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("   ")]
    [InlineData("")]
    public async Task Too_short_query_returns_empty_groups(string q)
    {
        await LoginAsync();
        var result = await SearchAsync(q);

        result.GetProperty("clients").GetArrayLength().Should().Be(0);
        result.GetProperty("projects").GetArrayLength().Should().Be(0);
        result.GetProperty("quotations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Search_short_term_returns_all_groups_empty()   // P14 DF-26
    {
        await LoginAsync();
        var res = await SearchAsync("a");
        foreach (var g in new[] { "clients", "projects", "quotations", "tasks", "requests" })
            res.GetProperty(g).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Search_finds_tasks_by_title_ignoring_accents()   // P14 DF-26
    {
        await LoginAsync();
        var tok = Token();
        var projectId = await CreateProjectAsync(await CreateClientAsync($"Khách {tok}"), $"Cà phê Sương Mai {tok}");
        var created = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/tasks", new { title = $"Phác thảo logo {tok}" });
        created.EnsureSuccessStatusCode();
        var taskId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var res = await SearchAsync($"phac thao logo {tok}");

        var tasks = res.GetProperty("tasks").EnumerateArray().ToList();
        tasks.Should().ContainSingle();
        tasks[0].GetProperty("id").GetGuid().Should().Be(taskId);
        tasks[0].GetProperty("projectId").GetGuid().Should().Be(projectId);
        tasks[0].GetProperty("title").GetString().Should().Be($"Phác thảo logo {tok}");
        tasks[0].GetProperty("projectName").GetString().Should().Be($"Cà phê Sương Mai {tok}");
        tasks[0].GetProperty("status").GetString().Should().Be("TODO");
        Ids(res, "projects").Should().BeEmpty();   // the project's name does not contain the task's words
    }

    [Fact]
    public async Task Search_finds_client_requests_by_content_with_an_excerpt()   // P14 DF-26
    {
        await LoginAsync();
        var tok = Token();
        var clientId = await CreateClientAsync($"Khách yêu cầu {tok}");
        var projectId = await CreateProjectAsync(clientId, $"Dự án yêu cầu {tok}");
        var content = $"Đổi màu logo sang xanh lá {tok} " + new string('x', 200);
        var created = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/requests", new { content, source = "ZALO" });
        created.EnsureSuccessStatusCode();
        var requestId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("request").GetProperty("id").GetGuid();

        var res = await SearchAsync($"xanh la {tok}");

        var requests = res.GetProperty("requests").EnumerateArray().ToList();
        requests.Should().ContainSingle();
        var hit = requests[0];
        hit.GetProperty("id").GetGuid().Should().Be(requestId);
        hit.GetProperty("projectId").GetGuid().Should().Be(projectId);
        hit.GetProperty("projectName").GetString().Should().Be($"Dự án yêu cầu {tok}");
        hit.GetProperty("clientName").GetString().Should().Be($"Khách yêu cầu {tok}");
        hit.GetProperty("status").GetString().Should().Be("NEW");
        hit.GetProperty("excerpt").GetString().Should().Be(content[..120]).And.Contain(tok);
    }

    [Fact]
    public async Task Too_long_query_is_rejected()
    {
        await LoginAsync();
        var resp = await Client.GetAsync($"/api/admin/search?q={new string('x', 101)}");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("q", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Clients_match_name_company_and_phone_ignoring_case_and_diacritics()
    {
        await LoginAsync();
        var tok = Token();
        var phone = "09" + Random.Shared.Next(10_000_000, 99_999_999);
        var id = await CreateClientAsync($"Nguyễn Thị Hoà {tok}", company: $"Cà Phê Đậm {tok}", phone: phone);

        Ids(await SearchAsync($"NGUYEN thi hoa {tok}"), "clients").Should().Contain(id);
        Ids(await SearchAsync($"ca phe dam {tok}"), "clients").Should().Contain(id);
        Ids(await SearchAsync(phone[3..]), "clients").Should().Contain(id);
    }

    [Fact]
    public async Task Phone_search_ignores_spaces_dots_and_dashes_on_both_sides()
    {
        await LoginAsync();
        var tok = Token();
        var d = Random.Shared.Next(100, 999).ToString();
        var spaced = await CreateClientAsync($"Spaced {tok}", phone: $"0901 {d} 567");
        var dashed = await CreateClientAsync($"Dashed {tok}", phone: $"0902-{d}-567");

        Ids(await SearchAsync($"0901{d}"), "clients").Should().Contain(spaced);
        Ids(await SearchAsync($"0901 {d}"), "clients").Should().Contain(spaced);
        Ids(await SearchAsync($"0902.{d}.567"), "clients").Should().Contain(dashed);
        Ids(await SearchAsync($"0902{d}567"), "clients").Should().Contain(dashed);
    }

    [Fact]
    public async Task Each_group_matches_only_its_own_columns()   // no cross-entity leakage
    {
        await LoginAsync();
        var clientTok = Token();
        var projectTok = Token();
        var notesTok = Token();
        var clientId = await CreateClientAsync($"Khách {clientTok}", notes: $"ghi chú nội bộ {notesTok}");
        var projectId = await CreateProjectAsync(clientId, $"Dự án {projectTok}");

        var byClient = await SearchAsync(clientTok);
        Ids(byClient, "clients").Should().Equal(clientId);
        Ids(byClient, "projects").Should().BeEmpty();      // the client's projects are not returned by its name
        Ids(byClient, "quotations").Should().BeEmpty();

        var byProject = await SearchAsync($"du an {projectTok}");
        Ids(byProject, "projects").Should().Equal(projectId);
        Ids(byProject, "clients").Should().BeEmpty();
        byProject.GetProperty("projects")[0].GetProperty("clientName").GetString().Should().Be($"Khách {clientTok}");

        var byNotes = await SearchAsync(notesTok);         // internal notes are never searched
        Ids(byNotes, "clients").Should().BeEmpty();
        Ids(byNotes, "projects").Should().BeEmpty();
    }

    [Fact]
    public async Task Quotations_match_number_without_dashes_or_title_and_skip_superseded()
    {
        await LoginAsync();
        var tok = Token();
        var projectId = await CreateProjectAsync(await CreateClientAsync($"Khách báo giá {tok}"), $"Dự án báo giá {tok}");
        var (supersededId, liveId, draftId, number) = await SeedQuotationChainAsync(projectId, tok);
        var compact = number.Replace("-", "");

        var byCompact = await SearchAsync(compact);
        Ids(byCompact, "quotations").Should().Equal(liveId);
        var hit = byCompact.GetProperty("quotations")[0];
        hit.GetProperty("number").GetString().Should().Be(number);
        hit.GetProperty("version").GetInt32().Should().Be(2);
        hit.GetProperty("effectiveStatus").GetString().Should().Be("SENT");
        hit.GetProperty("total").GetInt64().Should().Be(1_200_000);
        hit.GetProperty("projectName").GetString().Should().Be($"Dự án báo giá {tok}");

        Ids(await SearchAsync(number[3..]), "quotations").Should().Contain(liveId).And.NotContain(supersededId);   // "2031-0042"
        Ids(await SearchAsync($"nhap {tok}"), "quotations").Should().Equal(draftId);                                // draft has no number yet
    }

    [Fact]
    public async Task Prefix_matches_rank_first_and_each_group_is_capped_at_five()
    {
        await LoginAsync();
        var tok = Token();
        await CreateClientAsync($"Beta {tok}");
        var prefixed = await CreateClientAsync($"{tok} Alpha");
        for (var i = 0; i < 5; i++) await CreateClientAsync($"Gamma {tok} {i}");

        var clients = (await SearchAsync(tok)).GetProperty("clients").EnumerateArray().ToList();

        clients.Should().HaveCount(5);
        clients[0].GetProperty("id").GetGuid().Should().Be(prefixed);
        clients[1].GetProperty("name").GetString().Should().Be($"Beta {tok}");
    }

    [Fact]
    public async Task Like_wildcards_typed_by_the_user_are_literal()
    {
        await LoginAsync();
        await CreateClientAsync($"Wildcard {Token()}");

        var result = await SearchAsync("%_");

        result.GetProperty("clients").GetArrayLength().Should().Be(0);
        result.GetProperty("projects").GetArrayLength().Should().Be(0);
        result.GetProperty("quotations").GetArrayLength().Should().Be(0);
    }

    // ---------- helpers ----------

    private async Task<JsonElement> SearchAsync(string q)
    {
        var resp = await Client.GetAsync($"/api/admin/search?q={Uri.EscapeDataString(q)}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static List<Guid> Ids(JsonElement result, string group) =>
        result.GetProperty(group).EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

    private async Task<Guid> CreateClientAsync(string name, string? company = null, string? phone = null, string? notes = null)
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/clients", new { name, company, phone, notes });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateProjectAsync(Guid clientId, string name)
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/projects", new { clientId, name, designType = "LOGO" });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>v1 SUPERSEDED + v2 SENT of one number, plus a DRAFT without number titled "Nháp {tok}" (inserts are not locked).</summary>
    private async Task<(Guid Superseded, Guid Live, Guid Draft, string Number)> SeedQuotationChainAsync(Guid projectId, string tok)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayVn;
        var number = Quotation.FormatNumber("BG", 2031, Random.Shared.Next(1, 9999));
        var v1 = new Quotation
        {
            ProjectId = projectId, Kind = QuotationKinds.Initial, Number = number, Version = 1, Title = $"Báo giá cũ {tok}",
            Status = QuotationStatuses.Superseded, ValidityDays = 7, IssueDate = today.AddDays(-10), ExpiryDate = today.AddDays(-3),
            Subtotal = 1_000_000, Total = 1_000_000,
        };
        var v2 = new Quotation
        {
            ProjectId = projectId, Kind = QuotationKinds.Initial, Number = number, Version = 2, PreviousVersionId = v1.Id,
            Title = $"Báo giá mới {tok}", Status = QuotationStatuses.Sent, ValidityDays = 7, IssueDate = today,
            ExpiryDate = today.AddDays(7), Subtotal = 1_200_000, Total = 1_200_000,
        };
        var draft = new Quotation { ProjectId = projectId, Kind = QuotationKinds.Initial, Title = $"Nháp {tok}", ValidityDays = 7 };
        db.Quotations.AddRange(v1, v2, draft);
        await db.SaveChangesAsync();
        return (v1.Id, v2.Id, draft.Id, number);
    }
}
