namespace FreelanceFlow.Api.Common;

public sealed class AppSettings
{
    public string PublicBaseUrl { get; init; } = "";
    /// <summary>Extra browser origins allowed to make writes (e.g. a dev tunnel domain); PublicBaseUrl is always allowed.</summary>
    public string[] AdditionalOrigins { get; init; } = [];
    public int MaxUploadMb { get; init; } = 20;
}

public sealed class SeedSettings
{
    public string? DesignerUsername { get; init; }
    public string? DesignerPassword { get; init; }
}
