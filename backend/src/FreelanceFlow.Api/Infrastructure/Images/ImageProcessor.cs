using System.Text;
using FreelanceFlow.Api.Domain.Common;
using SkiaSharp;

namespace FreelanceFlow.Api.Infrastructure.Images;

/// <summary>
/// Spec §3–4 + P10: decode (sRGB) → apply EXIF orientation → fit → WebP original (≤ 3000 px, q80; lossless instead when
/// smaller for flat graphics), preview (≤ 2560 px, q78, watermark), thumb (≤ 480 px, q75, lighter watermark).
/// Client uploads get no preview: the original is served in its place. Each step takes over its input bitmap and the
/// previous one is disposed at once, so at most two full-size bitmaps are alive. Re-encoding drops EXIF/GPS. Pure: no I/O, no DB.
/// </summary>
public static class ImageProcessor
{
    public const int OriginalMaxEdge = 3000;
    public const int PreviewMaxEdge = 2560;
    public const int ThumbMaxEdge = 480;
    public const int OriginalQuality = 80;
    public const int PreviewQuality = 78;
    public const int ThumbQuality = 75;
    /// <summary>
    /// Lossless is tried only when a 64 × 64 sample shows at most this many colours (UI, screenshots, flat art):
    /// on photos it is many times larger and costs seconds of CPU (P10 measurement), so it is never attempted there.
    /// </summary>
    public const int FlatMaxDistinctColours = 256;
    private const int FlatSampleGrid = 64;
    public const int MinThumbOpacityPercent = 10;
    public const long MaxDecodedPixels = 50_000_000;
    public const float WatermarkAngleDegrees = -30f;
    public const float PreviewFontRatio = 0.04f;
    public const float ThumbFontRatio = 0.06f;
    private const float MinFontSize = 10f;
    private const double LightBackground = 0.6;

    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    /// <param name="separatePreview">false for client uploads (P10): no preview is encoded, so none can be watermarked.</param>
    public static ProcessedImage Process(byte[] input, WatermarkSpec? watermark, bool separatePreview = true)
    {
        if (watermark is not null && !separatePreview)
            throw new ArgumentException("A watermark is drawn on a separate preview only", nameof(watermark));
        if (ImageFormatSniffer.Detect(input) is null)
            throw new DomainException("UNSUPPORTED_FILE_TYPE", "Chỉ nhận ảnh JPEG, PNG hoặc WebP");

        using var original = DecodeOriginal(input);
        var text = watermark?.Text.Normalize(NormalizationForm.FormC);
        var originalBytes = EncodeOriginal(original);
        var previewBytes = separatePreview
            ? EncodeVariant(original, PreviewMaxEdge, PreviewQuality, text, watermark?.OpacityPercent ?? 0, PreviewFontRatio)
            : null;
        var thumbBytes = EncodeVariant(original, ThumbMaxEdge, ThumbQuality, text,
            watermark is null ? 0 : ThumbOpacity(watermark.OpacityPercent), ThumbFontRatio);
        return new ProcessedImage(original.Width, original.Height, originalBytes, previewBytes, thumbBytes);
    }

    public static int ThumbOpacity(int opacityPercent) =>
        Math.Max(MinThumbOpacityPercent, (int)Math.Round(opacityPercent * 2 / 3.0, MidpointRounding.AwayFromZero));

    internal static void GuardDimensions(int width, int height)
    {
        if ((long)width * height > MaxDecodedPixels)
            throw new DomainException("FILE_TOO_LARGE", "Ảnh có độ phân giải quá lớn (tối đa 50 megapixel)");
    }

    /// <summary>Lossy q80 always; flat-looking bitmaps are also encoded lossless and the smaller of the two is kept.</summary>
    internal static byte[] EncodeOriginal(SKBitmap bitmap)
    {
        var lossy = EncodeWebp(bitmap, OriginalQuality);
        if (!LooksFlat(bitmap)) return lossy;
        var lossless = EncodeLossless(bitmap);
        return lossless.Length < lossy.Length ? lossless : lossy;
    }

