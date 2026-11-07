namespace FreelanceFlow.Api.Common;

public sealed class AppSettings
{
    public string PublicBaseUrl { get; init; } = "";
    public string FilesRoot { get; init; } = "/data/files";
    public int MaxUploadMb { get; init; } = 20;
}

public sealed class SeedSettings
{
    public string? DesignerEmail { get; init; }
    public string? DesignerPassword { get; init; }
}
