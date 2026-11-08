using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Features.Images;
using FreelanceFlow.Api.Features.Revisions;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Projects;

/// <summary>
/// "Cập nhật tiến độ": a designer note on the client's portal timeline. Same mechanism as BR-95 —
/// activity_logs.visible_to_client + client_message, read by GET /api/portal/projects/{id}/timeline.
/// Plan 08: JSON { message } or multipart message + files (≤ 10 images, always shown to the client); the activity
/// entity is the update itself so its images (owner_id) and the timeline row share one id.
/// </summary>
public static class ProgressUpdatesEndpoints
{
    public static void MapProgressUpdatesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/admin").RequireAuthorization()
            .MapPost("projects/{projectId:guid}/progress-updates", PostProgressUpdate)
            .WithMetadata(new RequestSizeLimitAttribute(ImageLimits.MaxAdminBodyBytes));
    }

    private static async Task<IResult> PostProgressUpdate(
        Guid projectId, HttpRequest http, AppDbContext db, ActivityWriter activity, ImageUploader uploader,
        StorageAlertService storageAlerts, IClock clock, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return Results.NotFound();

        string? message;
        IFormFileCollection? files = null;
        if (http.HasFormContentType)
        {
            var blocked = await ImageForms.EnsureUploadsAllowedAsync(storageAlerts, http, imagesOptional: true, ct);
            if (blocked is not null) return blocked;
            var (form, error) = await ImageForms.ReadAsync(http, ImageLimits.MaxAdminBodyBytes, ct);
            if (error is not null) return error;
            message = form!["message"].ToString();
            files = form.Files;
        }
        else if (http.HasJsonContentType())
        {
            try
            {
                message = (await http.ReadFromJsonAsync<ProgressUpdateBody>(ct))?.Message;
            }
            catch (JsonException)
            {
                message = null;
            }
        }
        else
        {
            return Results.Json(
                new { code = "UNSUPPORTED_MEDIA_TYPE", detail = "Gửi JSON hoặc multipart/form-data" },
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        var errors = RevisionValidation.ProgressUpdate(new ProgressUpdateBody(message));
        if (files is not null)
            foreach (var (field, messages) in ImageLimits.ValidateCount(files.Count, ImageLimits.MaxFilesPerAdminRequest, required: false))
                errors[field] = messages;
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var updateId = Guid.CreateVersion7();
        var text = message!.Trim();
        IReadOnlyList<ProjectFile> images = [];
        try
        {
            if (files is { Count: > 0 })
                images = await uploader.StageAsync(files,
                    new ImageTarget(projectId, ImageOwnerTypes.ProgressUpdate, updateId, null, "DESIGNER", VisibleToClient: true), ct);
            activity.Write("progress_update", updateId, "project.progress_update",
                clientId: project.ClientId, projectId: projectId,
                data: new { length = text.Length, images = images.Count, bytes = images.Sum(i => i.SizeBytes) },
                visibleToClient: true,
                clientMessage: text);
            await db.SaveChangesAsync(ct);
        }
        catch (DomainException ex)
        {
            await uploader.DiscardAsync();
            return ImageResults.FromDomain(ex);
        }
        catch
        {
            await uploader.DiscardAsync();
            throw;
        }

        if (images.Count > 0) await storageAlerts.EvaluateSafelyAsync(CancellationToken.None);
        return Results.Created($"/api/admin/projects/{projectId}/activity",
            new ProgressUpdateDto(updateId, clock.UtcNow, text, images.Select(i => ImageDtos.ToAdmin(i) with { DuplicateOf = uploader.DuplicateOf(i.Id) }).ToList()));
    }
}
