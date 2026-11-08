namespace FreelanceFlow.Api.Domain.Files;

/// <summary>storage_alerts: one row per threshold; active while ClearedAt is null (spec §6).</summary>
public class StorageAlert
{
    /// <summary>activity_logs.entity_id for storage-wide entries (entity_type "storage").</summary>
    public static readonly Guid ActivityEntityId = Guid.Empty;

    public int Threshold { get; set; }
    public DateTimeOffset NotifiedAt { get; set; }
    public DateTimeOffset? ClearedAt { get; set; }
}
