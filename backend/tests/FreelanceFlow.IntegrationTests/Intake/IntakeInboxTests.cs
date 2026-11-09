using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Intake;

public class IntakeInboxTests(AppFactory factory) : IntakeTestBase(factory)
{
    [Fact]
    public async Task New_submission_is_listed_and_counted()   // AC-INT7
    {
        var slug = await EnableIntakeAsync();
        var id = await SubmitMenuAsync(Anonymous(), slug);

        var page = await Client.GetFromJsonAsync<JsonElement>("/api/admin/intake");
        var item = page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);
        item.GetProperty("templateName").GetString().Should().Be("Menu cửa hàng");
        item.GetProperty("contactPhone").GetString().Should().Be("0909 123 456");
        item.GetProperty("excerpt").GetString().Should().StartWith("Mẫu: Menu cửa hàng - Loại cửa hàng: Quán cà phê");
        item.GetProperty("imageCount").GetInt32().Should().Be(1);
        (await Client.GetFromJsonAsync<JsonElement>("/api/admin/intake/new-count")).GetProperty("count").GetInt32().Should().BeGreaterThan(0);
        (await Client.GetFromJsonAsync<JsonElement>("/api/admin/intake?status=REJECTED")).GetProperty("items").EnumerateArray()
            .Should().NotContain(i => i.GetProperty("id").GetGuid() == id);
        (await Client.GetAsync("/api/admin/intake?status=SPAM")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Detail_shows_answers_photos_suggestions_and_matching_clients()   // AC-INT7, BR-136
    {
        var slug = await EnableIntakeAsync();
        var existing = await Client.PostAsJsonAsync("/api/admin/clients", new { name = "Lan cũ", phone = "+84 909 123 456" });
        var existingId = (await existing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var id = await SubmitMenuAsync(Anonymous(), slug);

        var d = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/intake/{id}");

        d.GetProperty("status").GetString().Should().Be("NEW");
        d.GetProperty("answers").EnumerateArray().Select(a => a.GetProperty("label").GetString())
            .Should().StartWith(["Loại cửa hàng", "Dạng menu", "Kích thước"]);
        d.GetProperty("note").GetString().Should().Be("Quán ở Thủ Đức");
        d.GetProperty("suggestedDesignType").GetString().Should().Be("PRINT");
        d.GetProperty("suggestedServiceId").ValueKind.Should().Be(JsonValueKind.Null);
        d.GetProperty("matchingClients").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).Should().Contain(existingId);
        var thumb = await Client.GetAsync(d.GetProperty("images")[0].GetProperty("thumbUrl").GetString());
        thumb.StatusCode.Should().Be(HttpStatusCode.OK);
        thumb.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        d.GetProperty("rowVersion").GetUInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Reject_keeps_an_internal_reason_and_blocks_conversion()   // AC-INT8
    {
        var slug = await EnableIntakeAsync();
        var id = await SubmitMenuAsync(Anonymous(), slug, images: 0);
        var version = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/intake/{id}")).GetProperty("rowVersion").GetUInt32();

        var stale = await Client.PostAsJsonAsync($"/api/admin/intake/{id}/reject", new { reason = "x", rowVersion = version + 1 });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var resp = await Client.PostAsJsonAsync($"/api/admin/intake/{id}/reject", new { reason = "Ngoài khả năng", rowVersion = version });

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var d = await resp.Content.ReadFromJsonAsync<JsonElement>();
        d.GetProperty("status").GetString().Should().Be("REJECTED");
        d.GetProperty("rejectReason").GetString().Should().Be("Ngoài khả năng");
        var convert = await Client.PostAsJsonAsync($"/api/admin/intake/{id}/convert", new
        {
            clientName = "Lan", projectName = "Menu", designType = "PRINT", rowVersion = d.GetProperty("rowVersion").GetUInt32(),
        });
        convert.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await convert.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public async Task Delete_removes_the_submission_and_frees_its_photos()   // AC-INT8
    {
        var slug = await EnableIntakeAsync();
        var id = await SubmitMenuAsync(Anonymous(), slug);
        var thumbUrl = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/intake/{id}")).GetProperty("images")[0].GetProperty("thumbUrl").GetString();

        (await Client.DeleteAsync($"/api/admin/intake/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Client.GetAsync($"/api/admin/intake/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync(thumbUrl)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Files.AsNoTracking().Where(f => f.OwnerId == id).AllAsync(f => f.DeletedAt != null)).Should().BeTrue();
        (await db.ActivityLogs.AsNoTracking().AnyAsync(a => a.Action == "intake.deleted" && a.EntityId == id)).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_racing_a_convert_is_a_conflict_and_keeps_everything()   // review P13-BE #4
    {
        var slug = await EnableIntakeAsync();
        var id = await SubmitMenuAsync(Anonymous(), slug);
        Factory.BeforeSave.RunOnNextSave(async () =>
        {
            using var other = Factory.Services.CreateScope();   // a convert (any update) commits between load and save
            await other.ServiceProvider.GetRequiredService<AppDbContext>().Database
                .ExecuteSqlInterpolatedAsync($"UPDATE intake_submissions SET note = note || '' WHERE id = {id}");
        });

        var resp = await Client.DeleteAsync($"/api/admin/intake/{id}");

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("CONCURRENCY_CONFLICT");
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.IntakeSubmissions.AsNoTracking().AnyAsync(s => s.Id == id)).Should().BeTrue();
        (await db.Files.AsNoTracking().Where(f => f.OwnerId == id).AllAsync(f => f.DeletedAt == null)).Should().BeTrue();
    }

    [Fact]
    public async Task Inbox_needs_the_designer() =>
        (await Anonymous().GetAsync("/api/admin/intake")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
