using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Infrastructure.Images;
using SkiaSharp;

namespace FreelanceFlow.UnitTests.Infrastructure.Images;

public class ImageProcessorTests
{
    private static readonly WatermarkSpec Strong = new("Studio Test · Bản xem trước", 40);

    [Fact]
    public void Large_image_caps_original_at_3000_preview_at_2560_and_thumb_at_480_all_as_webp()
    {
        var png = TestImageFactory.Png(4000, 1000, new SKColor(40, 90, 160));

        var result = ImageProcessor.Process(png, Strong);

        (result.Width, result.Height).Should().Be((3000, 750));
        foreach (var bytes in new[] { result.Original, result.Preview!, result.Thumb })
            ImageFormatSniffer.Detect(bytes).Should().Be("image/webp");
        using var original = TestImageFactory.Decode(result.Original);
        using var preview = TestImageFactory.Decode(result.Preview!);
        using var thumb = TestImageFactory.Decode(result.Thumb);
        (original.Width, original.Height).Should().Be((3000, 750));
        (preview.Width, preview.Height).Should().Be((2560, 640));
        (thumb.Width, thumb.Height).Should().Be((480, 120));
    }

    [Fact]
    public void Small_image_is_never_upscaled()
    {
        var result = ImageProcessor.Process(TestImageFactory.Png(200, 100, SKColors.Orange), null);

        (result.Width, result.Height).Should().Be((200, 100));
        using var thumb = TestImageFactory.Decode(result.Thumb);
        (thumb.Width, thumb.Height).Should().Be((200, 100));
    }

    [Fact]
    public void Exif_orientation_is_applied_and_metadata_is_dropped()
    {
        using var source = TestImageFactory.Split(40, 20, SKColors.Red, SKColors.Blue);
        var jpeg = TestImageFactory.WithExifOrientation(TestImageFactory.Encode(source, SKEncodedImageFormat.Jpeg, 95), 6);
        TestImageFactory.ContainsAscii(jpeg, "Exif").Should().BeTrue("control: the input carries EXIF");

        var result = ImageProcessor.Process(jpeg, null);

        (result.Width, result.Height).Should().Be((20, 40), "orientation 6 = rotate 90° clockwise");
        using var original = TestImageFactory.Decode(result.Original);
        var top = original.GetPixel(10, 5);
        var bottom = original.GetPixel(10, 34);
        top.Red.Should().BeGreaterThan(top.Blue, "the left (red) half ends up on top");
        bottom.Blue.Should().BeGreaterThan(bottom.Red);
        foreach (var bytes in new[] { result.Original, result.Preview!, result.Thumb })
            TestImageFactory.ContainsAscii(bytes, "Exif").Should().BeFalse();
    }

    [Fact]
    public void Watermark_marks_preview_and_thumb_but_never_the_original()
    {
        var png = TestImageFactory.Png(600, 400, new SKColor(128, 128, 128));

        var marked = ImageProcessor.Process(png, Strong);
        var plain = ImageProcessor.Process(png, null);

        using var original = TestImageFactory.Decode(marked.Original);
        using var preview = TestImageFactory.Decode(marked.Preview!);
        using var thumb = TestImageFactory.Decode(marked.Thumb);
        using var plainOriginal = TestImageFactory.Decode(plain.Original);
        using var plainPreview = TestImageFactory.Decode(plain.Preview!);
        using var plainThumb = TestImageFactory.Decode(plain.Thumb);
        TestImageFactory.DifferingPixelRatio(original, preview).Should().BeGreaterThan(0.02);
        TestImageFactory.DifferingPixelRatio(plainThumb, thumb).Should().BeGreaterThan(0.01);
        TestImageFactory.DifferingPixelRatio(original, plainOriginal).Should().BeLessThan(0.01);
        TestImageFactory.DifferingPixelRatio(plainOriginal, plainPreview).Should().BeLessThan(0.01);
    }

    [Fact]
    public void Watermark_colour_contrasts_with_the_background()
    {
        var white = ImageProcessor.Process(TestImageFactory.Png(300, 300, SKColors.White), Strong);
        var black = ImageProcessor.Process(TestImageFactory.Png(300, 300, SKColors.Black), Strong);

        using var whitePreview = TestImageFactory.Decode(white.Preview!);
        using var blackPreview = TestImageFactory.Decode(black.Preview!);
        TestImageFactory.MeanLuminance(whitePreview).Should().BeLessThan(0.99, "dark text on a light image");
        TestImageFactory.MeanLuminance(blackPreview).Should().BeGreaterThan(0.01, "light text on a dark image");
    }

    [Theory]
    [InlineData(18, 12)]
    [InlineData(40, 27)]
    [InlineData(10, 10)]
    public void Thumb_watermark_is_lighter_but_at_least_10_percent(int opacity, int expected) =>
        ImageProcessor.ThumbOpacity(opacity).Should().Be(expected);

