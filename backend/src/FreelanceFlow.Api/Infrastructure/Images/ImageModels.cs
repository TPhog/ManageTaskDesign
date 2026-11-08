namespace FreelanceFlow.Api.Infrastructure.Images;

/// <summary>Watermark to draw on preview/thumb (spec §4); null = none (watermark off); client uploads never get one.</summary>
public sealed record WatermarkSpec(string Text, int OpacityPercent);

/// <summary>
/// WebP encodings of one upload; Width/Height are those of the original variant. Preview is null for client uploads
/// (P10): no preview object is stored and the original is served in its place.
/// </summary>
public sealed record ProcessedImage(int Width, int Height, byte[] Original, byte[]? Preview, byte[] Thumb);
