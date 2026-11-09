using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.IntegrationTests.Images;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Intake;

/// <summary>Plan 13 helpers: the designer turns the public form on; anonymous clients carry no cookie at all.</summary>
public abstract class IntakeTestBase(AppFactory factory) : IntegrationTestBase(factory)
{
    protected HttpClient Anonymous(WebApplicationFactory<Program>? host = null) =>
        (host ?? Factory).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    /// <summary>Signs the designer in and turns the form on (idempotent); returns the current slug.</summary>
    protected async Task<string> EnableIntakeAsync()
    {
        await LoginAsync();
        var resp = await Client.PutAsJsonAsync("/api/admin/settings/intake", new { enabled = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
    }

    protected static async Task<JsonElement> GetFormAsync(HttpClient anon, string slug)
    {
        var resp = await anon.GetAsync($"/api/public/intake/{slug}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected static Guid TemplateId(JsonElement form, string name) =>
        form.GetProperty("templates").EnumerateArray().Single(t => t.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();

    protected static readonly Dictionary<string, object?> MenuAnswers = new()
    {
        ["shop_type"] = "Quán cà phê",
        ["format"] = "2 mặt",
        ["size"] = new { preset = "A4 (210 × 297 mm)" },
        ["item_count"] = 24,
    };

    protected static MultipartFormDataContent IntakeForm(string formToken, Guid? templateId = null, object? answers = null,
        string? note = null, string contactName = "Chị Lan", string contactPhone = "0909 123 456", string? contactEmail = null,
        string? honeypot = null, int images = 0)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(formToken), "formToken" },
            { new StringContent(contactName), "contactName" },
            { new StringContent(contactPhone), "contactPhone" },
            { new StringContent(honeypot ?? ""), "website" },
        };
        if (contactEmail is not null) form.Add(new StringContent(contactEmail), "contactEmail");
        if (templateId is { } id) form.Add(new StringContent(id.ToString()), "templateId");
        if (answers is not null) form.Add(new StringContent(JsonSerializer.Serialize(answers)), "answers");
        if (note is not null) form.Add(new StringContent(note), "note");
        for (var i = 0; i < images; i++) form.Add(TestImages.Part(TestImages.Png(16, 12)), "files", $"menu-{i + 1}.png");
        return form;
    }

    /// <summary>Posts a valid "Menu cửa hàng" submission; returns the new submission id.</summary>
    protected async Task<Guid> SubmitMenuAsync(HttpClient anon, string slug, int images = 1)
    {
        var form = await GetFormAsync(anon, slug);
        var resp = await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(form.GetProperty("formToken").GetString()!,
            TemplateId(form, "Menu cửa hàng"), MenuAnswers, "Quán ở Thủ Đức", images: images));
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent, await resp.Content.ReadAsStringAsync());
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().IntakeSubmissions.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt).Select(s => s.Id).FirstAsync();
    }

    protected async Task<int> SubmissionCountAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().IntakeSubmissions.CountAsync();
    }

    protected sealed class MutableClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
        public DateOnly TodayVn => DateOnly.FromDateTime(UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
    }
}
