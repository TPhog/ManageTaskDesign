using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.RequestTemplates;

public class RequestTemplatesAdminTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private static object Body(string name = "Tờ rơi", string designType = "PRINT", Guid? serviceId = null, bool active = true,
        object[]? fields = null, uint? rowVersion = null) => new
    {
        name, description = "Tờ rơi A5 phát tay", designType, serviceId, active, rowVersion,
        fields = fields ?? new object[]
        {
            new { key = "headline", label = "Tiêu đề", type = "SHORT_TEXT", required = true, help = (string?)null, options = Array.Empty<string>() },
            new { key = "size", label = "Kích thước", type = "SIZE", required = true, help = "Chọn hoặc tự nhập", options = new[] { "A5 (148 × 210 mm)" } },
        },
    };

    private async Task<JsonElement> CreateAsync(object body)
    {
        var resp = await Client.PostAsJsonAsync("/api/admin/request-templates", body);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task List_returns_the_seeded_presets_in_order()   // AC-TPL1
    {
        await LoginAsync();

        var list = await Client.GetFromJsonAsync<JsonElement>("/api/admin/request-templates");

        list.EnumerateArray().Take(6).Select(t => t.GetProperty("name").GetString()).Should().Equal(
            "Banner quảng cáo", "Poster", "Menu cửa hàng", "Banner sự kiện / chương trình", "Bài đăng mạng xã hội", "Logo");
        var banner = list[0];
        banner.GetProperty("serviceName").ValueKind.Should().Be(JsonValueKind.Null);
        banner.GetProperty("rowVersion").GetUInt32().Should().BeGreaterThan(0);
        banner.GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("type").GetString()).Should().Contain(["SIZE", "IMAGES", "DATE"]);
    }

    [Fact]
    public async Task Create_appends_at_the_end_and_writes_activity()   // AC-TPL2
    {
        await LoginAsync();

        var created = await CreateAsync(Body());

        created.GetProperty("sortOrder").GetInt32().Should().BeGreaterThan(60);
        created.GetProperty("fields")[1].GetProperty("options")[0].GetString().Should().Be("A5 (148 × 210 mm)");
        using var scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().ActivityLogs.AsNoTracking()
            .AnyAsync(a => a.Action == "request_template.created" && a.EntityId == created.GetProperty("id").GetGuid()))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Create_validates_fields_design_type_and_service()
    {
        await LoginAsync();

        async Task Expect400(object body, string key, string? contains = null)
        {
            var resp = await Client.PostAsJsonAsync("/api/admin/request-templates", body);
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var errors = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            errors.TryGetProperty(key, out var messages).Should().BeTrue(errors.ToString());
            if (contains is not null) messages[0].GetString().Should().Contain(contains);
        }

        await Expect400(Body(fields: [new { key = "style", label = "Phong cách", type = "SINGLE_CHOICE", required = false, help = (string?)null, options = Array.Empty<string>() }]),
            "fields", "Câu hỏi 1: cần 1–20 lựa chọn");
        await Expect400(Body(designType: "FLYER"), "designType");
        await Expect400(Body(serviceId: Guid.NewGuid()), "serviceId");
        await Expect400(Body(name: "  "), "name");
    }

    [Fact]
    public async Task Update_links_a_service_and_a_stale_row_version_is_a_conflict()   // AC-TPL2
    {
        await LoginAsync();
        var service = await Client.PostAsJsonAsync("/api/admin/services",
            new { name = "Tờ rơi A5", description = "", basePrice = 800_000L, defaultIncludedRounds = 2, extraRoundPrice = 150_000L });
        var serviceId = (await service.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var created = await CreateAsync(Body());
        var id = created.GetProperty("id").GetGuid();
        var oldVersion = created.GetProperty("rowVersion").GetUInt32();

        var ok = await Client.PutAsJsonAsync($"/api/admin/request-templates/{id}", Body(serviceId: serviceId, active: false, rowVersion: oldVersion));
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        var updated = await ok.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("serviceName").GetString().Should().Be("Tờ rơi A5");
        updated.GetProperty("active").GetBoolean().Should().BeFalse();

        var stale = await Client.PutAsJsonAsync($"/api/admin/request-templates/{id}", Body(name: "Khác", rowVersion: oldVersion));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Delete_and_reorder_need_known_ids()   // AC-TPL2
    {
        await LoginAsync();
        var id = (await CreateAsync(Body(name: "Xoá tôi"))).GetProperty("id").GetGuid();

        (await Client.DeleteAsync($"/api/admin/request-templates/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/admin/request-templates/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ids = (await Client.GetFromJsonAsync<JsonElement>("/api/admin/request-templates"))
            .EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
        (await Client.PostAsJsonAsync("/api/admin/request-templates/reorder", new { ids = ids.Skip(1) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ids.Reverse();
        (await Client.PostAsJsonAsync("/api/admin/request-templates/reorder", new { ids }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetFromJsonAsync<JsonElement>("/api/admin/request-templates"))
            .EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).Should().Equal(ids);

        ids.Reverse();   // restore: List_returns_the_seeded_presets_in_order shares this database
        (await Client.PostAsJsonAsync("/api/admin/request-templates/reorder", new { ids }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Admin_routes_need_the_designer() =>
        (await Client.GetAsync("/api/admin/request-templates")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
