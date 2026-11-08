using SkiaSharp;

namespace FreelanceFlow.Api.Infrastructure.Images;

/// <summary>Noto Sans Bold embedded in the assembly: the Linux NoDependencies build has no system fonts.</summary>
internal static class WatermarkFont
{
    private const string ResourceName = "FreelanceFlow.WatermarkFont.ttf";

    private static readonly Lazy<SKTypeface> Font = new(() =>
    {
        using var stream = typeof(WatermarkFont).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        using var data = SKData.CreateCopy(buffer.ToArray());
        return SKTypeface.FromData(data) ?? throw new InvalidOperationException("Watermark font could not be loaded");
    });

    public static SKTypeface Typeface => Font.Value;
}
