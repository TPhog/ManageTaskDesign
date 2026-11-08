using System.Net;
using System.Text.Json;
using SkiaSharp;

namespace FreelanceFlow.IntegrationTests.Images;

public class WatermarkTests(AppFactory factory) : ImageTestBase(factory)
{
    [Fact]
    public async Task Watermark_settings_round_trip_and_validate()
    {
        await LoginAsync();
        var initial = await Client.GetFromJsonAsync<JsonElement>("/api/admin/settings/watermark");
        initial.GetProperty("enabled").GetBoolean().Should().BeTrue();
        initial.GetProperty("opacityPercent").GetInt32().Should().Be(18);
        initial.GetProperty("text").ValueKind.Should().Be(JsonValueKind.Null);
        initial.GetProperty("effectiveText").GetString().Should().EndWith("· Bản xem trước");

        var put = await Client.PutAsJsonAsync("/api/admin/settings/watermark", new { enabled = true, text = "  Studio Test  ", opacityPercent = 30 });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await put.Content.ReadFromJsonAsync<JsonElement>();
        saved.GetProperty("text").GetString().Should().Be("Studio Test");
        saved.GetProperty("effectiveText").GetString().Should().Be("Studio Test");
        saved.GetProperty("opacityPercent").GetInt32().Should().Be(30);

        async Task Expect400(object body, string field)
        {
            var resp = await Client.PutAsJsonAsync("/api/admin/settings/watermark", body);
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, field);
            (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue(field);
        }
        await Expect400(new { enabled = true, text = (string?)null, opacityPercent = 9 }, "opacityPercent");
        await Expect400(new { enabled = true, text = new string('x', 101), opacityPercent = 18 }, "text");
        await Expect400(new { text = (string?)null, opacityPercent = 18 }, "enabled");
    }

    [Fact]
    public async Task Designer_uploads_are_watermarked_only_while_enabled()
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var taskId = await CreateTaskAsync(await CreateProjectAsync(clientId, "Dự án watermark"));
        var gray = TestImages.Png(600, 400, new SKColor(128, 128, 128));

        await SetWatermarkAsync(enabled: true, opacity: 40);
        var marked = (await UploadTaskImagesAsync(Client, taskId, gray))[0].GetProperty("id").GetGuid();
        await SetWatermarkAsync(enabled: false, opacity: 18);
        var plain = (await UploadTaskImagesAsync(Client, taskId, gray))[0].GetProperty("id").GetGuid();
        await SetWatermarkAsync(enabled: true, opacity: 18);

        async Task<SKBitmap> Variant(Guid id, string variant) =>
            TestImages.Decode(await Client.GetByteArrayAsync($"/api/admin/images/{id}/{variant}"));
        using var markedOriginal = await Variant(marked, "original");
        using var markedPreview = await Variant(marked, "preview");
        using var plainOriginal = await Variant(plain, "original");
        using var plainPreview = await Variant(plain, "preview");
        TestImages.DifferingPixelRatio(markedOriginal, markedPreview).Should().BeGreaterThan(0.02);
        TestImages.DifferingPixelRatio(plainOriginal, plainPreview).Should().BeLessThan(0.01);
    }
}
