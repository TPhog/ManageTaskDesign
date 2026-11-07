using System.Security.Cryptography;
using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Clients;

public class Client : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";
    public string? Company { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ContactChannel { get; set; }
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public bool PortalEnabled { get; set; }
    public byte[]? PortalTokenHash { get; set; }
    public int PortalTokenVersion { get; set; }
    public DateTimeOffset? PortalTokenCreatedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Returns the raw token (plain-text); only valid immediately after generation (never stored)
    public string GeneratePortalToken(IClock clock)
    {
        var raw = Base64Url(RandomNumberGenerator.GetBytes(32));
        PortalTokenHash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        PortalTokenVersion++;
        PortalTokenCreatedAt = clock.UtcNow;
        PortalEnabled = false; // Reset; caller must call EnablePortal if wanted
        return raw;
    }

    public void EnablePortal()
    {
        if (PortalTokenHash == null)
            throw new DomainException("BUSINESS_RULE", "Cần tạo link portal trước khi bật");
        if (Status == "INACTIVE")
            throw new DomainException("BUSINESS_RULE", "Không thể bật portal cho khách đã lưu trữ");
        PortalEnabled = true;
    }

    public void DisablePortal()
    {
        PortalEnabled = false;
    }

    // BR-10, BR-11
    public void Archive(IClock clock, bool hasActiveProjects)
    {
        if (hasActiveProjects)
            throw new DomainException("HAS_ACTIVE_PROJECTS",
                "Khách có dự án đang thực hiện hoặc tạm dừng (BR-10)");
        Status = "INACTIVE";
        ArchivedAt = clock.UtcNow;
        PortalEnabled = false; // BR-11
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
