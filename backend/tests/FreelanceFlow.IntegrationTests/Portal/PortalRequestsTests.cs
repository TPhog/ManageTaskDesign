using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Features.Images;
using FreelanceFlow.IntegrationTests.Images;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Portal;

public class PortalRequestsTests(AppFactory factory) : PortalTestBase(factory)
{
    private async Task<(HttpClient Portal, Guid ClientId, Guid ProjectId)> ArrangeAsync(string projectName = "Dự án gửi yêu cầu")
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, projectName);
        return (await OpenPortalSessionAsync(token), clientId, projectId);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient portal, Guid projectId, string content, Guid? deliverableId = null) =>
        portal.PostAsync($"/api/portal/projects/{projectId}/requests", RequestForm(content, deliverableId));

    private static async Task<Guid> CreatedIdAsync(Task<HttpResponseMessage> pending)
    {
        var resp = await pending;
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<int> NewCountAsync() =>
        (await Client.GetFromJsonAsync<JsonElement>("/api/admin/requests/new-count")).GetProperty("count").GetInt32();

    [Fact]
    public async Task Client_request_is_stored_as_a_portal_request_and_reaches_the_inbox()   // REQ-01, AC-R1 (text only)
    {
        var (portal, _, projectId) = await ArrangeAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo");
        var countBefore = await NewCountAsync();

        var resp = await SendAsync(portal, projectId, "  Đổi màu chữ sang xanh navy  ", logo);

        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var created = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        created.GetProperty("projectId").GetGuid().Should().Be(projectId);
        created.GetProperty("projectName").GetString().Should().Be("Dự án gửi yêu cầu");
        created.GetProperty("deliverableName").GetString().Should().Be("Logo");
        created.GetProperty("content").GetString().Should().Be("Đổi màu chữ sang xanh navy");
        created.GetProperty("status").GetString().Should().Be("NEW");
        created.GetProperty("attachments").GetArrayLength().Should().Be(0);
        created.TryGetProperty("source", out _).Should().BeFalse();

        var inbox = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/requests?status=NEW&projectId={projectId}");
        var item = inbox.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == id);
        item.GetProperty("source").GetString().Should().Be("PORTAL");
        item.GetProperty("createdBy").GetString().Should().Be("CLIENT");
        (await NewCountAsync()).Should().Be(countBefore + 1);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.ActivityLogs.AsNoTracking().SingleAsync(a => a.EntityId == id);
        log.Action.Should().Be("request.created");
        log.ActorType.Should().Be("CLIENT");
        log.VisibleToClient.Should().BeFalse();
    }

    [Fact]
    public async Task Blank_content_foreign_deliverable_or_json_body_are_400()
    {
        var (portal, _, projectId) = await ArrangeAsync();
        var (otherClient, _) = await CreateClientWithPortalLinkAsync();
        var foreign = await CreateDeliverableAsync(await CreateProjectAsync(otherClient, "Dự án khách khác"), "Logo khác");

        async Task Expect400(HttpContent body, string field)
        {
            var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", body);
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, field);
            (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
                .TryGetProperty(field, out _).Should().BeTrue(field);
        }

        await Expect400(RequestForm("   "), "content");
        await Expect400(RequestForm(new string('a', 5001)), "content");
        await Expect400(RequestForm("Sửa logo", foreign), "deliverableId");
        await Expect400(JsonContent.Create(new { content = "Sửa logo" }), "content");
        (await portal.GetFromJsonAsync<JsonElement>("/api/portal/requests")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Cancelled_deliverable_is_refused_like_a_foreign_one()
    {
        var (portal, _, projectId) = await ArrangeAsync();
        var logo = await CreateDeliverableAsync(projectId, "Logo");
        await MoveDeliverableAsync(logo, "CANCELLED");

        var resp = await SendAsync(portal, projectId, "Sửa logo", logo);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
            .TryGetProperty("deliverableId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Malformed_multipart_is_400_not_500()
    {
        var (portal, _, projectId) = await ArrangeAsync();
        var body = new StringContent("--xyz\r\nContent-Disposition: form-data; name=\"content\"\r\n\r\ntruncated");
        body.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=xyz");

        var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", body);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Body_over_the_portal_upload_limit_is_413_not_500()
    {
        var (portal, _, projectId) = await ArrangeAsync();
        var form = RequestForm("Sửa logo");
        form.Add(new ByteArrayContent(TestImages.Png(8, 8)), "files", "qua-lon.png");
        form.Headers.ContentLength = ImageLimits.MaxPortalRequestBodyBytes + 1;   // declared, not allocated

        var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", form);

        resp.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge, await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Completed_or_cancelled_projects_refuse_new_requests_but_stay_visible()   // BR-21, AC-R2
    {
        var (portal, clientId, cancelled) = await ArrangeAsync();
        await ChangeProjectStatusAsync(cancelled, new { to = "CANCELLED", reason = "Khách huỷ" });
        var completed = await CreateProjectAsync(clientId, "Dự án đã xong");
        await ChangeProjectStatusAsync(completed, new { to = "IN_PROGRESS", confirmWarnings = true });
        await ChangeProjectStatusAsync(completed, new { to = "COMPLETED", confirmWarnings = true });

        foreach (var projectId in new[] { cancelled, completed })
        {
            var resp = await SendAsync(portal, projectId, "Sửa thêm");
            resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("BUSINESS_RULE");
            (await portal.GetAsync($"/api/portal/projects/{projectId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Twenty_first_request_of_the_day_is_rate_limited_per_client()   // BR-54, AC-R5
    {
        var (portal, clientId, projectId) = await ArrangeAsync();
        var second = await CreateProjectAsync(clientId, "Dự án thứ hai");
        (await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/requests",
            new { content = "Ghi hộ qua Zalo", source = "ZALO" })).EnsureSuccessStatusCode(); // designer-recorded: not counted

        for (var i = 1; i <= 20; i++)
            (await SendAsync(portal, i % 2 == 0 ? projectId : second, $"Yêu cầu {i}"))
                .StatusCode.Should().Be(HttpStatusCode.Created, $"request {i}");

        var limited = await SendAsync(portal, projectId, "Yêu cầu 21");
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await limited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("RATE_LIMITED");

        var (otherClient, otherToken) = await CreateClientWithPortalLinkAsync();
        var otherPortal = await OpenPortalSessionAsync(otherToken);
        (await SendAsync(otherPortal, await CreateProjectAsync(otherClient, "Khách khác"), "Không bị ảnh hưởng"))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // at the limit the body is never parsed: a malformed multipart is 429, not 400
        var garbage = new StringContent("--xyz\r\nContent-Disposition: form-data; name=\"content\"\r\n\r\ntruncated");
        garbage.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=xyz");
        (await portal.PostAsync($"/api/portal/projects/{projectId}/requests", garbage))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Full_storage_is_507_before_a_large_body_is_read_but_text_only_requests_still_pass()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var ownProject = await CreateProjectAsync(clientId, "Dự án đầy");
        var projectId = ownProject;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Files.Add(FreelanceFlow.Api.Domain.Files.ProjectFile.CreateImage(projectId, FreelanceFlow.Api.Domain.Files.ImageOwnerTypes.Task,
                Guid.NewGuid(), null, "DESIGNER", false, "filler.webp", 1, 1, 10_000, 1, 1, new byte[32], DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        await using var host = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.PostConfigure<FreelanceFlow.Api.Infrastructure.Storage.StorageOptions>(o => o.QuotaBytes = 1)));
        var portal = await OpenPortalSessionAsync(token, host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, HandleCookies = false }));

        var big = RequestForm("Có ảnh");
        big.Add(new ByteArrayContent(TestImages.Png(8, 8)), "files", "a.png");
        big.Headers.ContentLength = 1_000_000;   // declared: more than a text-only body
        var blocked = await portal.PostAsync($"/api/portal/projects/{ownProject}/requests", big);
        blocked.StatusCode.Should().Be(HttpStatusCode.InsufficientStorage);
        (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("STORAGE_FULL");

        (await SendAsync(portal, ownProject, "Chỉ chữ")).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Full_storage_accepts_a_template_request_text_only_even_with_a_required_images_question()
    {
        await LoginAsync();
        var created = await Client.PostAsJsonAsync("/api/admin/request-templates", new
        {
            name = "Cần ảnh", description = "", designType = "OTHER", serviceId = (Guid?)null, active = true,
            fields = new object[]
            {
                new { key = "idea", label = "Ý tưởng", type = "SHORT_TEXT", required = true, help = (string?)null, options = Array.Empty<string>() },
                new { key = "refs", label = "Ảnh mẫu", type = "IMAGES", required = true, help = (string?)null, options = Array.Empty<string>() },
            },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var templateId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án đầy");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Files.Add(FreelanceFlow.Api.Domain.Files.ProjectFile.CreateImage(projectId, FreelanceFlow.Api.Domain.Files.ImageOwnerTypes.Task,
                Guid.NewGuid(), null, "DESIGNER", false, "filler.webp", 1, 1, 10_000, 1, 1, new byte[32], DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        await using var host = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.PostConfigure<FreelanceFlow.Api.Infrastructure.Storage.StorageOptions>(o => o.QuotaBytes = 1)));
        var portal = await OpenPortalSessionAsync(token, host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, HandleCookies = false }));

        var form = new MultipartFormDataContent
        {
            { new StringContent(templateId.ToString()), "templateId" },
            { new StringContent("""{"idea":"Logo quán"}"""), "answers" },
        };
        var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", form);

        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Client_sees_own_requests_newest_first_with_status_and_decline_reason()   // REQ-06, AC-R4
    {
        var (portal, clientId, projectId) = await ArrangeAsync();
        var other = await CreateProjectAsync(clientId, "Dự án khác");
        var first = await CreatedIdAsync(SendAsync(portal, projectId, "Yêu cầu đầu"));
        var declined = await CreatedIdAsync(SendAsync(portal, projectId, "Làm thêm 10 banner miễn phí"));
        var elsewhere = await CreatedIdAsync(SendAsync(portal, other, "Yêu cầu dự án khác"));
        var rowVersion = (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/requests/{declined}"))
            .GetProperty("request").GetProperty("rowVersion").GetUInt32();
        (await Client.PostAsJsonAsync($"/api/admin/requests/{declined}/decline",
            new { reason = "Ngoài phạm vi hợp đồng", rowVersion })).EnsureSuccessStatusCode();

        var all = await portal.GetFromJsonAsync<JsonElement>("/api/portal/requests");
        all.EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).Should().Equal(elsewhere, declined, first);

        var mine = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/requests?projectId={projectId}");
        mine.EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).Should().Equal(declined, first);
        mine[0].GetProperty("status").GetString().Should().Be("DECLINED");
        mine[0].GetProperty("declineReason").GetString().Should().Be("Ngoài phạm vi hợp đồng");
        mine[1].GetProperty("status").GetString().Should().Be("NEW");
        mine[1].GetProperty("declineReason").ValueKind.Should().Be(JsonValueKind.Null);
        (await portal.GetAsync($"/api/portal/requests?projectId={Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_without_csrf_token_is_403_and_designer_cookie_is_401()   // docs/09 §4, 07-security §3
    {
        var (portal, _, projectId) = await ArrangeAsync();

        using var noCsrf = new HttpRequestMessage(HttpMethod.Post, $"/api/portal/projects/{projectId}/requests")
        {
            Content = RequestForm("Không có CSRF"),
        };
        noCsrf.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", portal.DefaultRequestHeaders.GetValues("Cookie")));
        noCsrf.Headers.TryAddWithoutValidation("Origin", "http://localhost");
        (await CreateBareClient().SendAsync(noCsrf)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await Client.PostAsync($"/api/portal/projects/{projectId}/requests", RequestForm("Designer")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await portal.GetFromJsonAsync<JsonElement>("/api/portal/requests")).GetArrayLength().Should().Be(0);
    }
}
