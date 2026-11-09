namespace FreelanceFlow.Api.Features.Intake;

/// <summary>Section "PublicIntake". Tests set MinFillSeconds = 0 and lift MaxSubmissionsPerIpPerDay.</summary>
public sealed class IntakeOptions
{
    /// <summary>BR-138: a form posted sooner than this after it was served is treated as a bot (silent 204).</summary>
    public int MinFillSeconds { get; set; } = 3;

    /// <summary>BR-137: stored submissions per client IP and Vietnamese day (below the studio-wide 30).</summary>
    public int MaxSubmissionsPerIpPerDay { get; set; } = 5;
}
