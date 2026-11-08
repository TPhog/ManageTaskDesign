using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace FreelanceFlow.IntegrationTests.Images;

public class ImageVariantPolicyTests(AppFactory factory) : ImageTestBase(factory)
{
    private string VariantPath(Guid projectId, Guid id, string variant) =>
        Path.Combine(Factory.FilesRoot, "projects", projectId.ToString(), id.ToString(), variant + ".webp");

    private async Task<(Guid ClientId, string Token, Guid ProjectId)> ArrangeAsync(string name)
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        return (clientId, token, await CreateProjectAsync(clientId, name + " " + Guid.NewGuid()));
    }

    [Fact]
    public async Task Designer_preview_is_capped_at_2560_and_variants_are_cached_as_immutable()
    {
        var (_, _, projectId) = await ArrangeAsync("Dự án biến thể");
        var taskId = await CreateTaskAsync(projectId);

        var image = (await UploadTaskImagesAsync(Client, taskId, TestImages.Png(3200, 800)))[0];
        var id = image.GetProperty("id").GetGuid();

        image.GetProperty("width").GetInt32().Should().Be(3000);
        image.GetProperty("originalDeletedAt").ValueKind.Should().Be(JsonValueKind.Null);
        image.GetProperty("duplicateOf").ValueKind.Should().Be(JsonValueKind.Null);
        var preview = await Client.GetAsync($"/api/admin/images/{id}/preview");
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Headers.CacheControl!.Private.Should().BeTrue();
        preview.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromDays(365));
        preview.Headers.CacheControl.Extensions.Should().Contain(e => e.Name == "immutable");
        preview.Headers.ETag!.Tag.Should().Be($"\"{id:N}-preview\"");
        using (var decoded = TestImages.Decode(await preview.Content.ReadAsByteArrayAsync()))
            (decoded.Width, decoded.Height).Should().Be((2560, 640));
        using (var original = TestImages.Decode(await Client.GetByteArrayAsync($"/api/admin/images/{id}/original")))
            (original.Width, original.Height).Should().Be((3000, 750));
    }

    [Fact]
    public async Task Client_photo_is_stored_once_and_its_preview_streams_that_object()
    {
        var (_, token, projectId) = await ArrangeAsync("Dự án ảnh khách");
        var portal = await OpenPortalSessionAsync(token);
        var form = RequestForm("Làm theo ảnh mẫu này");
        form.Add(TestImages.Part(TestImages.Png(300, 200)), "files", "mau.png");

        var resp = await portal.PostAsync($"/api/portal/projects/{projectId}/requests", form);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var id = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attachments")[0].GetProperty("id").GetGuid();

        File.Exists(VariantPath(projectId, id, "original")).Should().BeTrue();
        File.Exists(VariantPath(projectId, id, "thumb")).Should().BeTrue();
        File.Exists(VariantPath(projectId, id, "preview")).Should().BeFalse("the client's own photo is not stored twice");
        var originalBytes = await File.ReadAllBytesAsync(VariantPath(projectId, id, "original"));
        var thumbBytes = await File.ReadAllBytesAsync(VariantPath(projectId, id, "thumb"));
        using (var scope = Factory.Services.CreateScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Files.AsNoTracking().SingleAsync(f => f.Id == id);
            row.PreviewIsOriginal.Should().BeTrue();
            row.PreviewBytes.Should().Be(0);
            row.SizeBytes.Should().Be(originalBytes.Length + thumbBytes.Length);
        }

        var viaPortal = await portal.GetAsync($"/api/portal/images/{id}/preview");
        viaPortal.StatusCode.Should().Be(HttpStatusCode.OK);
        viaPortal.Headers.ETag!.Tag.Should().Be($"\"{id:N}-original\"");
        (await viaPortal.Content.ReadAsByteArrayAsync()).Should().Equal(originalBytes);
        (await portal.GetAsync($"/api/portal/images/{id}/original")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetByteArrayAsync($"/api/admin/images/{id}/preview")).Should().Equal(originalBytes);
    }

    [Fact]
    public async Task Identical_uploads_in_the_same_project_are_flagged_but_still_stored()
    {
        var (clientId, _, projectId) = await ArrangeAsync("Dự án ảnh trùng");
        var logo = await CreateTaskAsync(projectId, "Logo chính");
        var other = await CreateTaskAsync(projectId, "Bộ nhận diện");
        var png = TestImages.Png(120, 90, new SKColor(12, 34, 56));

        var first = (await UploadTaskImagesAsync(Client, logo, png))[0];
        first.GetProperty("duplicateOf").ValueKind.Should().Be(JsonValueKind.Null);
        var firstId = first.GetProperty("id").GetGuid();

        var again = (await UploadTaskImagesAsync(Client, other, png))[0];
        again.GetProperty("id").GetGuid().Should().NotBe(firstId, "still stored — no shared objects");
        var dup = again.GetProperty("duplicateOf");
        dup.GetProperty("id").GetGuid().Should().Be(firstId);
        dup.GetProperty("ownerType").GetString().Should().Be("TASK");
        dup.GetProperty("ownerId").GetGuid().Should().Be(logo);
        dup.GetProperty("taskTitle").GetString().Should().Be("Logo chính");
        dup.GetProperty("sameUpload").GetBoolean().Should().BeFalse();

        var batch = await UploadTaskImagesAsync(Client, other, TestImages.Png(40, 40, SKColors.Gold), TestImages.Png(40, 40, SKColors.Gold));
        batch[0].GetProperty("duplicateOf").ValueKind.Should().Be(JsonValueKind.Null);
        batch[1].GetProperty("duplicateOf").GetProperty("sameUpload").GetBoolean().Should().BeTrue();
        batch[1].GetProperty("duplicateOf").GetProperty("id").GetGuid().Should().Be(batch[0].GetProperty("id").GetGuid());

        var progressForm = TestImages.Form(png);
        progressForm.Add(new StringContent("Bản phác thảo"), "message");
        var progress = await Client.PostAsync($"/api/admin/projects/{projectId}/progress-updates", progressForm);
        progress.StatusCode.Should().Be(HttpStatusCode.Created, await progress.Content.ReadAsStringAsync());
        (await progress.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("images")[0]
            .GetProperty("duplicateOf").GetProperty("id").GetGuid().Should().Be(firstId, "earliest live copy");

        var elsewhere = await CreateProjectAsync(clientId, "Dự án khác " + Guid.NewGuid());
        (await UploadTaskImagesAsync(Client, await CreateTaskAsync(elsewhere), png))[0]
            .GetProperty("duplicateOf").ValueKind.Should().Be(JsonValueKind.Null, "only the same project counts");
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/tasks/{other}/images"))[0]
            .GetProperty("duplicateOf").ValueKind.Should().Be(JsonValueKind.Null, "lists never carry the warning");
    }
}
