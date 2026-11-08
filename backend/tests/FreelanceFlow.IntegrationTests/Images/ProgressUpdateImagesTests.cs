using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Images;

public class ProgressUpdateImagesTests(AppFactory factory) : ImageTestBase(factory)
{
    [Fact]
    public async Task Progress_update_takes_json_or_multipart_and_its_images_belong_to_the_update()
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án tiến độ ảnh");
        var url = $"/api/admin/projects/{projectId}/progress-updates";

        var json = await Client.PostAsJsonAsync(url, new { message = "Chỉ có chữ" });
        json.StatusCode.Should().Be(HttpStatusCode.Created);
        (await json.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("images").GetArrayLength().Should().Be(0);

        var form = TestImages.Form(TestImages.Png(120, 90));
        form.Add(new StringContent("Đã xong phác thảo"), "message");
        var multipart = await Client.PostAsync(url, form);
        multipart.StatusCode.Should().Be(HttpStatusCode.Created, await multipart.Content.ReadAsStringAsync());
        var created = await multipart.Content.ReadFromJsonAsync<JsonElement>();
        var updateId = created.GetProperty("id").GetGuid();
        created.GetProperty("message").GetString().Should().Be("Đã xong phác thảo");
        var image = created.GetProperty("images").EnumerateArray().Should().ContainSingle().Subject;
        image.GetProperty("ownerType").GetString().Should().Be("PROGRESS_UPDATE");
        image.GetProperty("ownerId").GetGuid().Should().Be(updateId);
        image.GetProperty("visibleToClient").GetBoolean().Should().BeTrue();

        var activity = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/activity?visibleOnly=true");
        var entry = activity.GetProperty("items").EnumerateArray()
            .Single(a => a.GetProperty("clientMessage").GetString() == "Đã xong phác thảo");
        entry.GetProperty("entityType").GetString().Should().Be("progress_update");
        entry.GetProperty("entityId").GetGuid().Should().Be(updateId);
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/images?ownerType=PROGRESS_UPDATE"))
            .EnumerateArray().Select(i => i.GetProperty("ownerId").GetGuid()).Should().Equal(updateId);
        (await Client.PatchAsJsonAsync($"/api/admin/images/{image.GetProperty("id").GetGuid()}", new { visibleToClient = false }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "progress images are always shown");
    }

    [Fact]
    public async Task Refused_progress_updates_leave_no_timeline_entry_and_no_files()
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án tiến độ lỗi");
        var url = $"/api/admin/projects/{projectId}/progress-updates";

        var blank = TestImages.Form(TestImages.Png(8, 8));
        blank.Add(new StringContent("   "), "message");
        var r1 = await Client.PostAsync(url, blank);
        r1.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await r1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("message", out _).Should().BeTrue();

        var eleven = TestImages.Form(Enumerable.Range(0, 11).Select(_ => TestImages.Png(4, 4)).ToArray());
        eleven.Add(new StringContent("Nhiều ảnh quá"), "message");
        var r2 = await Client.PostAsync(url, eleven);
        r2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await r2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("files", out _).Should().BeTrue();

        var broken = new MultipartFormDataContent
        {
            { new StringContent("Ảnh hỏng"), "message" },
            { TestImages.Part("%PDF-1.7"u8.ToArray()), "files", "x.png" },
        };
        (await Client.PostAsync(url, broken)).StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);

        var activity = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/activity?visibleOnly=true");
        activity.GetProperty("items").GetArrayLength().Should().Be(0);
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{projectId}/images")).GetArrayLength().Should().Be(0);
    }
}
