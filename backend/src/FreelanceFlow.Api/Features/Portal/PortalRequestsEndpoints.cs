using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Features.Images;
using FreelanceFlow.Api.Features.Revisions;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Notifications;
using FreelanceFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Features.Portal;

/// <summary>REQ-01 / REQ-06: the client sends change requests and follows their status.</summary>
public static class PortalRequestsEndpoints
{
    /// <summary>
    /// Text + up to 5 images (plan 08). An over-limit body is answered 413 (Payload Too Large),
    /// whether detected via Content-Length or while streaming the form.
    /// </summary>
    private const long MaxBodyBytes = ImageLimits.MaxPortalRequestBodyBytes;

    public static void MapPortalRequestsEndpoints(this IEndpointRouteBuilder app)
    {
        var portal = app.MapGroup("/api/portal").RequireAuthorization(PortalPrincipal.Policy);

        portal.MapGet("requests", ListRequests);
        portal.MapPost("projects/{id:guid}/requests", SendRequest)
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));
    }

    private static async Task<IResult> ListRequests(
        [FromServices] PortalPrincipal principal,
        AppDbContext db,
        Guid? projectId = null)
    {
        var own = PortalScope.OwnProjects(db, principal);
        if (projectId is { } pid && !await own.AnyAsync(p => p.Id == pid)) return Results.NotFound();

        var query = from r in db.ClientRequests.AsNoTracking()
                    join p in own on r.ProjectId equals p.Id
                    select new { Request = r, ProjectName = p.Name };
        if (projectId is { } id) query = query.Where(x => x.Request.ProjectId == id);

        var rows = await query
            .OrderByDescending(x => x.Request.CreatedAt).ThenByDescending(x => x.Request.Id)
            .ToListAsync();
        return Results.Ok(await ToDtosAsync(db, rows.Select(x => (x.Request, x.ProjectName)).ToList()));
    }

    /// <summary>
    /// REQ-01, BR-21/53/54. Multipart per docs/05 §4; up to 5 images, optimised like designer uploads but never watermarked (plan 08).
    /// The body is never logged (07-security).
    /// </summary>
    private static async Task<IResult> SendRequest(
        Guid id,
        HttpRequest http,
        [FromServices] PortalPrincipal principal,
        AppDbContext db,
        ActivityWriter activity,
        ImageUploader uploader,
        StorageAlertService storageAlerts,
        IClock clock,
        INotifier notifier,
        IOptions<AppSettings> appSettings,
        ILogger<PortalRequestsEndpointsLog> logger)
    {
        var project = await PortalScope.OwnProjects(db, principal)
            .Where(p => p.Id == id)
            .Select(p => new { p.Id, p.Name, p.Status })
            .FirstOrDefaultAsync();
        if (project is null) return Results.NotFound();

        if (!http.HasFormContentType)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["content"] = ["Gửi yêu cầu dạng multipart/form-data"],
            });
        if (http.ContentLength > MaxBodyBytes) return TooLarge();

        // cheap refusals before the (up to 101 MB) body is read
        // BR-54: portal requests of this client since 00:00 VN (designer-recorded requests do not count)
        var dayStart = ClientRequest.DayStartUtc(clock.TodayVn);
        var sentToday = await (from r in db.ClientRequests
                               join p in db.Projects on r.ProjectId equals p.Id
                               where p.ClientId == principal.ClientId && r.CreatedBy == "CLIENT" && r.CreatedAt >= dayStart
                               select r.Id).CountAsync();
        if (sentToday >= ClientRequest.DailyClientLimit)
            return Results.Json(
                new { code = "RATE_LIMITED", detail = $"Mỗi ngày bạn gửi được tối đa {ClientRequest.DailyClientLimit} yêu cầu" },
                statusCode: StatusCodes.Status429TooManyRequests);
        var blocked = await ImageForms.EnsureUploadsAllowedAsync(storageAlerts, http, imagesOptional: true, http.HttpContext.RequestAborted);
        if (blocked is not null) return blocked;

        IFormCollection form;
        try
        {
            form = await http.ReadFormAsync();
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge();
        }
        catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException or IOException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["content"] = ["Dữ liệu gửi lên không hợp lệ"],
            });
        }
        var content = form["content"].ToString();

        var errors = new Dictionary<string, string[]>();
        if (content.Trim().Length is < 1 or > ClientRequest.MaxContent)
            errors["content"] = [$"Nội dung dài 1–{ClientRequest.MaxContent} ký tự"];
        Guid? deliverableId = null;
        var rawDeliverable = form["deliverableId"].ToString();
        if (rawDeliverable.Length > 0)
        {
            if (Guid.TryParse(rawDeliverable, out var did) && await db.Deliverables.AnyAsync(d => d.Id == did && d.ProjectId == id && d.Status != "CANCELLED"))
                deliverableId = did;
            else
                errors["deliverableId"] = ["Hạng mục không thuộc dự án này"];
        }
        foreach (var (field, messages) in ImageLimits.ValidateCount(form.Files.Count, ImageLimits.MaxFilesPerPortalRequest, required: false))
            errors[field] = messages;
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        ClientRequest request;
        try
        {
            request = ClientRequest.CreateByClient(id, project.Status, deliverableId, content, sentToday);
        }
        catch (DomainException ex) when (ex.Code == "RATE_LIMITED")
        {
            return Results.Json(new { code = ex.Code, detail = ex.Detail }, statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (DomainException ex)
        {
            return RevisionResults.FromDomain(ex);
        }

        db.ClientRequests.Add(request);
        activity.Write("request", request.Id, "request.created",
            clientId: principal.ClientId, projectId: id, actorType: "CLIENT",
            data: new { source = request.Source, createdBy = request.CreatedBy, deliverableId = request.DeliverableId });
        IReadOnlyList<ProjectFile> images = [];
        try
        {
            if (form.Files.Count > 0)
            {
                // the request and its photos commit together; a refused photo creates no request (spec §7)
                images = await uploader.StageAsync(form.Files,
                    new ImageTarget(id, ImageOwnerTypes.ClientRequest, request.Id, request.Id, "CLIENT", VisibleToClient: false),
                    http.HttpContext.RequestAborted);
                activity.Write("request", request.Id, "image.uploaded",
                    clientId: principal.ClientId, projectId: id, actorType: "CLIENT",
                    data: new { count = images.Count, bytes = images.Sum(i => i.SizeBytes), ownerType = ImageOwnerTypes.ClientRequest });
            }
            await db.SaveChangesAsync();
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

        await NotifyDesignerAsync(db, notifier, appSettings.Value, logger, request, project.Name, principal.ClientId, images.Count);

        var dto = (await ToDtosAsync(db, [(request, project.Name)])).Single();
        return Results.Json(dto, statusCode: StatusCodes.Status201Created);
    }

    /// <summary>Discord alert for the designer; runs after commit and must never fail the request.</summary>
    private static async Task NotifyDesignerAsync(
        AppDbContext db, INotifier notifier, AppSettings app, ILogger logger,
        ClientRequest request, string projectName, Guid clientId, int imageCount = 0)
    {
        try
        {
            var clientName = await db.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.Name).FirstOrDefaultAsync();
            var fields = new List<NotificationField>
            {
                new("Khách", clientName ?? "-"),
                new("Dự án", projectName),
            };
            if (request.DeliverableId is { } did)
            {
                var deliverable = await db.Deliverables.AsNoTracking().Where(d => d.Id == did).Select(d => d.Name).FirstOrDefaultAsync();
                if (deliverable is not null) fields.Add(new("Hạng mục", deliverable));
            }
            if (imageCount > 0) fields.Add(new("Ảnh đính kèm", imageCount.ToString()));
            var oneLine = string.Join(' ', request.Content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var excerpt = oneLine.Length > 200 ? oneLine[..200] + "…" : oneLine;
            var url = Uri.TryCreate(app.PublicBaseUrl, UriKind.Absolute, out var baseUri)
                ? $"{baseUri.AbsoluteUri.TrimEnd('/')}/admin/requests/{request.Id}"
                : null;
            await notifier.EnqueueAsync(new Notification("Yêu cầu mới từ khách", excerpt, url, NotificationSeverity.Info, fields));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not enqueue the client-request notification ({ExceptionType})", ex.GetType().Name);
        }
    }

    private static IResult TooLarge() =>
        Results.Json(new { code = "PAYLOAD_TOO_LARGE", detail = "Nội dung gửi lên quá lớn" },
            statusCode: StatusCodes.Status413PayloadTooLarge);

    private static async Task<List<PortalRequestDto>> ToDtosAsync(
        AppDbContext db, IReadOnlyList<(ClientRequest Request, string ProjectName)> rows)
    {
        var ids = rows.Select(x => x.Request.Id).ToList();
        var deliverableIds = rows.Where(x => x.Request.DeliverableId != null)
            .Select(x => x.Request.DeliverableId!.Value).Distinct().ToList();
        var names = await db.Deliverables.AsNoTracking()
            .Where(d => deliverableIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name);
        // 07-security §4.4: files the client may see — shared by the designer or uploaded by the client
        var attachments = (await db.Files.AsNoTracking()
                .Where(f => f.RequestId != null && ids.Contains(f.RequestId.Value) && f.DeletedAt == null
                            && (f.VisibleToClient || f.UploadedBy == "CLIENT"))
                .OrderBy(f => f.CreatedAt)
                .Select(f => new { RequestId = f.RequestId!.Value, f.Id, f.OriginalName, f.ContentType, IsImage = f.OwnerType != null })
                .ToListAsync())
            .ToLookup(f => f.RequestId, f => new PortalRequestAttachmentDto(f.Id, f.OriginalName, f.ContentType,
                f.IsImage ? ImageDtos.PortalUrl(f.Id, ImageVariants.Thumb) : null,
                f.IsImage ? ImageDtos.PortalUrl(f.Id, ImageVariants.Preview) : null));

        return rows.Select(x => new PortalRequestDto(
            x.Request.Id,
            x.Request.ProjectId,
            x.ProjectName,
            x.Request.DeliverableId,
            x.Request.DeliverableId is { } did ? names.GetValueOrDefault(did) : null,
            x.Request.CreatedAt,
            x.Request.Content,
            x.Request.Status,
            x.Request.DeclineReason,
            attachments[x.Request.Id].ToList())).ToList();
    }
}

/// <summary>Logger category for PortalRequestsEndpoints (static classes cannot be type arguments).</summary>
internal sealed class PortalRequestsEndpointsLog;
