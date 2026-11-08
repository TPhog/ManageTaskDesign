using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Portal;

/// <summary>
/// 07-security §4.4: every portal query starts from the signed-in client's own, non-archived projects
/// and joins back to them — a clientId never comes from the request.
/// </summary>
internal static class PortalScope
{
    public static IQueryable<Project> OwnProjects(AppDbContext db, PortalPrincipal principal) =>
        db.Projects.AsNoTracking().Where(p => p.ClientId == principal.ClientId && p.ArchivedAt == null);
}
