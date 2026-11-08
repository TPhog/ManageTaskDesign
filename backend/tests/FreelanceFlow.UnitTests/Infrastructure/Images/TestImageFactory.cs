using SkiaSharp;

namespace FreelanceFlow.UnitTests.Infrastructure.Images;

/// <summary>Tiny in-memory images — tests never touch the disk or the network.</summary>
internal static class TestImageFactory
{
    public static SKBitmap Solid(int width, int height, SKColor color)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(color);
        return bitmap;
    }

    /// <summary>Left half <paramref name="left"/>, right half <paramref name="right"/>.</summary>
    public static SKBitmap Split(int width, int height, SKColor left, SKColor right)
    {
        var bitmap = Solid(width, height, right);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = left };
        canvas.DrawRect(0, 0, width / 2f, height, paint);
        canvas.Flush();
        return bitmap;
    }

    public static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 100)
    {
        using var data = bitmap.Encode(format, quality);
        return data.ToArray();
    }

    public static byte[] Png(int width, int height, SKColor color)
    {
        using var bitmap = Solid(width, height, color);
        return Encode(bitmap, SKEncodedImageFormat.Png);
    }

    /// <summary>Inserts a minimal big-endian EXIF APP1 segment (one IFD0 entry: Orientation) into a JPEG.</summary>
    public static byte[] WithExifOrientation(byte[] jpeg, byte orientation)
    {
        byte[] app1 =
        [
            0xFF, 0xE1, 0x00, 0x22,                         // APP1, length 34
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,             // "Exif\0\0"
            0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // TIFF "MM", 42, IFD0 at 8
            0x00, 0x01,                                     // 1 entry
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, orientation, 0x00, 0x00, // 0x0112 SHORT
            0x00, 0x00, 0x00, 0x00,                         // no next IFD
        ];
        var insertAt = 2; // after SOI
        if (jpeg[2] == 0xFF && jpeg[3] == 0xE0) insertAt = 4 + ((jpeg[4] << 8) | jpeg[5]); // after JFIF APP0
        return [.. jpeg[..insertAt], .. app1, .. jpeg[insertAt..]];
    }

    public static SKBitmap Decode(byte[] bytes) => SKBitmap.Decode(bytes) ?? throw new InvalidOperationException("decode");

    public static double DifferingPixelRatio(SKBitmap a, SKBitmap b, int tolerance = 12)
    {
        if (a.Width != b.Width || a.Height != b.Height) return 1;
        var differing = 0;
        for (var y = 0; y < a.Height; y++)
        for (var x = 0; x < a.Width; x++)
        {
            var p = a.GetPixel(x, y);
            var q = b.GetPixel(x, y);
            if (Math.Abs(p.Red - q.Red) > tolerance || Math.Abs(p.Green - q.Green) > tolerance || Math.Abs(p.Blue - q.Blue) > tolerance)
                differing++;
        }
        return (double)differing / (a.Width * a.Height);
    }

    public static double MeanLuminance(SKBitmap bitmap)
    {
        double sum = 0;
        for (var y = 0; y < bitmap.Height; y += 2)
        for (var x = 0; x < bitmap.Width; x += 2)
        {
            var c = bitmap.GetPixel(x, y);
            sum += 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
        }
        return sum / (((bitmap.Height + 1) / 2) * ((bitmap.Width + 1) / 2)) / 255.0;
    }

    public static bool ContainsAscii(byte[] haystack, string needle) =>
        haystack.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(needle)) >= 0;
}
