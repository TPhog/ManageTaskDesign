using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Features.Revisions;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Projects;

/// <summary>
/// "Cập nhật tiến độ": a designer note on the client's portal timeline. Same mechanism as BR-95 —
/// activity_logs.visible_to_client + client_message, read by GET /api/portal/projects/{id}/timeline.
/// </summary>
public static class ProgressUpdatesEndpoints
{
    public static void MapProgressUpdatesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/admin").RequireAuthorization()
            .MapPost("projects/{projectId:guid}/progress-updates", PostProgressUpdate);
    }

    private static async Task<IResult> PostProgressUpdate(
        Guid projectId, ProgressUpdateBody body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.NotFound();
        var errors = RevisionValidation.ProgressUpdate(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var message = body.Message!.Trim();
        activity.Write("project", projectId, "project.progress_update",
            clientId: project.ClientId, projectId: projectId,
            data: new { length = message.Length },
            visibleToClient: true,
            clientMessage: message);
        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/projects/{projectId}/activity", new ProgressUpdateDto(clock.UtcNow, message));
    }
}
