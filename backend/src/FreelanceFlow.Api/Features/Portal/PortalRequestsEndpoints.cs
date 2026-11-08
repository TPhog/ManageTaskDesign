using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Features.Revisions;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Portal;

/// <summary>REQ-01 / REQ-06: the client sends change requests and follows their status.</summary>
public static class PortalRequestsEndpoints
{
    public static void MapPortalRequestsEndpoints(this IEndpointRouteBuilder app)
    {
        var portal = app.MapGroup("/api/portal").RequireAuthorization(PortalPrincipal.Policy);

        portal.MapGet("requests", ListRequests);
        portal.MapPost("projects/{id:guid}/requests", SendRequest);
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
    /// REQ-01, BR-21/53/54. Multipart per docs/05 §4; text only until M3 (plan 08), so a file part is refused.
    /// The body is never logged (07-security).
    /// </summary>
    private static async Task<IResult> SendRequest(
        Guid id,
        HttpRequest http,
        [FromServices] PortalPrincipal principal,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
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
        var form = await http.ReadFormAsync();
        var content = form["content"].ToString();

        var errors = new Dictionary<string, string[]>();
        if (content.Trim().Length is < 1 or > ClientRequest.MaxContent)
            errors["content"] = [$"Nội dung dài 1–{ClientRequest.MaxContent} ký tự"];
        Guid? deliverableId = null;
        var rawDeliverable = form["deliverableId"].ToString();
        if (rawDeliverable.Length > 0)
        {
            if (Guid.TryParse(rawDeliverable, out var did) && await db.Deliverables.AnyAsync(d => d.Id == did && d.ProjectId == id))
                deliverableId = did;
            else
                errors["deliverableId"] = ["Hạng mục không thuộc dự án này"];
        }
        if (form.Files.Count > 0)
            errors["files"] = ["Chưa hỗ trợ đính kèm ảnh — vui lòng chỉ gửi nội dung"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        // BR-54: portal requests of this client since 00:00 VN (designer-recorded requests do not count)
        var dayStart = ClientRequest.DayStartUtc(clock.TodayVn);
        var sentToday = await (from r in db.ClientRequests
                               join p in db.Projects on r.ProjectId equals p.Id
                               where p.ClientId == principal.ClientId && r.CreatedBy == "CLIENT" && r.CreatedAt >= dayStart
                               select r.Id).CountAsync();

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
        await db.SaveChangesAsync();

        var dto = (await ToDtosAsync(db, [(request, project.Name)])).Single();
        return Results.Json(dto, statusCode: StatusCodes.Status201Created);
    }

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
                .Select(f => new { RequestId = f.RequestId!.Value, f.Id, f.OriginalName, f.ContentType })
                .ToListAsync())
            .ToLookup(f => f.RequestId, f => new PortalRequestAttachmentDto(f.Id, f.OriginalName, f.ContentType));

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
