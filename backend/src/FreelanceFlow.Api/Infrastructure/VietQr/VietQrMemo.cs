using FreelanceFlow.Api.Domain.Quotations;

namespace FreelanceFlow.Api.Infrastructure.VietQr;

/// <summary>Transfer content (docs/06 §4): "{NUMBER_NO_DASH} {COC|TT|CONLAI}", e.g. "BG20260007 COC". No version.</summary>
public static class VietQrMemo
{
    public static string For(string quotationNumber, string purpose)
    {
        var suffix = purpose switch
        {
            QrPurposes.Deposit => "COC",
            QrPurposes.Remaining => "CONLAI",
            QrPurposes.Full => "TT",
            _ => throw new ArgumentException($"Unknown QR purpose: {purpose}", nameof(purpose)),
        };
        var memo = $"{quotationNumber.Replace("-", "")} {suffix}";
        VietQrPayloadBuilder.ValidateMemo(memo);
        return memo;
    }
}
