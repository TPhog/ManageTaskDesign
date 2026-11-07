namespace FreelanceFlow.Api.Domain.Common;

public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo VnTz = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly TodayVn => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));
}
