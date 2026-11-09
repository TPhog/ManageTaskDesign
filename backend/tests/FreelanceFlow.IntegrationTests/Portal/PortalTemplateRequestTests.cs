using System.Net;
using System.Text.Json;
using FreelanceFlow.IntegrationTests.Images;

namespace FreelanceFlow.IntegrationTests.Portal;

public class PortalTemplateRequestTests(AppFactory factory) : PortalTestBase(factory)
{
    private static readonly Dictionary<string, object?> PosterAnswers = new()
    {
        ["purpose"] = "Khai trương",
        ["format"] = "In ấn",
        ["size"] = new { preset = "A3 (297 × 420 mm)" },
        ["headline"] = "Khai trương chi nhánh 2",
        ["budget"] = 1_500_000,
    };

    private static MultipartFormDataContent TemplateForm(Guid templateId, object answers, string? note = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(templateId.ToString()), "templateId" },
            { new StringContent(JsonSerializer.Serialize(answers)), "answers" },
        };
        if (note is not null) form.Add(new StringContent(note), "content");
        return form;
    }

    private async Task<(HttpClient Portal, Guid ProjectId, Guid PosterId)> ArrangeAsync()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án poster " + Guid.NewGuid());
        var portal = await OpenPortalSessionAsync(token);
        var templates = await portal.GetFromJsonAsync<JsonElement>("/api/portal/request-templates");
        var poster = templates.EnumerateArray().Single(t => t.GetProperty("name").GetString() == "Poster").GetProperty("id").GetGuid();
        return (portal, projectId, poster);
    }

    [Fact]
    public async Task Client_lists_active_templates_without_internal_fields()   // AC-TPL3
    {
        var (portal, _, _) = await ArrangeAsync();
        var admin = await Client.GetFromJsonAsync<JsonElement>("/api/admin/request-templates");
        var logo = admin.EnumerateArray().Single(t => t.GetProperty("name").GetString() == "Logo");
        var hide = await Client.PutAsJsonAsync($"/api/admin/request-templates/{logo.GetProperty("id").GetGuid()}", new
        {
            name = "Logo", description = logo.GetProperty("description").GetString(), designType = "LOGO", serviceId = (Guid?)null,
            active = false, fields = logo.GetProperty("fields"), rowVersion = logo.GetProperty("rowVersion").GetUInt32(),
        });
        hide.EnsureSuccessStatusCode();

        var resp = await portal.GetAsync("/api/portal/request-templates");
        var body = await resp.Content.ReadAsStringAsync();

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContain("\"serviceId\"").And.NotContain("\"designType\"").And.NotContain("\"active\"");
        JsonDocument.Parse(body).RootElement.EnumerateArray().Select(t => t.GetProperty("name").GetString())
            .Should().Contain("Poster").And.NotContain("Logo");
    }

    [Fact]
    public async Task Template_request_stores_the_rendered_content_and_the_answers()   // AC-TPL4, BR-131/132
    {
        var (portal, projectId, poster) = await ArrangeAsync();

        var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", TemplateForm(poster, PosterAnswers, "Gửi trước thứ 6"));

        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var created = await resp.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("templateName").GetString().Should().Be("Poster");
        created.GetProperty("content").GetString().Should().Be(
            "Mẫu: Poster\n- Mục đích poster: Khai trương\n- Hình thức: In ấn\n- Kích thước: A3 (297 × 420 mm)\n" +
            "- Tiêu đề chính: Khai trương chi nhánh 2\n- Ngân sách dự kiến (đồng): 1.500.000\n\nGhi chú thêm:\nGửi trước thứ 6");

        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/requests/{created.GetProperty("id").GetGuid()}");
        var request = detail.GetProperty("request");
        request.GetProperty("templateName").GetString().Should().Be("Poster");
        request.GetProperty("answers").EnumerateArray().Select(a => a.GetProperty("display").GetString())
            .Should().Equal("Khai trương", "In ấn", "A3 (297 × 420 mm)", "Khai trương chi nhánh 2", "1.500.000");

        (await Factory.Notifications.WaitForAsync(n => n.Title == "Yêu cầu mới từ khách"
                && n.Fields!.Any(f => f.Name == "Mẫu" && f.Value == "Poster")))
            .Should().NotBeEmpty();
    }

    [Fact]
    public async Task Missing_or_wrong_answers_are_reported_per_question_and_nothing_is_stored()
    {
        var (portal, projectId, poster) = await ArrangeAsync();

        var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests",
            TemplateForm(poster, new Dictionary<string, object?> { ["purpose"] = "Khai trương", ["format"] = "Tờ rơi" }));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.EnumerateObject().Select(e => e.Name).Should().BeEquivalentTo(["answers.format", "answers.size", "answers.headline"]);
        (await portal.GetFromJsonAsync<JsonElement>($"/api/portal/requests?projectId={projectId}")).GetArrayLength().Should().Be(0);

        var malformed = new MultipartFormDataContent { { new StringContent(poster.ToString()), "templateId" }, { new StringContent("[1"), "answers" } };
        var bad = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", malformed);
        (await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("answers", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_or_hidden_template_is_refused()
    {
        var (portal, projectId, _) = await ArrangeAsync();

        var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", TemplateForm(Guid.NewGuid(), PosterAnswers));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("templateId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Required_image_question_needs_a_photo()   // BR-131
    {
        var (portal, projectId, _) = await ArrangeAsync();
        var created = await Client.PostAsJsonAsync("/api/admin/request-templates", new
        {
            name = "Chỉ ảnh " + Guid.NewGuid(), description = "", designType = "OTHER", serviceId = (Guid?)null, active = true,
            fields = new[] { new { key = "refs", label = "Ảnh mẫu", type = "IMAGES", required = true, help = (string?)null, options = Array.Empty<string>() } },
        });
        var templateId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var none = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", TemplateForm(templateId, new { }));
        (await none.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("answers.refs")[0]
            .GetString().Should().Be("Đính kèm ít nhất 1 ảnh");

        var withPhoto = TemplateForm(templateId, new { });
        withPhoto.Add(TestImages.Part(TestImages.Png(8, 8)), "files", "mau.png");
        var ok = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", withPhoto);
        ok.StatusCode.Should().Be(HttpStatusCode.Created, await ok.Content.ReadAsStringAsync());
        var dto = await ok.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("content").GetString().Should().EndWith("- Ảnh mẫu: 1 ảnh đính kèm");
        dto.GetProperty("attachments").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task A_template_request_with_nothing_answered_and_no_note_is_refused()   // review P13-BE #10
    {
        var (portal, projectId, _) = await ArrangeAsync();
        var created = await Client.PostAsJsonAsync("/api/admin/request-templates", new
        {
            name = "Tuỳ chọn " + Guid.NewGuid(), description = "", designType = "OTHER", serviceId = (Guid?)null, active = true,
            fields = new[] { new { key = "idea", label = "Ý tưởng", type = "SHORT_TEXT", required = false, help = (string?)null, options = Array.Empty<string>() } },
        });
        var templateId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", TemplateForm(templateId, new { }, note: "  "));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").EnumerateObject().Select(e => e.Name)
            .Should().Equal("answers");
        (await portal.PostAsync($"/api/portal/projects/{projectId}/requests", TemplateForm(templateId, new { }, note: "Chỉ ghi chú")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Free_text_request_is_unchanged()
    {
        var (portal, projectId, _) = await ArrangeAsync();
        var form = new MultipartFormDataContent { { new StringContent("Đổi màu chữ"), "content" } };

        var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", form);

        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("templateName").ValueKind.Should().Be(JsonValueKind.Null);
        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/requests/{dto.GetProperty("id").GetGuid()}");
        detail.GetProperty("request").GetProperty("answers").GetArrayLength().Should().Be(0);
    }
}