    [Fact]
    public void Non_images_and_corrupt_images_are_unsupported()
    {
        var pdf = () => ImageProcessor.Process("%PDF-1.7 fake"u8.ToArray(), null);
        pdf.Should().Throw<DomainException>().Which.Code.Should().Be("UNSUPPORTED_FILE_TYPE");
        var gif = () => ImageProcessor.Process(System.Text.Encoding.ASCII.GetBytes("GIF89a...."), null);
        gif.Should().Throw<DomainException>().Which.Code.Should().Be("UNSUPPORTED_FILE_TYPE");
        byte[] truncatedPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
        var corrupt = () => ImageProcessor.Process(truncatedPng, null);
        corrupt.Should().Throw<DomainException>().Which.Code.Should().Be("UNSUPPORTED_FILE_TYPE");
    }

    [Fact]
    public void Decompression_bombs_are_too_large()
    {
        var act = () => ImageProcessor.GuardDimensions(10_000, 5_001);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("FILE_TOO_LARGE");
        ImageProcessor.GuardDimensions(10_000, 5_000); // exactly 50 MP is accepted
    }

    [Fact]
    public void Transparent_image_is_flattened_onto_white_so_the_watermark_stays_visible()
    {
        var png = TestImageFactory.Png(300, 300, SKColors.Transparent);

        var result = ImageProcessor.Process(png, Strong);

        using var preview = TestImageFactory.Decode(result.Preview!);
        using var thumb = TestImageFactory.Decode(result.Thumb);
        foreach (var bitmap in new[] { preview, thumb })
        {
            bitmap.GetPixel(0, 0).Alpha.Should().Be(255, "transparency must not let the watermark be removed");
            TestImageFactory.MeanLuminance(bitmap).Should().BeLessThan(0.99, "dark text on the white backdrop");
            TestImageFactory.MeanLuminance(bitmap).Should().BeGreaterThan(0.8, "backdrop is white, not black");
        }
    }

    [Fact]
    public void Decided_edges_and_qualities_are_in_force()
    {
        (ImageProcessor.OriginalMaxEdge, ImageProcessor.PreviewMaxEdge, ImageProcessor.ThumbMaxEdge).Should().Be((3000, 2560, 480));
        (ImageProcessor.OriginalQuality, ImageProcessor.PreviewQuality, ImageProcessor.ThumbQuality).Should().Be((80, 78, 75));
    }

    [Fact]
    public void Client_upload_has_no_separate_preview_and_cannot_carry_a_watermark()
    {
        var png = TestImageFactory.Png(300, 200, SKColors.Orange);

        var result = ImageProcessor.Process(png, null, separatePreview: false);

        result.Preview.Should().BeNull();
        using var thumb = TestImageFactory.Decode(result.Thumb);
        (thumb.Width, thumb.Height).Should().Be((300, 200));
        var marked = () => ImageProcessor.Process(png, Strong, separatePreview: false);
        marked.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Flat_graphics_keep_a_lossless_original_when_it_is_smaller()
    {
        using var stripes = TestImageFactory.Stripes(600, 400, SKColors.Black, SKColors.White);
        var lossy = TestImageFactory.Encode(stripes, SKEncodedImageFormat.Webp, ImageProcessor.OriginalQuality);

        var result = ImageProcessor.Process(TestImageFactory.Encode(stripes, SKEncodedImageFormat.Png), null);

        TestImageFactory.ContainsAscii(result.Original, "VP8L").Should().BeTrue("lossless WebP chunk");
        result.Original.Length.Should().BeLessThan(lossy.Length);
        using var decoded = TestImageFactory.Decode(result.Original);
        TestImageFactory.DifferingPixelRatio(decoded, stripes, tolerance: 0).Should().Be(0);
    }

    [Fact]
    public void Photos_keep_the_lossy_original_and_lossless_is_never_tried()
    {
        using var noise = TestImageFactory.Noise(400, 300, seed: 7);

        ImageProcessor.LooksFlat(noise).Should().BeFalse();
        var result = ImageProcessor.Process(TestImageFactory.Encode(noise, SKEncodedImageFormat.Png), null);

        TestImageFactory.ContainsAscii(result.Original, "VP8L").Should().BeFalse();
    }

    [Fact]
    public void Looks_flat_counts_distinct_colours_on_a_sample_grid()
    {
        using var solid = TestImageFactory.Solid(100, 100, SKColors.Teal);
        using var stripes = TestImageFactory.Stripes(300, 200, SKColors.Red, SKColors.White);
        using var noise = TestImageFactory.Noise(200, 200, seed: 3);

        ImageProcessor.LooksFlat(solid).Should().BeTrue();
        ImageProcessor.LooksFlat(stripes).Should().BeTrue();
        ImageProcessor.LooksFlat(noise).Should().BeFalse();
    }
}
