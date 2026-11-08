namespace FreelanceFlow.Api.Common;

public sealed class AppSettings
{
    public string PublicBaseUrl { get; init; } = "";
    public int MaxUploadMb { get; init; } = 20;
}

public sealed class SeedSettings
{
    public string? DesignerUsername { get; init; }
    public string? DesignerPassword { get; init; }
}
