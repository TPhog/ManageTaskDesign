namespace FreelanceFlow.Api.Domain.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly TodayVn { get; }
}
