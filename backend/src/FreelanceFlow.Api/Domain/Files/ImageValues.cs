namespace FreelanceFlow.Api.Domain.Files;

/// <summary>What an image belongs to (files.owner_type). DELIVERABLE is reserved for delivery (later plan).</summary>
public static class ImageOwnerTypes
{
    public const string Task = "TASK";
    public const string ProgressUpdate = "PROGRESS_UPDATE";
    public const string ClientRequest = "CLIENT_REQUEST";
    public const string Deliverable = "DELIVERABLE";
    /// <summary>Plan 13: reference photos of a public intake submission (no project until converted).</summary>
    public const string IntakeSubmission = "INTAKE_SUBMISSION";
    public static readonly string[] All = [Task, ProgressUpdate, ClientRequest, Deliverable, IntakeSubmission];
}

/// <summary>The three WebP objects stored per image (spec §3).</summary>
public static class ImageVariants
{
    public const string Original = "original";
    public const string Preview = "preview";
    public const string Thumb = "thumb";
    public static readonly string[] All = [Original, Preview, Thumb];

    /// <summary>The portal never receives the original (spec §4).</summary>
    public static readonly string[] ClientVisible = [Preview, Thumb];
}
