using System.Net;
using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Intake;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Intake;

public class PublicIntakeDailyCapTests(AppFactory factory) : IntakeTestBase(factory)
{
    [Fact]
    public async Task Thirty_submissions_a_day_close_the_form_until_tomorrow()   // AC-INT6, BR-137
    {
        var slug = await EnableIntakeAsync();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < IntakeSubmission.DailyLimit; i++)
                db.IntakeSubmissions.Add(IntakeSubmission.Create($"Khách {i}", "0909123456", null, null, null, [], "Cần thiết kế"));
            await db.SaveChangesAsync();
        }
        var anon = Anonymous();
        var token = (await GetFormAsync(anon, slug)).GetProperty("formToken").GetString()!;

        var resp = await anon.PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "thứ 31"));

        resp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("RATE_LIMITED");
    }
}
