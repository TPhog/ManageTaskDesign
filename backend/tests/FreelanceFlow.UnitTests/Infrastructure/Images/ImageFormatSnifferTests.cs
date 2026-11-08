using FluentAssertions;
using FreelanceFlow.Api.Infrastructure.Images;
using SkiaSharp;

namespace FreelanceFlow.UnitTests.Infrastructure.Images;

public class ImageFormatSnifferTests
{
    [Fact]
    public void Detects_jpeg_png_and_webp_by_magic_bytes()
    {
        using var bitmap = TestImageFactory.Solid(4, 4, SKColors.Teal);
        ImageFormatSniffer.Detect(TestImageFactory.Encode(bitmap, SKEncodedImageFormat.Jpeg)).Should().Be("image/jpeg");
        ImageFormatSniffer.Detect(TestImageFactory.Encode(bitmap, SKEncodedImageFormat.Png)).Should().Be("image/png");
        ImageFormatSniffer.Detect(TestImageFactory.Encode(bitmap, SKEncodedImageFormat.Webp, 80)).Should().Be("image/webp");
    }

    [Theory]
    [InlineData("GIF89a....")]
    [InlineData("%PDF-1.7 ...")]
    [InlineData("RIFF....WAVE")]
    [InlineData("")]
    public void Anything_else_is_not_an_image(string head) =>
        ImageFormatSniffer.Detect(System.Text.Encoding.ASCII.GetBytes(head)).Should().BeNull();
}
