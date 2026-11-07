using FluentAssertions;
using FreelanceFlow.Api.Domain.Auth;
using Microsoft.AspNetCore.Identity;

namespace FreelanceFlow.UnitTests.Domain;

public class DesignerAccountTests
{
    private static DesignerAccount CreateAccount(string password = "password123456")
    {
        var hasher = new PasswordHasher<DesignerAccount>();
        var account = new DesignerAccount { Username = "testuser" };
        account.PasswordHash = hasher.HashPassword(account, password);
        return account;
    }

    [Fact]
    public void VerifyPassword_correct_returns_true()
    {
        var account = CreateAccount("password123456");
        var hasher = new PasswordHasher<DesignerAccount>();
        account.VerifyPassword("password123456", hasher).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_wrong_returns_false()
    {
        var account = CreateAccount("password123456");
        var hasher = new PasswordHasher<DesignerAccount>();
        account.VerifyPassword("wrongpassword", hasher).Should().BeFalse();
    }

    [Fact]
    public void RecordFailedLogin_four_times_does_not_lock()
    {
        var account = new DesignerAccount();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 4; i++)
            account.RecordFailedLogin(now);
        account.IsLockedOut(now).Should().BeFalse();
        account.FailedLogins.Should().Be(4);
    }

    [Fact]
    public void RecordFailedLogin_fifth_time_returns_true_and_sets_lockout()
    {
        var account = new DesignerAccount();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 4; i++)
            account.RecordFailedLogin(now);
        var locked = account.RecordFailedLogin(now);
        locked.Should().BeTrue();
        account.LockoutUntil.Should().BeCloseTo(now.AddMinutes(15), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void IsLockedOut_during_lockout_returns_true()
    {
        var account = new DesignerAccount { LockoutUntil = DateTimeOffset.UtcNow.AddMinutes(10) };
        account.IsLockedOut(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsLockedOut_after_lockout_expires_returns_false()
    {
        var account = new DesignerAccount { LockoutUntil = DateTimeOffset.UtcNow.AddMinutes(-1) };
        account.IsLockedOut(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void ChangePassword_updates_hash_and_rotates_security_stamp()
    {
        var account = CreateAccount("oldpassword123");
        var originalStamp = account.SecurityStamp;
        var hasher = new PasswordHasher<DesignerAccount>();

        account.ChangePassword(hasher.HashPassword(account, "newpassword456"));

        account.VerifyPassword("newpassword456", hasher).Should().BeTrue();
        account.SecurityStamp.Should().NotBe(originalStamp);
        account.FailedLogins.Should().Be(0);
        account.LockoutUntil.Should().BeNull();
    }

    [Fact]
    public void ClearFailedLogins_resets_counter_and_lockout()
    {
        var account = new DesignerAccount { FailedLogins = 3, LockoutUntil = DateTimeOffset.UtcNow.AddMinutes(10) };
        account.ClearFailedLogins();
        account.FailedLogins.Should().Be(0);
        account.LockoutUntil.Should().BeNull();
    }
}
