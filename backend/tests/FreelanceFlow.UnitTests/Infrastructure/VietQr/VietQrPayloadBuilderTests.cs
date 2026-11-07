using FreelanceFlow.Api.Infrastructure.VietQr;

namespace FreelanceFlow.UnitTests.Infrastructure.VietQr;

public class VietQrPayloadBuilderTests
{
    [Fact]
    public void Vector1_matches_exactly()
    {
        var r = new VietQrRequest("970436", "0123456789", 1_200_000, "BG20260007 COC");
        Assert.Equal(
            "00020101021238540010A00000072701240006970436011001234567890208QRIBFTTA5303704540712000005802VN62180814BG20260007 COC6304FCA9",
            VietQrPayloadBuilder.Build(r));
    }

    [Fact]
    public void Vector2_matches_exactly()
    {
        var r = new VietQrRequest("970436", "0123456789", 2_400_000, "BG20260007 TT");
        Assert.Equal(
            "00020101021238540010A00000072701240006970436011001234567890208QRIBFTTA5303704540724000005802VN62170813BG20260007 TT63041421",
            VietQrPayloadBuilder.Build(r));
    }

    [Fact]
    public void Vector3_static_qr()
    {
        var r = new VietQrRequest("970436", "0123456789", null, null);
        Assert.Equal(
            "00020101021138540010A00000072701240006970436011001234567890208QRIBFTTA53037045802VN63046A15",
            VietQrPayloadBuilder.Build(r));
    }

    [Fact]
    public void Memo_too_long_throws() =>
        Assert.Throws<ArgumentException>(() =>
            VietQrPayloadBuilder.ValidateMemo("ABCDEFGHIJKLMNOPQRSTUVWXYZ")); // 26 chars

    [Fact]
    public void Memo_with_vietnamese_diacritics_throws() =>
        Assert.Throws<ArgumentException>(() =>
            VietQrPayloadBuilder.ValidateMemo("BG2026 CÓC")); // CÓC with diacritic

    [Fact]
    public void Memo_with_special_chars_throws() =>
        Assert.Throws<ArgumentException>(() =>
            VietQrPayloadBuilder.ValidateMemo("BG2026-007")); // has dash

    [Fact]
    public void Amount_zero_produces_static_qr()
    {
        var r = new VietQrRequest("970436", "0123456789", 0, null);
        var payload = VietQrPayloadBuilder.Build(r);
        Assert.Contains("010211", payload);      // PoI = "11" (static)
        Assert.DoesNotContain("5407", payload);  // no amount field (tag 54)
    }
}
