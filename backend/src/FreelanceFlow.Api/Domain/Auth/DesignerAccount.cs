using FreelanceFlow.Api.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace FreelanceFlow.Api.Domain.Auth;

public class DesignerAccount : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public int FailedLogins { get; set; }
    public DateTimeOffset? LockoutUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public bool VerifyPassword(string password, IPasswordHasher<DesignerAccount> hasher) =>
        hasher.VerifyHashedPassword(this, PasswordHash, password) != PasswordVerificationResult.Failed;

    public bool RecordFailedLogin(DateTimeOffset now)
    {
        FailedLogins++;
        if (FailedLogins >= 5)
        {
            LockoutUntil = now.AddMinutes(15);
            return true;
        }
        return false;
    }

    public void ClearFailedLogins()
    {
        FailedLogins = 0;
        LockoutUntil = null;
    }

    public bool IsLockedOut(DateTimeOffset now) =>
        LockoutUntil.HasValue && now < LockoutUntil.Value;

    public void ChangePassword(string newHash)
    {
        PasswordHash = newHash;
        SecurityStamp = Guid.NewGuid().ToString("N");
        ClearFailedLogins();
    }
}
