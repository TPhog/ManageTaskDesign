namespace FreelanceFlow.Api.Infrastructure.Storage;

/// <summary>Section "Storage" (spec §2, §5). Production values come from appsettings.json / env.</summary>
public sealed class StorageOptions
{
    public const string Section = "Storage";

    public LocalStorageOptions Local { get; set; } = new();
    /// <summary>Storage:QuotaBytes — 10 GB by default.</summary>
    public long QuotaBytes { get; set; } = 10_737_418_240;
    /// <summary>0 disables the daily reconciliation (integration tests).</summary>
    public int ReconcileIntervalHours { get; set; } = 24;
}

public sealed class LocalStorageOptions
{
    /// <summary>Storage:Local:RootPath — absolute, or relative to the content root. Docker: /data/files (volume).</summary>
    public string RootPath { get; set; } = "./data/files";
}
