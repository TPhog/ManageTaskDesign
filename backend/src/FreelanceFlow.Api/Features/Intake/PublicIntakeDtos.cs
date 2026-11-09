using FreelanceFlow.Api.Features.RequestTemplates;

namespace FreelanceFlow.Api.Features.Intake;

/// <summary>Only what is already printed on every quotation (07-security §4.5).</summary>
public sealed record PublicStudioDto(string DisplayName, string? ContactPhone, string? ContactEmail);

public sealed record PublicIntakeFormDto(
    PublicStudioDto Studio, IReadOnlyList<PublicRequestTemplateDto> Templates, int MaxImages, int MaxImageMb, string FormToken);
