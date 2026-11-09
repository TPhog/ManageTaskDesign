using Microsoft.Extensions.Caching.Memory;

namespace FreelanceFlow.Api.Features.Intake;

/// <summary>
/// BR-137 (review P13-BE #2): stored public submissions per client IP and Vietnamese day, so one address cannot use up
/// the studio-wide daily cap. In memory (one API instance; a restart only resets the counts). The IP is never stored
/// in the database or logged.
/// </summary>
public sealed class IntakeIpDailyCounter(IMemoryCache cache)
{
    private readonly Lock _gate = new();

    public int Count(string ip, DateOnly todayVn) => cache.TryGetValue(Key(ip, todayVn), out int n) ? n : 0;

    public void Record(string ip, DateOnly todayVn)
    {
        lock (_gate)
            cache.Set(Key(ip, todayVn), Count(ip, todayVn) + 1, TimeSpan.FromHours(26));   // outlives the VN day it counts
    }

    private static string Key(string ip, DateOnly day) => $"intake-ip-day:{day:yyyyMMdd}:{ip}";
}
