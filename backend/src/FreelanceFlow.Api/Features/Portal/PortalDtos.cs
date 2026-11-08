namespace FreelanceFlow.Api.Features.Portal;

// Portal DTOs are separate from admin DTOs on purpose (07-security §4.4):
// never add internalNotes, notes, scopeOverrideReason or a private status reason here.

public sealed record PortalSessionRequest(string? Token);

public sealed record PortalDesignerDto(string DisplayName, string? LogoUrl, string? ContactPhone, string? ContactEmail);

public sealed record PortalMeDto(string ClientName, PortalDesignerDto Designer);
