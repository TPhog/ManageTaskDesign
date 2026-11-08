using System.Net;
using System.Text.Json;

namespace FreelanceFlow.IntegrationTests.Images;

public class StorageListTests(AppFactory factory) : ImageTestBase(factory)
{
    [Fact]
    public async Task Storage_list_filters_sorts_totals_and_bulk_delete_reports_skipped_ids()
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var done = await CreateProjectAsync(clientId, "Dự án đã xong " + Guid.NewGuid());
        var active = await CreateProjectAsync(clientId, "Dự án đang làm " + Guid.NewGuid());
        var doneTask = await CreateTaskAsync(done, "Vẽ logo");
        var activeTask = await CreateTaskAsync(active, "Dàn trang");
        var doneImage = (await UploadTaskImagesAsync(Client, doneTask, TestImages.Png(300, 200)))[0];
        var activeImages = await UploadTaskImagesAsync(Client, activeTask, TestImages.Png(900, 700), TestImages.Png(20, 20));
        await ChangeProjectStatusAsync(done, new { to = "IN_PROGRESS", confirmWarnings = true });
        await ChangeProjectStatusAsync(done, new { to = "COMPLETED", confirmWarnings = true });

        var ofDone = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/storage/images?projectId={done}");
        var item = ofDone.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("projectStatus").GetString().Should().Be("COMPLETED");
        item.GetProperty("taskTitle").GetString().Should().Be("Vẽ logo");
        item.GetProperty("thumbUrl").GetString().Should().StartWith("/api/admin/images/");
        ofDone.GetProperty("total").GetInt32().Should().Be(1);
        ofDone.GetProperty("totalBytes").GetInt64().Should().Be(doneImage.GetProperty("sizeBytes").GetInt64());

        var completed = await Client.GetFromJsonAsync<JsonElement>("/api/admin/storage/images?completedOnly=true&pageSize=100");
        var completedIds = completed.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        completedIds.Should().Contain(doneImage.GetProperty("id").GetGuid())
            .And.NotContain(activeImages[0].GetProperty("id").GetGuid());
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/storage/images?projectId={done}&olderThanMonths=6"))
            .GetProperty("total").GetInt32().Should().Be(0, "uploaded just now");

        var bySize = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/storage/images?projectId={active}&sort=size");
        var sizes = bySize.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sizeBytes").GetInt64()).ToList();
        sizes.Should().BeInDescendingOrder();
        var oldest = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/storage/images?projectId={active}&sort=oldest");
        oldest.GetProperty("items")[0].GetProperty("id").GetGuid().Should().Be(activeImages[0].GetProperty("id").GetGuid());
        (await Client.GetAsync("/api/admin/storage/images?sort=name")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetAsync("/api/admin/storage/images?olderThanMonths=121")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await Client.DeleteAsync($"/api/admin/tasks/{activeTask}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var orphaned = await Client.GetFromJsonAsync<JsonElement>($"/api/admin/storage/images?projectId={active}");
        orphaned.GetProperty("items").EnumerateArray().Should().HaveCount(2)
            .And.OnlyContain(i => i.GetProperty("taskTitle").ValueKind == JsonValueKind.Null, "deleted task — images stay until the designer deletes them");

        var bulk = await Client.PostAsJsonAsync("/api/admin/images/bulk-delete",
            new { ids = new[] { doneImage.GetProperty("id").GetGuid(), Guid.NewGuid() } });
        bulk.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await bulk.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("deleted").GetInt32().Should().Be(1);
        result.GetProperty("skipped").GetInt32().Should().Be(1);
        result.GetProperty("freedBytes").GetInt64().Should().Be(doneImage.GetProperty("sizeBytes").GetInt64());
        (await Client.PostAsJsonAsync("/api/admin/images/bulk-delete", new { ids = Array.Empty<Guid>() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetFromJsonAsync<JsonElement>($"/api/admin/storage/images?projectId={done}"))
            .GetProperty("total").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(-5)]
    [InlineData(0)]
    public async Task Extreme_page_numbers_are_clamped_instead_of_overflowing(int page)
    {
        await LoginAsync();
        var resp = await Client.GetAsync($"/api/admin/storage/images?page={page}&pageSize=100");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("page").GetInt32().Should().BeInRange(1, 100_000);
    }
}
