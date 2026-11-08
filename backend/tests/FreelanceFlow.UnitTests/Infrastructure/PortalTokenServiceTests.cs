using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Infrastructure.Security;

namespace FreelanceFlow.UnitTests.Infrastructure;

public class PortalTokenServiceTests
{
    [Fact]
    public void Hash_of_issued_token_matches_hash_stored_on_client()
    {
        var client = new Client { Status = "ACTIVE" };
        var token = client.IssuePortalLink(new FixedClock());

        Assert.True(PortalTokenService.Matches(client.PortalTokenHash, PortalTokenService.Hash(token)));
    }

    [Fact]
    public void Matches_is_false_for_other_token_or_missing_hash()
    {
        var client = new Client { Status = "ACTIVE" };
        client.IssuePortalLink(new FixedClock());
        var other = PortalTokenService.Hash(new string('A', 43));

        Assert.False(PortalTokenService.Matches(client.PortalTokenHash, other));
        Assert.False(PortalTokenService.Matches(null, other));
    }

    [Fact]
    public void IsWellFormed_accepts_only_43_base64url_characters()
    {
        var client = new Client { Status = "ACTIVE" };
        Assert.True(PortalTokenService.IsWellFormed(client.IssuePortalLink(new FixedClock())));
        Assert.True(PortalTokenService.IsWellFormed(new string('a', 42) + "_"));
        Assert.False(PortalTokenService.IsWellFormed(null));
        Assert.False(PortalTokenService.IsWellFormed(""));
        Assert.False(PortalTokenService.IsWellFormed("not-a-token"));
        Assert.False(PortalTokenService.IsWellFormed(new string('a', 42) + "+"));
        Assert.False(PortalTokenService.IsWellFormed(new string('a', 44)));
    }
}

file sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow => new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero);
    public DateOnly TodayVn => new(2026, 11, 8);
}