    /// <summary>At most <see cref="FlatMaxDistinctColours"/> colours on a 64 × 64 sample grid (stops counting as soon as it exceeds).</summary>
    internal static bool LooksFlat(SKBitmap bitmap)
    {
        var colours = new HashSet<uint>();
        for (var i = 0; i < FlatSampleGrid; i++)
        for (var j = 0; j < FlatSampleGrid; j++)
        {
            var c = bitmap.GetPixel((int)((i + 0.5) * bitmap.Width / FlatSampleGrid), (int)((j + 0.5) * bitmap.Height / FlatSampleGrid));
            if (colours.Add((uint)c) && colours.Count > FlatMaxDistinctColours) return false;
        }
        return true;
    }

    /// <summary>The codec reads the caller's buffer through a stream (no copy) and is released before encoding starts.</summary>
    private static SKBitmap DecodeOriginal(byte[] input)
    {
        using var codec = SKCodec.Create(new MemoryStream(input, writable: false))
            ?? throw new DomainException("UNSUPPORTED_FILE_TYPE", "Không đọc được ảnh — file có thể bị hỏng");
        GuardDimensions(codec.Info.Width, codec.Info.Height);
        return Fit(Orient(Decode(codec), codec.EncodedOrigin), OriginalMaxEdge);
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

    /// <summary>
    /// Takes ownership of <paramref name="src"/>: returned as is for TopLeft, otherwise disposed once the oriented copy exists.
    /// x' = ScaleX·x + SkewX·y + TransX; y' = SkewY·x + ScaleY·y + TransY.
    /// </summary>
    private static SKBitmap Orient(SKBitmap src, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return src;
        using (src)
        {
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
    }

    /// <summary>Takes ownership of <paramref name="src"/>; never upscales.</summary>
    private static SKBitmap Fit(SKBitmap src, int maxEdge)
    {
        var resized = Downscaled(src, maxEdge);
        if (resized is null) return src;
        src.Dispose();
        return resized;
    }

    /// <summary>Borrows <paramref name="src"/>: a downscaled copy, or null when it already fits (never upscales).</summary>
    private static SKBitmap? Downscaled(SKBitmap src, int maxEdge)
    {
        var longEdge = Math.Max(src.Width, src.Height);
        if (longEdge <= maxEdge) return null;
        var scale = (double)maxEdge / longEdge;
        var width = Math.Max(1, (int)Math.Round(src.Width * scale, MidpointRounding.AwayFromZero));
        var height = Math.Max(1, (int)Math.Round(src.Height * scale, MidpointRounding.AwayFromZero));
        return src.Resize(src.Info.WithSize(width, height), Sampling)
            ?? throw new InvalidOperationException("Image resize failed");
    }

    /// <summary>
    /// Preview/thumb from the original: a downscaled copy (or the original itself when it already fits — no copy),
    /// flattened onto white and watermarked when <paramref name="text"/> is set. Every intermediate is disposed here.
    /// </summary>
    private static byte[] EncodeVariant(SKBitmap original, int maxEdge, int quality, string? text, int opacityPercent, float fontRatio)
    {
        using var resized = Downscaled(original, maxEdge);
        var source = resized ?? original;
        if (text is null) return EncodeWebp(source, quality);
        using var marked = FlattenOnWhite(source);
        DrawWatermark(marked, text, opacityPercent, fontRatio);
        return EncodeWebp(marked, quality);
    }

    /// <summary>Opaque copy over a white backdrop so transparency can neither hide nor strip the watermark.</summary>
    private static SKBitmap FlattenOnWhite(SKBitmap src)
    {
        var dst = new SKBitmap(src.Info.WithAlphaType(SKAlphaType.Premul));
        using (var canvas = new SKCanvas(dst))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(src, 0, 0);
            canvas.Flush();
        }
        return dst;
    }

    private static byte[] EncodeWebp(SKBitmap bitmap, int quality)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Webp, quality)
            ?? throw new InvalidOperationException("WebP encoding failed");
        return data.ToArray();
    }

    private static byte[] EncodeLossless(SKBitmap bitmap)
    {
        using var pixmap = bitmap.PeekPixels();
        using var data = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100))
            ?? throw new InvalidOperationException("WebP lossless encoding failed");
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
