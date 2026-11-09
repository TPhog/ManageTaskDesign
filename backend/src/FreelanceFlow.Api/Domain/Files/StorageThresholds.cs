namespace FreelanceFlow.Api.Domain.Files;

public sealed record ThresholdChange(int Threshold, bool Raised);

/// <summary>
/// Spec §2: thresholds 50/70/85/90/95 % of Storage:QuotaBytes, each reported once when crossed upwards and
/// re-armed below (T − 2) %. Integer basis points (1/100 %) — no floating point in the decisions.
/// </summary>
public static class StorageThresholds
{
    public static readonly int[] All = [50, 70, 85, 90, 95];
    /// <summary>50 % is logged only; these also notify the designer.</summary>
    public static readonly int[] NotifyAt = [70, 85, 90, 95];
    public const int BlockUploadsAt = 90;
    public const int HysteresisPercent = 2;

    public static long BasisPoints(long usedBytes, long quotaBytes) =>
        quotaBytes <= 0 ? 10_000 : usedBytes * 10_000 / quotaBytes;

    public static double Percent(long usedBytes, long quotaBytes) =>
        Math.Round(BasisPoints(usedBytes, quotaBytes) / 100.0, 1, MidpointRounding.AwayFromZero);

    public static bool UploadsBlocked(long usedBytes, long quotaBytes) =>
        BasisPoints(usedBytes, quotaBytes) >= BlockUploadsAt * 100;

    /// <summary>Plan 13: photos sent by anonymous new clients stop earlier, so they can never eat the designer's room.</summary>
    public const int BlockIntakePhotosAt = 80;

    public static bool IntakePhotosBlocked(long usedBytes, long quotaBytes) =>
        BasisPoints(usedBytes, quotaBytes) >= BlockIntakePhotosAt * 100;

    public static string Level(long usedBytes, long quotaBytes) => BasisPoints(usedBytes, quotaBytes) switch
    {
        >= 9_500 => "CRITICAL",
        >= 9_000 => "BLOCKED",
        >= 8_500 => "WARNING",
        >= 7_000 => "NOTICE",
        _ => "OK",
    };

    public static IReadOnlyList<ThresholdChange> Evaluate(long usedBytes, long quotaBytes, IReadOnlySet<int> active)
    {
        var bp = BasisPoints(usedBytes, quotaBytes);
        var changes = new List<ThresholdChange>();
        foreach (var t in All)
        {
            if (!active.Contains(t) && bp >= t * 100) changes.Add(new ThresholdChange(t, true));
            else if (active.Contains(t) && bp < (t - HysteresisPercent) * 100) changes.Add(new ThresholdChange(t, false));
        }
        return changes;
    }
}
