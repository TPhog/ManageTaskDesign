using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.UnitTests;

public class ClientTests
{
    private static IClock FakeClock() => new FakeClock(DateTimeOffset.UtcNow);

    [Fact]
    public void Archive_WithNoActiveProjects_SetsInactiveAndDisablesPortal()
    {
        var client = new Client { Status = "ACTIVE", PortalEnabled = true };
        client.Archive(FakeClock(), hasActiveProjects: false);

        Assert.Equal("INACTIVE", client.Status);
        Assert.False(client.PortalEnabled); // BR-11
        Assert.NotNull(client.ArchivedAt);
    }

    [Fact]
    public void Archive_WithActiveProjects_ThrowsHasActiveProjects()
    {
        var client = new Client { Status = "ACTIVE" };
        var ex = Assert.Throws<DomainException>(() =>
            client.Archive(FakeClock(), hasActiveProjects: true));
        Assert.Equal("HAS_ACTIVE_PROJECTS", ex.Code); // BR-10
    }

    [Fact]
    public void GeneratePortalToken_ReturnsRawToken_AndSetsHash()
    {
        var client = new Client { PortalEnabled = true, PortalTokenVersion = 2 };
        var token = client.GeneratePortalToken(FakeClock());

        Assert.NotEmpty(token);
        Assert.Equal(43, token.Length); // base64url(32 bytes) = 43 chars
        Assert.NotNull(client.PortalTokenHash);
        Assert.Equal(3, client.PortalTokenVersion); // BR-12: incremented
        Assert.False(client.PortalEnabled); // Reset to false
    }

    [Fact]
    public void GeneratePortalToken_CalledTwice_ProducesDifferentTokensAndIncreasesVersion()
    {
        var client = new Client { PortalTokenVersion = 0 };
        var t1 = client.GeneratePortalToken(FakeClock());
        var h1 = client.PortalTokenHash!.ToArray();

        var t2 = client.GeneratePortalToken(FakeClock());

        Assert.NotEqual(t1, t2);
        Assert.False(h1.SequenceEqual(client.PortalTokenHash!));
        Assert.Equal(2, client.PortalTokenVersion);
    }

    [Fact]
    public void EnablePortal_WithTokenHash_SetsEnabled()
    {
        var client = new Client { Status = "ACTIVE" };
        client.GeneratePortalToken(FakeClock());
        client.EnablePortal();
        Assert.True(client.PortalEnabled);
    }

    [Fact]
    public void EnablePortal_WithoutTokenHash_ThrowsBusinessRule()
    {
        var client = new Client { Status = "ACTIVE", PortalTokenHash = null };
        var ex = Assert.Throws<DomainException>(() => client.EnablePortal());
        Assert.Equal("BUSINESS_RULE", ex.Code);
    }

    [Fact]
    public void EnablePortal_OnArchivedClient_ThrowsBusinessRule()
    {
        var client = new Client { Status = "INACTIVE" };
        client.GeneratePortalToken(FakeClock());
        var ex = Assert.Throws<DomainException>(() => client.EnablePortal());
        Assert.Equal("BUSINESS_RULE", ex.Code);
    }

    [Fact]
    public void DisablePortal_SetsDisabled()
    {
        var client = new Client { PortalEnabled = true };
        client.DisablePortal();
        Assert.False(client.PortalEnabled);
    }
}

file class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;
    public DateOnly TodayVn => DateOnly.FromDateTime(now.Date);
}
