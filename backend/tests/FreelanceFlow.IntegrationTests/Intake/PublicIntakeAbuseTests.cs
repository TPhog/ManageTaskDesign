using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Infrastructure.Storage;
using FreelanceFlow.IntegrationTests.Images;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Intake;

public class PublicIntakeAbuseTests(AppFactory factory) : IntakeTestBase(factory)
{
    [Fact]
    public async Task Honeypot_is_answered_like_success_but_nothing_is_stored()   // AC-INT5, BR-138
    {
        var slug = await EnableIntakeAsync();
        var anon = Anonymous();
        var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;
        var before = await SubmissionCountAsync();

        var resp = await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "spam", honeypot: "http://spam.example"));

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SubmissionCountAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Filling_faster_than_the_minimum_is_dropped_silently()   // AC-INT5
    {
        var slug = await EnableIntakeAsync();
        await using var host = Factory.WithWebHostBuilder(b => b.UseSetting("PublicIntake:MinFillSeconds", "30"));
        var anon = Anonymous(host);
        var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;
        var before = await SubmissionCountAsync();

        (await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "quá nhanh"))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SubmissionCountAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Bad_foreign_or_expired_tokens_ask_to_reload()   // AC-INT5
    {
        var slug = await EnableIntakeAsync();
        var clock = new MutableClock();
        await using var host = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IClock>(clock)));
        var anon = Anonymous(host);
        var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;

        async Task ExpectExpired(string url, string formToken)
        {
            var resp = await anon.PostAsync(url, IntakeForm(formToken, note: "x"));
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("FORM_EXPIRED");
        }

        await ExpectExpired($"/api/public/intake/{slug}", "not-a-token");
        clock.UtcNow = clock.UtcNow.AddHours(25);
        await ExpectExpired($"/api/public/intake/{slug}", token);

        clock.UtcNow = DateTimeOffset.UtcNow;
        var fresh = (await (await Client.PostAsync("/api/admin/settings/intake/regenerate", null))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString()!;
        await ExpectExpired($"/api/public/intake/{fresh}", token);   // a token of the old link
    }

    [Fact]
    public async Task Requests_per_ip_are_rate_limited()   // AC-INT6
    {
        var slug = await EnableIntakeAsync();
        await using var host = Factory.WithWebHostBuilder(b => b.UseSetting("RateLimit:PublicIntakePerMinute", "2"));
        var anon = Anonymous(host);

        (await anon.GetAsync($"/api/public/intake/{slug}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anon.GetAsync($"/api/public/intake/{slug}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anon.GetAsync($"/api/public/intake/{slug}")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_form_token_works_once()   // review P13-BE #2: no replay of one GET
    {
        var slug = await EnableIntakeAsync();
        var anon = Anonymous();
        var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;
        var before = await SubmissionCountAsync();

        (await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "lần 1"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var replay = await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "lần 2"));

        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("FORM_EXPIRED");
        (await SubmissionCountAsync()).Should().Be(before + 1);
    }

    [Fact]
    public async Task A_refused_form_keeps_its_token_for_the_corrected_resend()
    {
        var slug = await EnableIntakeAsync();
        var anon = Anonymous();
        var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;

        (await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "x", contactPhone: "12")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "x")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Posts_per_ip_are_rate_limited_per_hour()   // review P13-BE #2
    {
        var slug = await EnableIntakeAsync();
        await using var host = Factory.WithWebHostBuilder(b => b.UseSetting("RateLimit:PublicIntakeSubmitPerHour", "2"));
        var anon = Anonymous(host);

        async Task<HttpStatusCode> PostFreshAsync()
        {
            var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;
            return (await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "theo giờ"))).StatusCode;
        }

        (await PostFreshAsync()).Should().Be(HttpStatusCode.NoContent);
        (await PostFreshAsync()).Should().Be(HttpStatusCode.NoContent);
        (await PostFreshAsync()).Should().Be(HttpStatusCode.TooManyRequests);
        (await anon.GetAsync($"/api/public/intake/{slug}")).StatusCode.Should().Be(HttpStatusCode.OK, "reading the form is limited separately");
    }

    [Fact]
    public async Task One_ip_cannot_use_up_the_studio_day()   // review P13-BE #2: per-IP daily cap below the global 30
    {
        var slug = await EnableIntakeAsync();
        await using var host = Factory.WithWebHostBuilder(b => b.UseSetting("PublicIntake:MaxSubmissionsPerIpPerDay", "2"));
        var anon = Anonymous(host);

        async Task<HttpResponseMessage> PostFreshAsync()
        {
            var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;
            return await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "theo ngày"));
        }

        (await PostFreshAsync()).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostFreshAsync()).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var third = await PostFreshAsync();

        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await third.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("RATE_LIMITED");
    }

    [Fact]
    public async Task Full_storage_refuses_photos_but_still_takes_text()   // AC-INT6, decision 2b
    {
        var slug = await EnableIntakeAsync();
        await using var host = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.PostConfigure<StorageOptions>(o => o.QuotaBytes = 10)));
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var filler = FreelanceFlow.Api.Domain.Intake.IntakeSubmission.Create("Filler", "0909000000", null, null, null, [], "filler");
            db.IntakeSubmissions.Add(filler);
            db.Files.Add(ProjectFile.CreateImage(null, ImageOwnerTypes.IntakeSubmission, filler.Id, null, "CLIENT", false, "f.webp",
                1, 1, 3, 3, 3, [1], DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        var anon = Anonymous(host);
        var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;

        var withPhoto = IntakeForm(token, note: "có ảnh", images: 1);
        withPhoto.Headers.ContentLength = 1_000_000;   // declared: more than a text-only body
        var blocked = await anon.PostAsync($"/api/public/intake/{slug}", withPhoto);
        blocked.StatusCode.Should().Be(HttpStatusCode.InsufficientStorage);
        (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("STORAGE_FULL");

        (await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "chỉ chữ"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task From_80_percent_storage_the_form_takes_text_only()   // review P13-BE #5: anonymous photos stop before the designer's 90 %
    {
        var slug = await EnableIntakeAsync();
        await using var host = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.PostConfigure<StorageOptions>(o => o.QuotaBytes = 1000)));
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var used = await scope.ServiceProvider.GetRequiredService<FreelanceFlow.Api.Features.Storage.StorageAlertService>().UsedBytesAsync();
            var filler = FreelanceFlow.Api.Domain.Intake.IntakeSubmission.Create("Filler", "0909000000", null, null, null, [], "filler");
            db.IntakeSubmissions.Add(filler);
            var third = (850 - used) / 3;   // → about 85 %: above the intake limit, below the designer's 90 %
            db.Files.Add(ProjectFile.CreateImage(null, ImageOwnerTypes.IntakeSubmission, filler.Id, null, "CLIENT", false, "f.webp",
                1, 1, third, third, third, [1], DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        var anon = Anonymous(host);
        var form = await GetFormAsync(anon, slug);
        form.GetProperty("maxImages").GetInt32().Should().Be(0, "the form hides the photo picker");
        var token = form.GetProperty("formToken").GetString()!;

        foreach (var declareLarge in new[] { true, false })
        {
            var withPhoto = IntakeForm(token, note: "có ảnh", images: 1);
            if (declareLarge) withPhoto.Headers.ContentLength = 1_000_000;
            var refused = await anon.PostAsync($"/api/public/intake/{slug}", withPhoto);
            refused.StatusCode.Should().Be(HttpStatusCode.InsufficientStorage);
            (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("STORAGE_FULL");
        }

        (await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "chỉ chữ"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Oversized_body_is_413()
    {
        var slug = await EnableIntakeAsync();
        var anon = Anonymous();
        var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;
        var form = IntakeForm(token, note: "lớn", images: 1);
        form.Headers.ContentLength = FreelanceFlow.Api.Features.Images.ImageLimits.PublicIntakeBodyBytes(20, 3) + 1;   // declared, not allocated

        (await anon.PostAsync($"/api/public/intake/{slug}", form)).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Body_limit_follows_the_configured_photo_size()   // review P13-BE #6: 3 × App:MaxUploadMb + 1 MB of text
    {
        var slug = await EnableIntakeAsync();
        await using var host = Factory.WithWebHostBuilder(b => b.UseSetting("App:MaxUploadMb", "1"));
        var anon = Anonymous(host);
        var form = await GetFormAsync(anon, slug);
        form.GetProperty("maxImageMb").GetInt32().Should().Be(1);
        var body = IntakeForm(form.GetProperty("formToken").GetString()!, note: "lớn", images: 1);
        body.Headers.ContentLength = 4L * 1024 * 1024 + 1;   // above 3 × 1 MB + 1 MB, far below the old fixed 61 MB

        (await anon.PostAsync($"/api/public/intake/{slug}", body)).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }
}
