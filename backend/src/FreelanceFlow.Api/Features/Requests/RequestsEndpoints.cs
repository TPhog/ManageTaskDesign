using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Features.Revisions;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Requests;

public static class RequestsEndpoints
{
    public static void MapRequestsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization();

        group.MapGet("requests", ListRequests);
        group.MapGet("requests/new-count", NewCount);
        group.MapGet("requests/{id:guid}", GetRequest);
        group.MapPost("projects/{projectId:guid}/requests", CreateRequest);
        group.MapPost("requests/{id:guid}/triage", Triage);
        group.MapPost("requests/{id:guid}/decline", Decline);
        group.MapPost("revisions/suggest-billing", SuggestBilling);
    }

    private static async Task<IResult> ListRequests(
        AppDbContext db, string? status = null, Guid? projectId = null, Guid? clientId = null, string? q = null,
        int page = 1, int pageSize = 20)
    {
        if (status is not null && !RequestStatuses.All.Contains(status))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Trạng thái không hợp lệ (NEW | TRIAGED | DECLINED)"],
            });
        page = Math.Clamp(page, 1, 100_000);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ClientRequests.AsNoTracking();
        if (status is not null) query = query.Where(r => r.Status == status);
        if (projectId is { } pid) query = query.Where(r => r.ProjectId == pid);
        if (clientId is { } cid) query = query.Where(r => db.Projects.Any(p => p.Id == r.ProjectId && p.ClientId == cid));
        // QA D-06: search the request text, its project and its client
        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.Trim().ToLowerInvariant();
            query = query.Where(r => r.Content.ToLower().Contains(lower) ||
                db.Projects.Any(p => p.Id == r.ProjectId &&
                    (p.Name.ToLower().Contains(lower) || db.Clients.Any(c => c.Id == p.ClientId && c.Name.ToLower().Contains(lower)))));
        }

        var total = await query.CountAsync();
        var ids = await query
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.Id)
            .ToListAsync();
        var items = await RequestReads.LoadAsync(db, ids);

        return Results.Ok(new { items, page, pageSize, total });
    }

    /// <summary>REQ-03: badge on the "Yêu cầu" menu.</summary>
    private static async Task<IResult> NewCount(AppDbContext db) =>
        Results.Ok(new NewRequestCountDto(await db.ClientRequests.CountAsync(r => r.Status == RequestStatuses.New)));

    private static async Task<IResult> GetRequest(Guid id, AppDbContext db, IClock clock)
    {
        var detail = await RequestReads.LoadDetailAsync(db, id, clock.TodayVn);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    /// <summary>REQ-02: designer records a request on the client's behalf.</summary>
    private static async Task<IResult> CreateRequest(
        Guid projectId, CreateRequestBody body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.NotFound();

        var errors = RevisionValidation.CreateRequest(body);
        if (body.DeliverableId is { } did && !await db.Deliverables.AnyAsync(d => d.Id == did && d.ProjectId == projectId))
            errors["deliverableId"] = ["Hạng mục không thuộc dự án này"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (project.Status == "CANCELLED")
            return RevisionResults.BusinessRule("Dự án đã huỷ — không ghi nhận yêu cầu mới");

        var request = ClientRequest.CreateByDesigner(projectId, body.DeliverableId, body.Content!, body.Source!);
        db.ClientRequests.Add(request);
        activity.Write("request", request.Id, "request.created",
            clientId: project.ClientId, projectId: projectId,
            data: new { source = request.Source, createdBy = request.CreatedBy, deliverableId = request.DeliverableId });
        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/requests/{request.Id}", await RequestReads.LoadDetailAsync(db, request.Id, clock.TodayVn));
    }

    /// <summary>REQ-04, BR-50/51: NEW → TRIAGED and 1..n rounds, one transaction.</summary>
    private static async Task<IResult> Triage(Guid id, TriageBody body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var errors = RevisionValidation.Rounds(body.Rounds);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var request = await db.ClientRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (request is null) return Results.NotFound();
        var project = await db.Projects.AsNoTracking().FirstAsync(p => p.Id == request.ProjectId);
        db.Entry(request).Property<uint>("xmin").OriginalValue = body.RowVersion;

        CreatedRounds created;
        try
        {
            request.MarkTriaged(clock.UtcNow);
            created = await RoundCreation.AddAsync(db, activity, project, body.Rounds!, request.Id, clock.TodayVn);
        }
        catch (DomainException ex)
        {
            return RevisionResults.FromDomain(ex);
        }

        activity.Write("request", request.Id, "request.triaged",
            clientId: project.ClientId, projectId: project.Id,
            data: new { rounds = created.Rounds.Select(r => new { r.Id, r.DeliverableId, r.RoundNo }).ToArray() },
            visibleToClient: true,
            clientMessage: "Yêu cầu của bạn đã được tiếp nhận");
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return RevisionResults.Concurrency();
        }
        catch (DbUpdateException e) when (RevisionResults.IsUniqueViolation(e))
        {
            return RevisionResults.Concurrency();
        }

        return Results.Ok(await RequestReads.LoadDetailAsync(db, id, clock.TodayVn, created.Suggestions));
    }

    /// <summary>REQ-05, BR-52: NEW → DECLINED with a reason the client sees.</summary>
    private static async Task<IResult> Decline(Guid id, DeclineBody body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var errors = RevisionValidation.Decline(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var request = await db.ClientRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (request is null) return Results.NotFound();
        db.Entry(request).Property<uint>("xmin").OriginalValue = body.RowVersion;
        try
        {
            request.Decline(body.Reason);
        }
        catch (DomainException ex)
        {
            return RevisionResults.FromDomain(ex);
        }

        var clientId = await db.Projects.Where(p => p.Id == request.ProjectId).Select(p => p.ClientId).FirstAsync();
        activity.Write("request", request.Id, "request.declined",
            clientId: clientId, projectId: request.ProjectId,
            data: new { reason = request.DeclineReason },
            visibleToClient: true,
            clientMessage: $"Yêu cầu bị từ chối: {request.DeclineReason}");
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return RevisionResults.Concurrency();
        }

        return Results.Ok(await RequestReads.LoadDetailAsync(db, id, clock.TodayVn));
    }

    /// <summary>docs/05 §3.7.1: preview of the billing a new round of these item types would get. Saves nothing.</summary>
    private static async Task<IResult> SuggestBilling(SuggestBillingBody body, AppDbContext db)
    {
        if (body.Items is null || body.Items.Length is 0 or > RevisionValidation.MaxItemsPerRound
            || body.Items.Any(i => i.Type is null || !RevisionItemTypes.All.Contains(i.Type)
                                   || (i.Billing is not null && !RevisionBillings.All.Contains(i.Billing))))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["items"] = ["Danh sách loại mục không hợp lệ"],
            });

        DeliverableQuota? quota = null;
        if (body.DeliverableId is { } did)
        {
            var d = await db.Deliverables.AsNoTracking().FirstOrDefaultAsync(x => x.Id == did);
            if (d is null) return Results.NotFound();
            var used = await RevisionQuota.UsedRounds(db.RevisionRounds.Where(r => r.DeliverableId == did), db.RevisionItems)
                .Select(x => x.UsedRounds)
                .FirstOrDefaultAsync();
            quota = new DeliverableQuota(d.Name, d.IncludedRounds, used, d.ExtraRoundPrice);
        }

        var plan = RevisionQuota.PlanRound(quota, body.Items.Select(i => new PlannedItemInput(i.Type!, i.Billing)).ToList());
        return Results.Ok(plan.Items.Select(i => new SuggestionDto(i.SuggestedBilling, i.SuggestionReason)).ToList());
    }
}
