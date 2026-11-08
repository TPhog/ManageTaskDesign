using System.Net.Http.Headers;
using SkiaSharp;

namespace FreelanceFlow.IntegrationTests.Images;

/// <summary>Tiny generated images — no fixture files, no network.</summary>
public static class TestImages
{
    public static byte[] Png(int width = 64, int height = 48, SKColor? color = null)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(color ?? new SKColor(70, 130, 180));
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static ByteArrayContent Part(byte[] bytes, string contentType = "image/png")
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return part;
    }

    /// <summary>multipart with one "files" part per image, named anh-1.png, anh-2.png…</summary>
    public static MultipartFormDataContent Form(params byte[][] images)
    {
        var form = new MultipartFormDataContent();
        for (var i = 0; i < images.Length; i++) form.Add(Part(images[i]), "files", $"anh-{i + 1}.png");
        return form;
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
}
