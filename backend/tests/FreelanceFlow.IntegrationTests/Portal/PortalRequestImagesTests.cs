using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.IntegrationTests.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace FreelanceFlow.IntegrationTests.Portal;

public class PortalRequestImagesTests(AppFactory factory) : ImageTestBase(factory)
{
    private async Task<(HttpClient Portal, Guid ProjectId)> ArrangeAsync()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án ảnh yêu cầu");
        return (await OpenPortalSessionAsync(token), projectId);
    }

    private static MultipartFormDataContent RequestWithImages(string content, params byte[][] images)
    {
        var form = RequestForm(content);
        for (var i = 0; i < images.Length; i++) form.Add(TestImages.Part(images[i]), "files", $"mau-{i + 1}.png");
        return form;
    }

    [Fact]
    public async Task Client_photos_reach_the_designer_unwatermarked_and_only_their_sender_can_see_them()
    {
        var (portal, projectId) = await ArrangeAsync();
        await SetWatermarkAsync(enabled: true, opacity: 40); // client photos are exempt anyway
        var gray = TestImages.Png(300, 200, new SKColor(128, 128, 128));

        var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests",
            RequestWithImages("Đổi màu theo ảnh mẫu", gray, TestImages.Png(20, 20)));
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var created = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var requestId = created.GetProperty("id").GetGuid();
        var attachments = created.GetProperty("attachments");
        attachments.GetArrayLength().Should().Be(2);
        var first = attachments[0];
        var imageId = first.GetProperty("id").GetGuid();
        first.GetProperty("originalName").GetString().Should().Be("mau-1.png");
        first.GetProperty("thumbUrl").GetString().Should().Be($"/api/portal/images/{imageId}/thumb");
        first.GetProperty("previewUrl").GetString().Should().Be($"/api/portal/images/{imageId}/preview");
        first.TryGetProperty("originalUrl", out _).Should().BeFalse();

        (await portal.GetAsync($"/api/portal/images/{imageId}/thumb")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await portal.GetAsync($"/api/portal/images/{imageId}/original")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{projectId}/images")).GetArrayLength()
            .Should().Be(0, "request photos are listed with the request, not in the gallery");
        (await portal.GetFromJsonAsync<JsonElement>($"/api/portal/requests?projectId={projectId}"))[0]
            .GetProperty("attachments").GetArrayLength().Should().Be(2);

        var detail = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/requests/{requestId}");
        var adminAttachment = detail.GetProperty("request").GetProperty("attachments")[0];
        adminAttachment.GetProperty("originalUrl").GetString().Should().Be($"/api/admin/images/{imageId}/original");
        using var original = TestImages.Decode(await Client.GetByteArrayAsync($"/api/admin/images/{imageId}/original"));
        using var preview = TestImages.Decode(await Client.GetByteArrayAsync($"/api/admin/images/{imageId}/preview"));
        TestImages.DifferingPixelRatio(original, preview).Should().BeLessThan(0.01, "no watermark on client photos");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Files.AsNoTracking().SingleAsync(f => f.Id == imageId);
            row.OwnerType.Should().Be("CLIENT_REQUEST");
            row.OwnerId.Should().Be(requestId);
            row.RequestId.Should().Be(requestId);
            row.UploadedBy.Should().Be("CLIENT");
            row.VisibleToClient.Should().BeFalse();
            row.Kind.Should().Be("REFERENCE");
            (await db.ActivityLogs.AsNoTracking().AnyAsync(a => a.EntityId == requestId && a.Action == "image.uploaded" && a.ActorType == "CLIENT"))
                .Should().BeTrue();
        }
        await SetWatermarkAsync(enabled: true, opacity: 18);
    }

    [Fact]
    public async Task Too_many_or_non_image_attachments_create_no_request_and_other_clients_get_404()
    {
        var (portal, projectId) = await ArrangeAsync();
        var url = $"/api/portal/projects/{projectId}/requests";

        var six = await portal.PostAsync(url, RequestWithImages("Sáu ảnh", Enumerable.Range(0, 6).Select(_ => TestImages.Png(4, 4)).ToArray()));
        six.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await six.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("files", out _).Should().BeTrue();
        var pdf = RequestForm("Kèm hợp đồng");
        pdf.Add(TestImages.Part("%PDF-1.7"u8.ToArray()), "files", "hop-dong.png");
        var pdfResp = await portal.PostAsync(url, pdf);
        pdfResp.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await pdfResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("UNSUPPORTED_FILE_TYPE");
        (await portal.GetFromJsonAsync<JsonElement>("/api/portal/requests")).GetArrayLength().Should().Be(0);

        var ok = await portal.PostAsync(url, RequestWithImages("Một ảnh mẫu", TestImages.Png(10, 10)));
        ok.StatusCode.Should().Be(HttpStatusCode.Created);
        var imageId = (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attachments")[0].GetProperty("id").GetGuid();
        var (_, otherToken) = await CreateClientWithPortalLinkAsync();
        var other = await OpenPortalSessionAsync(otherToken);
        (await other.GetAsync($"/api/portal/images/{imageId}/thumb")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
