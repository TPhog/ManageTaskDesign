namespace FreelanceFlow.Api.Features.Intake;

/// <summary>Section "PublicIntake". Tests set MinFillSeconds = 0.</summary>
public sealed class IntakeOptions
{
    /// <summary>BR-138: a form posted sooner than this after it was served is treated as a bot (silent 204).</summary>
    public int MinFillSeconds { get; set; } = 3;
}
