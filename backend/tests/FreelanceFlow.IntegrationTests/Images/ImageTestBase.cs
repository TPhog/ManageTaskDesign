using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.IntegrationTests.Portal;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Images;

public abstract class ImageTestBase(AppFactory factory) : PortalTestBase(factory)
{
    protected async Task<Guid> CreateTaskAsync(Guid projectId, string title = "Thiết kế logo", Guid? deliverableId = null)
    {
        var resp = await Client.PostAsJsonAsync($"/api/admin/projects/{projectId}/tasks", new { title, deliverableId });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    protected static async Task<JsonElement> UploadTaskImagesAsync(HttpClient client, Guid taskId, params byte[][] images)
    {
        var resp = await client.PostAsync($"/api/admin/tasks/{taskId}/images", TestImages.Form(images));
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected static async Task<JsonElement> UsageAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/api/admin/storage/usage");

    /// <summary>A stored-looking image row (no objects on disk) that occupies <paramref name="bytes"/> of quota.</summary>
    protected async Task<Guid> InsertFillerImageAsync(Guid projectId, Guid taskId, long bytes)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var filler = ProjectFile.CreateImage(projectId, ImageOwnerTypes.Task, taskId, null, "DESIGNER", false, "filler.webp",
            1, 1, bytes - 2, 1, 1, new byte[32], DateTimeOffset.UtcNow);
        db.Files.Add(filler);
        await db.SaveChangesAsync();
        return filler.Id;
    }

    /// <summary>Same as LoginAsync, for a client of another host (WithWebHostBuilder) sharing the database.</summary>
    protected static async Task LoginOnAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = AppFactory.TestUsername, password = AppFactory.TestPassword });
        resp.EnsureSuccessStatusCode();
        var cookies = resp.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToList();
        var admin = cookies.First(c => c.StartsWith("ff_admin=", StringComparison.Ordinal));
        var csrf = cookies.First(c => c.StartsWith("ff_csrf=", StringComparison.Ordinal));
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{admin}; {csrf}");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-CSRF-Token", Uri.UnescapeDataString(csrf["ff_csrf=".Length..]));
        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "http://localhost");
    }

    protected async Task SetWatermarkAsync(bool enabled, int opacity, string? text = null)
    {
        var resp = await Client.PutAsJsonAsync("/api/admin/settings/watermark", new { enabled, text, opacityPercent = opacity });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
    }
}
