using System.Net.Http.Json;
using FreelanceFlow.Api.Domain.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FreelanceFlow.IntegrationTests.Dashboard;

/// <summary>Derived host whose IClock says a fixed Vietnam day (05:00Z = 12:00 VN), with a logged-in client (GET only).</summary>
internal static class DashboardTestHost
{
    public static readonly DateOnly Today = new(2030, 5, 15);

    public static DateOnly D(int month, int day, int year = 2030) => new(year, month, day);

    public static async Task<(HttpClient Client, IServiceProvider Services)> CreateAsync(
        AppFactory factory, Func<IServiceProvider, Task> seed, DateOnly? today = null)
    {
        var day = today ?? Today;
        var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<IClock>(new FixedDayClock(day))));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { username = AppFactory.TestUsername, password = AppFactory.TestPassword });
        resp.EnsureSuccessStatusCode();
        var cookies = resp.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToList();
        var admin = cookies.First(c => c.StartsWith("ff_admin=", StringComparison.Ordinal));
        var csrf = cookies.First(c => c.StartsWith("ff_csrf=", StringComparison.Ordinal));
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"{admin}; {csrf}");
        await seed(host.Services);
        return (client, host.Services);
    }

    internal sealed class FixedDayClock(DateOnly today) : IClock
    {
        public DateTimeOffset UtcNow => new(today.ToDateTime(new TimeOnly(5, 0)), TimeSpan.Zero);
        public DateOnly TodayVn => today;
    }
}
