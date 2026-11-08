using System.Net;
using System.Text;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Images;

public class TaskImagesTests(AppFactory factory) : ImageTestBase(factory)
{
    private async Task<(Guid ProjectId, Guid TaskId)> ArrangeAsync()
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án ảnh " + Guid.NewGuid());
        return (projectId, await CreateTaskAsync(projectId));
    }

    private string VariantPath(Guid projectId, Guid id, string variant) =>
        Path.Combine(Factory.FilesRoot, "projects", projectId.ToString(), id.ToString(), variant + ".webp");

    [Fact]
    public async Task Upload_stores_three_webp_variants_and_counts_all_their_bytes()
    {
        var (projectId, taskId) = await ArrangeAsync();
        var usedBefore = (await UsageAsync(Client)).GetProperty("usedBytes").GetInt64();

        var images = await UploadTaskImagesAsync(Client, taskId, TestImages.Png(800, 600));

        var image = images.EnumerateArray().Should().ContainSingle().Subject;
        var id = image.GetProperty("id").GetGuid();
        image.GetProperty("projectId").GetGuid().Should().Be(projectId);
        image.GetProperty("ownerType").GetString().Should().Be("TASK");
        image.GetProperty("ownerId").GetGuid().Should().Be(taskId);
        image.GetProperty("originalName").GetString().Should().Be("anh-1.png");
        image.GetProperty("width").GetInt32().Should().Be(800);
        image.GetProperty("height").GetInt32().Should().Be(600);
        image.GetProperty("visibleToClient").GetBoolean().Should().BeTrue();
        image.GetProperty("uploadedBy").GetString().Should().Be("DESIGNER");
        image.GetProperty("thumbUrl").GetString().Should().Be($"/api/admin/images/{id}/thumb");
        image.GetProperty("previewUrl").GetString().Should().Be($"/api/admin/images/{id}/preview");
        image.GetProperty("originalUrl").GetString().Should().Be($"/api/admin/images/{id}/original");

        long onDisk = 0;
        foreach (var variant in new[] { "original", "preview", "thumb" })
        {
            var bytes = await File.ReadAllBytesAsync(VariantPath(projectId, id, variant));
            Encoding.ASCII.GetString(bytes, 8, 4).Should().Be("WEBP", variant);
            onDisk += bytes.Length;
        }
        image.GetProperty("sizeBytes").GetInt64().Should().Be(onDisk);
        (await UsageAsync(Client)).GetProperty("usedBytes").GetInt64().Should().Be(usedBefore + onDisk);

        var thumb = await Client.GetAsync($"/api/admin/images/{id}/thumb");
        thumb.StatusCode.Should().Be(HttpStatusCode.OK);
        thumb.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        thumb.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        thumb.Headers.CacheControl!.Private.Should().BeTrue();
        thumb.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromDays(365));
        var etag = thumb.Headers.ETag!;
        etag.Tag.Should().Be($"\"{id:N}-thumb\"");
        using (var conditional = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/images/{id}/thumb"))
        {
            conditional.Headers.IfNoneMatch.Add(etag);
            (await Client.SendAsync(conditional)).StatusCode.Should().Be(HttpStatusCode.NotModified);
        }
        using (var decoded = TestImages.Decode(await thumb.Content.ReadAsByteArrayAsync()))
            (decoded.Width, decoded.Height).Should().Be((480, 360));

        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{taskId}/images")).GetArrayLength().Should().Be(1);
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/images?ownerType=TASK")).GetArrayLength().Should().Be(1);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Files.AsNoTracking().SingleAsync(f => f.Id == id);
        (row.OriginalBytes + row.PreviewBytes + row.ThumbBytes).Should().Be(onDisk);
        row.Kind.Should().Be("PREVIEW");
        row.ContentType.Should().Be("image/webp");
        (await db.ActivityLogs.AsNoTracking().AnyAsync(a => a.EntityId == taskId && a.Action == "image.uploaded")).Should().BeTrue();
    }

    [Fact]
    public async Task Non_images_oversized_files_and_bad_counts_are_refused_and_nothing_stays_stored()
    {
        var (projectId, taskId) = await ArrangeAsync();
        var url = $"/api/admin/tasks/{taskId}/images";

        async Task ExpectCode(HttpContent body, HttpStatusCode status, string code)
        {
            var resp = await Client.PostAsync(url, body);
            resp.StatusCode.Should().Be(status, await resp.Content.ReadAsStringAsync());
            (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be(code);
        }

        var pdf = new MultipartFormDataContent { { TestImages.Part("%PDF-1.7 fake"u8.ToArray()), "files", "hoa-don.png" } };
        await ExpectCode(pdf, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_FILE_TYPE");

        var mixed = TestImages.Form(TestImages.Png(32, 32));   // first image is fine, second is not → whole batch rolled back
        mixed.Add(TestImages.Part("GIF89a...."u8.ToArray(), "image/gif"), "files", "dong.gif");
        await ExpectCode(mixed, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_FILE_TYPE");

        var huge = new byte[21 * 1024 * 1024];
        TestImages.Png(4, 4).CopyTo(huge, 0);
        await ExpectCode(new MultipartFormDataContent { { TestImages.Part(huge), "files", "qua-lon.png" } },
            HttpStatusCode.RequestEntityTooLarge, "FILE_TOO_LARGE");

        var none = await Client.PostAsync(url, new MultipartFormDataContent { { new StringContent("true"), "visibleToClient" } });
        none.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await none.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("files", out _).Should().BeTrue();
        var eleven = await Client.PostAsync(url, TestImages.Form(Enumerable.Range(0, 11).Select(_ => TestImages.Png(4, 4)).ToArray()));
        eleven.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await Client.PostAsJsonAsync(url, new { files = "x" });
        json.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{taskId}/images")).GetArrayLength().Should().Be(0);
        var projectFolder = Path.Combine(Factory.FilesRoot, "projects", projectId.ToString());
        (Directory.Exists(projectFolder) ? Directory.EnumerateFiles(projectFolder, "*", SearchOption.AllDirectories) : [])
            .Should().BeEmpty("objects written before the failure are discarded");
    }

    [Fact]
    public async Task Hiding_and_deleting_an_image_frees_its_bytes_and_removes_its_objects()
    {
        var (projectId, taskId) = await ArrangeAsync();
        var image = (await UploadTaskImagesAsync(Client, taskId, TestImages.Png(100, 80)))[0];
        var id = image.GetProperty("id").GetGuid();
        var size = image.GetProperty("sizeBytes").GetInt64();

        var patch = await Client.PatchAsJsonAsync($"/api/admin/images/{id}", new { visibleToClient = false });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await patch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("visibleToClient").GetBoolean().Should().BeFalse();
        (await Client.PatchAsJsonAsync($"/api/admin/images/{id}", new { })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var before = (await UsageAsync(Client)).GetProperty("usedBytes").GetInt64();
        (await Client.DeleteAsync($"/api/admin/images/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await UsageAsync(Client)).GetProperty("usedBytes").GetInt64().Should().Be(before - size);
        foreach (var variant in new[] { "original", "preview", "thumb" })
            File.Exists(VariantPath(projectId, id, variant)).Should().BeFalse(variant);
        (await Client.GetAsync($"/api/admin/images/{id}/preview")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{taskId}/images")).GetArrayLength().Should().Be(0);
        (await Client.DeleteAsync($"/api/admin/images/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ActivityLogs.AsNoTracking().CountAsync(a => a.EntityId == id && (a.Action == "image.deleted" || a.Action == "image.visibility_changed")))
            .Should().Be(2);
        (await db.Files.AsNoTracking().SingleAsync(f => f.Id == id)).DeletedAt.Should().NotBeNull("soft delete keeps the row");
    }

    [Fact]
    public async Task Unknown_task_variant_or_owner_type_is_rejected_and_anonymous_callers_get_401()
    {
        var (projectId, taskId) = await ArrangeAsync();
        var id = (await UploadTaskImagesAsync(Client, taskId, TestImages.Png(16, 16)))[0].GetProperty("id").GetGuid();

        (await Client.PostAsync($"/api/admin/tasks/{Guid.NewGuid()}/images", TestImages.Form(TestImages.Png(4, 4))))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/admin/tasks/{Guid.NewGuid()}/images")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/admin/images/{id}/raw")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/admin/projects/{projectId}/images?ownerType=QUOTATION")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var anonymous = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        (await anonymous.GetAsync($"/api/admin/images/{id}/thumb")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/admin/storage/usage")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Missing_stored_object_is_a_404_that_browsers_may_not_cache()
    {
        var (projectId, taskId) = await ArrangeAsync();
        var id = (await UploadTaskImagesAsync(Client, taskId, TestImages.Png(40, 40)))[0].GetProperty("id").GetGuid();
        var ok = await Client.GetAsync($"/api/admin/images/{id}/thumb");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        ok.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromDays(365));

        File.Delete(VariantPath(projectId, id, "thumb"));
        var missing = await Client.GetAsync($"/api/admin/images/{id}/thumb");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.Headers.CacheControl?.MaxAge.Should().BeNull();
        missing.Headers.Contains("ETag").Should().BeFalse();
    }

    [Fact]
    public async Task Blocked_storage_answers_507_before_the_body_is_read()
    {
        var (_, taskId) = await ArrangeAsync();
        await UploadTaskImagesAsync(Client, taskId, TestImages.Png(16, 16)); // usage > 1 byte
        await using var host = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.PostConfigure<FreelanceFlow.Api.Infrastructure.Storage.StorageOptions>(o => o.QuotaBytes = 1)));
        var admin = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        await LoginOnAsync(admin);

        var garbage = new StringContent("not multipart at all");
        garbage.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("multipart/form-data; boundary=xyz");
        var resp = await admin.PostAsync($"/api/admin/tasks/{taskId}/images", garbage);
        resp.StatusCode.Should().Be(HttpStatusCode.InsufficientStorage, "a body that would be a 400 is never parsed while storage is full");
    }

    [Fact]
    public async Task Declared_content_length_over_the_limit_is_413_payload_too_large()
    {
        var (_, taskId) = await ArrangeAsync();
        var body = TestImages.Form(TestImages.Png(16, 16));
        body.Headers.ContentLength = FreelanceFlow.Api.Features.Images.ImageLimits.MaxAdminBodyBytes + 1;
        var resp = await Client.PostAsync($"/api/admin/tasks/{taskId}/images", body);
        resp.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("PAYLOAD_TOO_LARGE");
    }
}
