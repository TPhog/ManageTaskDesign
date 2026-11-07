namespace FreelanceFlow.Api.Domain.Settings;

public class Settings
{
    public short Id { get; set; } = 1;
    public string DisplayName { get; set; } = "";
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public Guid? LogoFileId { get; set; }
    public string? BankBin { get; set; }
    public string? BankAccountNo { get; set; }
    public string? BankAccountName { get; set; }
    public string QuotationPrefix { get; set; } = "BG";
    public int DefaultValidityDays { get; set; } = 7;
    public int DefaultDepositPercent { get; set; } = 50;
    public string DefaultTerms { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }

    public Bank? Bank { get; set; }
}
