namespace FreelanceFlow.Api.Infrastructure.Images;

/// <summary>Watermark to draw on preview/thumb (spec §4); null = none (client uploads or watermark off).</summary>
public sealed record WatermarkSpec(string Text, int OpacityPercent);

/// <summary>Three WebP encodings of one upload; Width/Height are those of the original variant.</summary>
public sealed record ProcessedImage(int Width, int Height, byte[] Original, byte[] Preview, byte[] Thumb);
