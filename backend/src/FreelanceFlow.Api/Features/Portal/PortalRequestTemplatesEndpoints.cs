using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Features.RequestTemplates;
using FreelanceFlow.Api.Infrastructure.Security;

namespace FreelanceFlow.Api.Features.Portal;

/// <summary>Plan 13: templates offered in "Gửi yêu cầu" — studio-wide data, identical for every signed-in client.</summary>
public static class PortalRequestTemplatesEndpoints
{
    public static void MapPortalRequestTemplatesEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/portal/request-templates", async (AppDbContext db, CancellationToken ct) =>
                Results.Ok(await RequestTemplateDtos.ListPublicAsync(db, ct)))
           .RequireAuthorization(PortalPrincipal.Policy);
}
