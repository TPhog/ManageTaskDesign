using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Intake;

public class PublicIntakeTests(AppFactory factory) : IntakeTestBase(factory)
{
    [Fact]
    public async Task Form_shows_the_studio_contact_and_active_templates_only()   // AC-INT3
    {
        var slug = await EnableIntakeAsync();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = await db.Settings.FirstAsync();
            settings.DisplayName = "Studio Mây";
            settings.ContactPhone = "0901 000 111";
            settings.BankAccountNo = "0123456789";
            await db.SaveChangesAsync();
        }

        var resp = await Anonymous().GetAsync($"/api/public/intake/{slug}");
        var body = await resp.Content.ReadAsStringAsync();

        resp.Headers.CacheControl!.NoStore.Should().BeTrue();
        resp.Headers.GetValues("X-Robots-Tag").Should().Equal("noindex");
        var form = JsonDocument.Parse(body).RootElement;
        form.GetProperty("studio").GetProperty("displayName").GetString().Should().Be("Studio Mây");
        form.GetProperty("studio").GetProperty("contactPhone").GetString().Should().Be("0901 000 111");
        form.GetProperty("templates").GetArrayLength().Should().BeGreaterThanOrEqualTo(6);
        form.GetProperty("maxImages").GetInt32().Should().Be(3);
        form.GetProperty("formToken").GetString().Should().NotBeNullOrEmpty();
        body.Should().NotContain("0123456789").And.NotContain("\"serviceId\"").And.NotContain("\"designType\"");
    }

    [Fact]
    public async Task Valid_submission_with_a_photo_is_stored_and_nothing_is_echoed()   // AC-INT4, BR-134/135
    {
        var slug = await EnableIntakeAsync();
        var anon = Anonymous();
        var form = await GetFormAsync(anon, slug);

        var resp = await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(form.GetProperty("formToken").GetString()!,
            TemplateId(form, "Menu cửa hàng"), MenuAnswers, "Quán ở Thủ Đức", contactEmail: "lan@example.com", images: 1));

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await resp.Content.ReadAsStringAsync()).Should().BeEmpty();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.IntakeSubmissions.AsNoTracking().OrderByDescending(s => s.CreatedAt).FirstAsync();
        saved.Status.Should().Be("NEW");
        saved.TemplateName.Should().Be("Menu cửa hàng");
        saved.ContactEmail.Should().Be("lan@example.com");
        saved.Body.Should().StartWith("Mẫu: Menu cửa hàng\n- Loại cửa hàng: Quán cà phê\n- Dạng menu: 2 mặt\n- Kích thước: A4 (210 × 297 mm)\n- Số món (ước lượng): 24\n- Ảnh menu cũ / logo / ảnh món: 1 ảnh đính kèm")
            .And.EndWith("Ghi chú thêm:\nQuán ở Thủ Đức");
        var image = await db.Files.AsNoTracking().SingleAsync(f => f.OwnerId == saved.Id);
        (image.OwnerType, image.ProjectId, image.UploadedBy, image.Kind).Should().Be((ImageOwnerTypes.IntakeSubmission, (Guid?)null, "CLIENT", "REFERENCE"));
        image.StorageKey.Should().StartWith($"intake/{saved.Id}/");
        (await db.ActivityLogs.AsNoTracking().SingleAsync(a => a.Action == "intake.submitted" && a.EntityId == saved.Id))
            .ActorType.Should().Be("CLIENT");

        var alert = (await Factory.Notifications.WaitForAsync(n => n.Title == "Khách mới gửi yêu cầu" && n.Url!.EndsWith($"/admin/intake/{saved.Id}"))).Single();
        alert.Fields!.Should().Contain(f => f.Name == "Mẫu" && f.Value == "Menu cửa hàng");
        alert.Fields!.Should().NotContain(f => f.Value.Contains("0909") || f.Value.Contains("lan@example.com"));
    }

    [Fact]
    public async Task Free_text_needs_a_description()   // BR-134
    {
        var slug = await EnableIntakeAsync();
        var anon = Anonymous();
        var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;

        var empty = await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token));
        (await empty.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("note", out _).Should().BeTrue();

        (await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "Cần thiết kế thiệp cưới")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().IntakeSubmissions.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt).FirstAsync()).Body.Should().Be("Cần thiết kế thiệp cưới");
    }

    [Fact]
    public async Task Contact_answers_and_photo_count_are_validated_per_field()
    {
        var slug = await EnableIntakeAsync();
        var anon = Anonymous();
        var form = await GetFormAsync(anon, slug);
        var token = form.GetProperty("formToken").GetString()!;
        var before = await SubmissionCountAsync();

        var resp = await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, TemplateId(form, "Menu cửa hàng"),
            new Dictionary<string, object?> { ["shop_type"] = "Quán cà phê", ["format"] = "2 mặt", ["item_count"] = 24 },
            contactName: " ", contactPhone: "123", contactEmail: "lan@", images: 4));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").EnumerateObject().Select(e => e.Name)
            .Should().BeEquivalentTo(["contactName", "contactPhone", "contactEmail", "answers.size", "files"]);
        (await SubmissionCountAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Designer_cookie_is_ignored_and_no_csrf_token_is_needed()   // 07-security §4.5
    {
        var slug = await EnableIntakeAsync();
        var anon = Anonymous();
        var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;
        var before = await SubmissionCountAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/public/intake/{slug}") { Content = IntakeForm(token, note: "Từ máy designer") };
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", Client.DefaultRequestHeaders.GetValues("Cookie")));
        request.Headers.TryAddWithoutValidation("Origin", "null");   // what a browser sends under Referrer-Policy: no-referrer

        var resp = await anon.SendAsync(request);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await resp.Content.ReadAsStringAsync()).Should().BeEmpty();
        (await SubmissionCountAsync()).Should().Be(before + 1);
    }
}
