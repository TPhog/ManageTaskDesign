using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Projects;

public static class BriefsEndpoints
{
    public static void MapBriefsEndpoints(this IEndpointRouteBuilder app)
    {
        var adminGroup = app.MapGroup("/api/admin").RequireAuthorization();

        // List briefs for a project
        adminGroup.MapGet("projects/{projectId:guid}/briefs", ListBriefs);

        // Create new version
        adminGroup.MapPost("projects/{projectId:guid}/briefs", CreateNewVersion);

        // Brief detail + edit + confirm
        adminGroup.MapGet("briefs/{briefId:guid}", GetBrief);
        adminGroup.MapPut("briefs/{briefId:guid}", UpdateBrief);
        adminGroup.MapPost("briefs/{briefId:guid}/confirm", ConfirmBrief);
    }

    private static async Task<IResult> ListBriefs(Guid projectId, AppDbContext db)
    {
        var exists = await db.Projects.AnyAsync(p => p.Id == projectId);
        if (!exists) return Results.NotFound();

        var briefs = await db.ProjectBriefs
            .AsNoTracking()
            .Where(b => b.ProjectId == projectId)
            .OrderByDescending(b => b.Version)
            .Select(b => new
            {
                b.Id,
                b.ProjectId,
                b.Version,
                b.Status,
                b.ConfirmedAt,
                b.CreatedAt,
                b.UpdatedAt,
            })
            .ToListAsync();

        return Results.Ok(briefs);
    }

    private static async Task<IResult> GetBrief(Guid briefId, AppDbContext db)
    {
        var brief = await db.ProjectBriefs.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == briefId);
        if (brief == null) return Results.NotFound();

        return Results.Ok(new
        {
            brief.Id,
            brief.ProjectId,
            brief.Version,
            brief.Status,
            brief.Content,
            brief.ConfirmedAt,
            brief.CreatedAt,
            brief.UpdatedAt,
        });
    }

    private static async Task<IResult> UpdateBrief(
        Guid briefId,
        UpdateBriefRequest body,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var brief = await db.ProjectBriefs.FindAsync(briefId);
        if (brief == null) return Results.NotFound();

        if (brief.Status == "CONFIRMED")
            return Results.Conflict(new { code = "BRIEF_LOCKED", detail = "Brief đã được xác nhận, không thể sửa" });

        brief.Content = body.Content;

        activity.Write("brief", brief.Id, "brief.updated",
            projectId: brief.ProjectId,
            data: new { version = brief.Version });

        // DB trigger trg_brief_immutable also guards this
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            brief.Id,
            brief.ProjectId,
            brief.Version,
            brief.Status,
            brief.Content,
            brief.UpdatedAt,
        });
    }

    private static async Task<IResult> ConfirmBrief(
        Guid briefId,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var brief = await db.ProjectBriefs.FindAsync(briefId);
        if (brief == null) return Results.NotFound();

        try
        {
            brief.Confirm(clock);
        }
        catch (DomainException ex) when (ex.Code == "BUSINESS_RULE")
        {
            return Results.UnprocessableEntity(new { code = ex.Code, detail = ex.Detail });
        }
        catch (DomainException ex)
        {
            return Results.Conflict(new { code = ex.Code, detail = ex.Detail });
        }

        // Get project for clientId
        var projectClientId = await db.Projects
            .Where(p => p.Id == brief.ProjectId)
            .Select(p => p.ClientId)
            .FirstOrDefaultAsync();

        activity.Write("brief", brief.Id, "brief.confirmed",
            clientId: projectClientId == Guid.Empty ? null : projectClientId,
            projectId: brief.ProjectId,
            data: new { version = brief.Version },
            visibleToClient: true,
            clientMessage: $"Đã chốt yêu cầu thiết kế (lần {brief.Version})");

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            brief.Id,
            brief.ProjectId,
            brief.Version,
            brief.Status,
            brief.ConfirmedAt,
        });
    }

    private static async Task<IResult> CreateNewVersion(
        Guid projectId,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var exists = await db.Projects.AnyAsync(p => p.Id == projectId);
        if (!exists) return Results.NotFound();

        // BR-25: at most 1 DRAFT per project
        var hasDraft = await db.ProjectBriefs
            .AnyAsync(b => b.ProjectId == projectId && b.Status == "DRAFT");
        if (hasDraft)
            return Results.Conflict(new { code = "BUSINESS_RULE", detail = "Đã có brief DRAFT cho dự án này (BR-25)" });

        // Get latest version as source (BR-28)
        var latest = await db.ProjectBriefs
            .Where(b => b.ProjectId == projectId)
            .OrderByDescending(b => b.Version)
            .FirstOrDefaultAsync();

        if (latest == null)
            return Results.NotFound(new { detail = "Không có brief nào trong dự án" });

        // BR-26: version increments sequentially
        var maxVersion = await db.ProjectBriefs
            .Where(b => b.ProjectId == projectId)
            .MaxAsync(b => b.Version);

        var newBrief = latest.CreateNewVersion(maxVersion + 1);
        db.ProjectBriefs.Add(newBrief);

        var projectClientId = await db.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.ClientId)
            .FirstOrDefaultAsync();

        activity.Write("brief", newBrief.Id, "brief.version_created",
            clientId: projectClientId == Guid.Empty ? null : projectClientId,
            projectId: projectId,
            data: new { version = newBrief.Version, copiedFrom = latest.Version });

        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/briefs/{newBrief.Id}", new
        {
            newBrief.Id,
            newBrief.ProjectId,
            newBrief.Version,
            newBrief.Status,
            newBrief.Content,
            newBrief.CreatedAt,
        });
    }
}

record UpdateBriefRequest(string Content, uint? RowVersion);
