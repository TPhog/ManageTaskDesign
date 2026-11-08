using System.Net;
using System.Text.Json;
using FreelanceFlow.IntegrationTests.Images;

namespace FreelanceFlow.IntegrationTests.Portal;

public class PortalImagesTests(AppFactory factory) : ImageTestBase(factory)
{
    private static Guid FirstId(JsonElement images) => images[0].GetProperty("id").GetGuid();

    [Fact]
    public async Task Client_sees_visible_task_and_progress_previews_but_never_originals_hidden_or_orphaned_ones()
    {
        await LoginAsync();
        var (clientId, token) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án gallery");
        var logo = await CreateDeliverableAsync(projectId, "Logo chính");
        var task = await CreateTaskAsync(projectId, "SECRET-TASK-TITLE", logo);
        var visible = FirstId(await UploadTaskImagesAsync(Client, task, TestImages.Png(800, 600)));

        var hiddenForm = TestImages.Form(TestImages.Png(40, 40));
        hiddenForm.Add(new StringContent("false"), "visibleToClient");
        var hiddenResp = await Client.PostAsync($"/api/admin/tasks/{task}/images", hiddenForm);
        hiddenResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var hidden = FirstId(await hiddenResp.Content.ReadFromJsonAsync<JsonElement>());

        var doomedTask = await CreateTaskAsync(projectId, "Task sẽ xoá");
        var orphan = FirstId(await UploadTaskImagesAsync(Client, doomedTask, TestImages.Png(30, 30)));
        (await Client.DeleteAsync($"/api/admin/tasks/{doomedTask}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var progressForm = TestImages.Form(TestImages.Png(50, 50));
        progressForm.Add(new StringContent("Đã xong phác thảo"), "message");
        var progress = await Client.PostAsync($"/api/admin/projects/{projectId}/progress-updates", progressForm);
        progress.StatusCode.Should().Be(HttpStatusCode.Created);
        var progressImage = FirstId((await progress.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("images"));

        var portal = await OpenPortalSessionAsync(token);
        var galleryResp = await portal.GetAsync($"/api/portal/projects/{projectId}/images");
        galleryResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await galleryResp.Content.ReadAsStringAsync();
        foreach (var forbidden in new[] { "originalUrl", "originalName", "ownerId", "SECRET-TASK-TITLE", "anh-1.png", task.ToString() })
            body.Should().NotContain(forbidden);
        var gallery = JsonDocument.Parse(body).RootElement;
        gallery.EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Equal([progressImage, visible], "newest first");
        var taskItem = gallery.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == visible);
        taskItem.GetProperty("source").GetString().Should().Be("TASK");
        taskItem.GetProperty("deliverableName").GetString().Should().Be("Logo chính");
        taskItem.GetProperty("previewUrl").GetString().Should().Be($"/api/portal/images/{visible}/preview");
        var progressItem = gallery.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == progressImage);
        progressItem.GetProperty("source").GetString().Should().Be("PROGRESS_UPDATE");
        progressItem.GetProperty("deliverableName").ValueKind.Should().Be(JsonValueKind.Null);

        var preview = await portal.GetAsync($"/api/portal/images/{visible}/preview");
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        using (var decoded = TestImages.Decode(await preview.Content.ReadAsByteArrayAsync()))
            decoded.Width.Should().Be(800, "the client sees full resolution, watermarked");
        (await portal.GetAsync($"/api/portal/images/{visible}/original")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await portal.GetAsync($"/api/portal/images/{hidden}/thumb")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await portal.GetAsync($"/api/portal/images/{orphan}/thumb")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var timeline = await portal.GetFromJsonAsync<JsonElement>($"/api/portal/projects/{projectId}/timeline");
        var update = timeline.GetProperty("items").EnumerateArray()
            .Single(t => t.GetProperty("message").GetString() == "Đã xong phác thảo");
        var timelineImage = update.GetProperty("images").EnumerateArray().Should().ContainSingle().Subject;
        timelineImage.GetProperty("id").GetGuid().Should().Be(progressImage);
        timelineImage.GetProperty("previewUrl").GetString().Should().Be($"/api/portal/images/{progressImage}/preview");

        (await Client.DeleteAsync($"/api/admin/images/{visible}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await portal.GetAsync($"/api/portal/images/{visible}/preview")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
