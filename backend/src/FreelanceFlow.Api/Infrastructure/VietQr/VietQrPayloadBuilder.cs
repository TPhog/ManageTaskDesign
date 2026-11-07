namespace FreelanceFlow.Api.Infrastructure.VietQr;

public sealed record VietQrRequest(string Bin, string AccountNo, long? Amount, string? Memo);

public static class VietQrPayloadBuilder
{
    static string Tlv(string id, string value)
    {
        if (value.Length > 99) throw new ArgumentException($"TLV {id} too long: {value.Length}");
        return id + value.Length.ToString("D2") + value;
    }

    public static string Build(VietQrRequest r)
    {
        ValidateMemo(r.Memo);

        var beneficiary = Tlv("00", r.Bin) + Tlv("01", r.AccountNo);
        var merchant    = Tlv("00", "A000000727") + Tlv("01", beneficiary) + Tlv("02", "QRIBFTTA");
        var sb = new System.Text.StringBuilder()
            .Append(Tlv("00", "01"))
            .Append(Tlv("01", r.Amount is > 0 ? "12" : "11"))
            .Append(Tlv("38", merchant))
            .Append(Tlv("53", "704"));
        if (r.Amount is > 0)
            sb.Append(Tlv("54", r.Amount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        sb.Append(Tlv("58", "VN"));
        if (!string.IsNullOrEmpty(r.Memo))
            sb.Append(Tlv("62", Tlv("08", r.Memo)));
        sb.Append("6304");
        var s = sb.ToString();
        return s + Crc16Ccitt.Compute(s);
    }

    public static void ValidateMemo(string? memo)
    {
        if (string.IsNullOrEmpty(memo)) return;
        if (memo.Length > 25)
            throw new ArgumentException("Memo must be ≤ 25 characters", nameof(memo));
        if (!System.Text.RegularExpressions.Regex.IsMatch(memo, @"^[A-Z0-9 ]+$"))
            throw new ArgumentException("Memo must contain only A-Z, 0-9, space", nameof(memo));
    }
}
