namespace FreelanceFlow.Api.Domain.Projects;

/// <summary>Values of projects.design_type (ck_projects_design_type) — also used by request templates (plan 13).</summary>
public static class ProjectDesignTypes
{
    public const string Logo = "LOGO";
    public const string Banner = "BANNER";
    public const string Poster = "POSTER";
    public const string Branding = "BRANDING";
    public const string UiUx = "UI_UX";
    public const string SocialMedia = "SOCIAL_MEDIA";
    public const string Packaging = "PACKAGING";
    public const string Print = "PRINT";
    public const string Other = "OTHER";
    public static readonly string[] All = [Logo, Banner, Poster, Branding, UiUx, SocialMedia, Packaging, Print, Other];
}
