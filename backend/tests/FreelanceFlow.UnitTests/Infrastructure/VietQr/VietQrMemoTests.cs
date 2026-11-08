using FreelanceFlow.Api.Infrastructure.VietQr;

namespace FreelanceFlow.UnitTests.Infrastructure.VietQr;

public class VietQrMemoTests
{
    [Theory]
    [InlineData("deposit", "BG20260007 COC")]
    [InlineData("full", "BG20260007 TT")]
    [InlineData("remaining", "BG20260007 CONLAI")]
    public void Memo_is_number_without_dashes_plus_suffix(string purpose, string expected) =>
        Assert.Equal(expected, VietQrMemo.For("BG-2026-0007", purpose));

    [Fact]
    public void Unknown_purpose_throws() =>
        Assert.Throws<ArgumentException>(() => VietQrMemo.For("BG-2026-0007", "tip"));

    [Fact]
    public void Memo_reproduces_docs_vector_1()
    {
        var memo = VietQrMemo.For("BG-2026-0007", "deposit");
        Assert.Equal(
            "00020101021238540010A00000072701240006970436011001234567890208QRIBFTTA5303704540712000005802VN62180814BG20260007 COC6304FCA9",
            VietQrPayloadBuilder.Build(new VietQrRequest("970436", "0123456789", 1_200_000, memo)));
    }
}
