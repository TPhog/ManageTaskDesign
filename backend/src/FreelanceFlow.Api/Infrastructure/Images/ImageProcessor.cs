using System.Text;
using FreelanceFlow.Api.Domain.Common;
using SkiaSharp;

namespace FreelanceFlow.Api.Infrastructure.Images;

/// <summary>
/// Spec §3–4: decode (sRGB) → apply EXIF orientation → fit → WebP original/preview/thumb, watermark on preview/thumb.
/// Re-encoding drops every metadata block (EXIF, GPS). Pure: no I/O, no DB.
/// </summary>
public static class ImageProcessor
{
    public const int OriginalMaxEdge = 3000;
    public const int ThumbMaxEdge = 480;
    public const int OriginalQuality = 85;
    public const int PreviewQuality = 82;
    public const int ThumbQuality = 75;
    public const int MinThumbOpacityPercent = 10;
    public const long MaxDecodedPixels = 50_000_000;
    public const float WatermarkAngleDegrees = -30f;
    public const float PreviewFontRatio = 0.04f;
    public const float ThumbFontRatio = 0.06f;
    private const float MinFontSize = 10f;
    private const double LightBackground = 0.6;

    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    public static ProcessedImage Process(byte[] input, WatermarkSpec? watermark)
    {
        if (ImageFormatSniffer.Detect(input) is null)
            throw new DomainException("UNSUPPORTED_FILE_TYPE", "Chỉ nhận ảnh JPEG, PNG hoặc WebP");

        using var data = SKData.CreateCopy(input);
        using var codec = SKCodec.Create(data)
            ?? throw new DomainException("UNSUPPORTED_FILE_TYPE", "Không đọc được ảnh — file có thể bị hỏng");
        GuardDimensions(codec.Info.Width, codec.Info.Height);

        using var decoded = Decode(codec);
        using var oriented = Orient(decoded, codec.EncodedOrigin);
        using var original = Fit(oriented, OriginalMaxEdge);
        using var preview = Copy(original);
        using var thumb = Fit(original, ThumbMaxEdge);

        if (watermark is not null)
        {
            var text = watermark.Text.Normalize(NormalizationForm.FormC);
            DrawWatermark(preview, text, watermark.OpacityPercent, PreviewFontRatio);
            DrawWatermark(thumb, text, ThumbOpacity(watermark.OpacityPercent), ThumbFontRatio);
        }

        return new ProcessedImage(original.Width, original.Height,
            EncodeWebp(original, OriginalQuality), EncodeWebp(preview, PreviewQuality), EncodeWebp(thumb, ThumbQuality));
    }

    public static int ThumbOpacity(int opacityPercent) =>
        Math.Max(MinThumbOpacityPercent, (int)Math.Round(opacityPercent * 2 / 3.0, MidpointRounding.AwayFromZero));

    internal static void GuardDimensions(int width, int height)
    {
        if ((long)width * height > MaxDecodedPixels)
            throw new DomainException("FILE_TOO_LARGE", "Ảnh có độ phân giải quá lớn (tối đa 50 megapixel)");
    }

    private static SKBitmap Decode(SKCodec codec)
    {
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul,
            SKColorSpace.CreateSrgb());
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput) return bitmap;
        bitmap.Dispose();
        throw new DomainException("UNSUPPORTED_FILE_TYPE", "Không đọc được ảnh — file có thể bị hỏng");
    }

    /// <summary>Always returns a new bitmap. x' = ScaleX·x + SkewX·y + TransX; y' = SkewY·x + ScaleY·y + TransY.</summary>
    private static SKBitmap Orient(SKBitmap src, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return Copy(src);
        float w = src.Width, h = src.Height;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),     // 2: mirror horizontal
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), // 3: rotate 180
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),   // 4: mirror vertical
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),       // 5: transpose
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),     // 6: rotate 90 CW
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), // 7: transverse
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),   // 8: rotate 90 CCW
            _ => SKMatrix.Identity,
        };
        var dst = new SKBitmap(src.Info.WithSize(swap ? src.Height : src.Width, swap ? src.Width : src.Height));
        using (var canvas = new SKCanvas(dst))
        {
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(src, 0, 0);
            canvas.Flush();
        }
        return dst;
    }

    /// <summary>Always returns a new bitmap; never upscales.</summary>
    private static SKBitmap Fit(SKBitmap src, int maxEdge)
    {
        var longEdge = Math.Max(src.Width, src.Height);
        if (longEdge <= maxEdge) return Copy(src);
        var scale = (double)maxEdge / longEdge;
        var width = Math.Max(1, (int)Math.Round(src.Width * scale, MidpointRounding.AwayFromZero));
        var height = Math.Max(1, (int)Math.Round(src.Height * scale, MidpointRounding.AwayFromZero));
        return src.Resize(src.Info.WithSize(width, height), Sampling)
            ?? throw new InvalidOperationException("Image resize failed");
    }

    private static SKBitmap Copy(SKBitmap src) => src.Copy() ?? throw new InvalidOperationException("Image copy failed");

    private static byte[] EncodeWebp(SKBitmap bitmap, int quality)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Webp, quality)
            ?? throw new InvalidOperationException("WebP encoding failed");
        return data.ToArray();
    }

    /// <summary>Text tiled diagonally (−30°) over the whole bitmap, staggered rows, contrast colour from mean luminance.</summary>
    private static void DrawWatermark(SKBitmap bitmap, string text, int opacityPercent, float fontRatio)
    {
        var fontSize = Math.Max(MinFontSize, Math.Min(bitmap.Width, bitmap.Height) * fontRatio);
        var alpha = (byte)Math.Round(255 * opacityPercent / 100.0, MidpointRounding.AwayFromZero);
        var colour = (MeanLuminance(bitmap) > LightBackground ? SKColors.Black : SKColors.White).WithAlpha(alpha);

        using var font = new SKFont(WatermarkFont.Typeface, fontSize);
        using var paint = new SKPaint { IsAntialias = true, Color = colour };
        using var canvas = new SKCanvas(bitmap);

        var stepX = font.MeasureText(text) + fontSize * 3;
        var stepY = fontSize * 4;
        var reach = (float)Math.Sqrt((double)bitmap.Width * bitmap.Width + (double)bitmap.Height * bitmap.Height) / 2 + stepX;
        canvas.Translate(bitmap.Width / 2f, bitmap.Height / 2f);
        canvas.RotateDegrees(WatermarkAngleDegrees);
        var row = 0;
        for (var y = -reach; y <= reach; y += stepY, row++)
        {
            var offset = row % 2 == 0 ? 0 : stepX / 2;
            for (var x = -reach - offset; x <= reach; x += stepX)
                canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
        }
        canvas.Flush();
    }

    private static double MeanLuminance(SKBitmap bitmap)
    {
        const int grid = 16;
        double sum = 0;
        for (var i = 0; i < grid; i++)
        for (var j = 0; j < grid; j++)
        {
            var c = bitmap.GetPixel((int)((i + 0.5) * bitmap.Width / grid), (int)((j + 0.5) * bitmap.Height / grid));
            sum += (0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue) / 255.0;
        }
        return sum / (grid * grid);
    }
}
