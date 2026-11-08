using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Domain.Tasks;
using FreelanceFlow.Api.Features.Tasks;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Revisions;

public static class RevisionsEndpoints
{
    public static void MapRevisionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization();

        group.MapGet("projects/{projectId:guid}/revisions", GetProjectRevisions);
        group.MapPost("projects/{projectId:guid}/rounds", CreateRound);
        group.MapPost("rounds/{roundId:guid}/items", AddItem);
        group.MapPut("revision-items/{id:guid}", UpdateItem);
        group.MapPost("revision-items/{id:guid}/work-status", ChangeWorkStatus);
        group.MapPost("revision-items/{id:guid}/tasks", CreateTask);
    }

    private sealed record QuotaContext(Guid ClientId, DeliverableQuota? Quota, int UsedRounds);

    private static async Task<IResult> GetProjectRevisions(Guid projectId, AppDbContext db, IClock clock)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Results.NotFound();
        return Results.Ok(await RevisionReads.LoadProjectAsync(db, projectId, clock.TodayVn));
    }

    /// <summary>docs/05 §3.8: a round that did not come through a request.</summary>
    private static async Task<IResult> CreateRound(Guid projectId, RoundBody body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Results.NotFound();
        var errors = RevisionValidation.Round(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        CreatedRounds created;
        try
        {
            created = await RoundCreation.AddAsync(db, activity, project, [body], null, clock.TodayVn);
        }
        catch (DomainException ex)
        {
            return RevisionResults.FromDomain(ex);
        }
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (RevisionResults.IsUniqueViolation(e))
        {
            return RevisionResults.Concurrency();
        }

        var roundId = created.Rounds[0].Id;
        var dto = (await RevisionReads.LoadRoundsAsync(db, r => r.Id == roundId, clock.TodayVn, created.Suggestions)).Single();
        return Results.Created($"/api/admin/projects/{projectId}/revisions", dto);
    }

    private static async Task<IResult> AddItem(Guid roundId, RevisionItemBody body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var round = await db.RevisionRounds.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roundId);
        if (round is null) return Results.NotFound();
        var errors = RevisionValidation.Item(body.Description, body.Type, body.Billing, billingRequired: false);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var context = await QuotaContextAsync(db, round);
        var siblings = await db.RevisionItems.AsNoTracking().Where(i => i.RoundId == roundId).ToListAsync();
        var counted = RevisionQuota.RoundIsCounted(siblings);
        var suggestion = RevisionQuota.SuggestBilling(body.Type!, context.Quota, counted);

        RevisionItem item;
        try
        {
            item = round.AddItem(body.Description!, body.Type!, body.Billing ?? suggestion.Billing,
                (siblings.Count == 0 ? 0 : siblings.Max(i => i.SortOrder)) + 10);
        }
        catch (DomainException ex)
        {
            return RevisionResults.FromDomain(ex);
        }
        db.RevisionItems.Add(item);

        var usedAfter = context.UsedRounds + (!counted && RevisionQuota.CountsTowardQuota(item.Billing, item.WorkStatus) ? 1 : 0);
        activity.Write("revision_item", item.Id, "revision_item.created",
            clientId: context.ClientId, projectId: round.ProjectId,
            data: new
            {
                roundId, type = item.Type, billing = item.Billing, deliverableId = round.DeliverableId,
                usedRoundsBefore = context.UsedRounds, usedRoundsAfter = usedAfter,
            });
        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/projects/{round.ProjectId}/revisions",
            await RevisionReads.LoadItemAsync(db, item.Id, clock.TodayVn, suggestion));
    }

    /// <summary>docs/05 §3.8 PUT: BR-61 billing lock, BR-41, BR-45 audit of the quota change.</summary>
    private static async Task<IResult> UpdateItem(Guid id, UpdateRevisionItemBody body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var errors = RevisionValidation.Item(body.Description, body.Type, body.Billing, billingRequired: true);
        if ((body.InternalNotes?.Length ?? 0) > RevisionValidation.MaxNotes)
            errors["internalNotes"] = [$"Tối đa {RevisionValidation.MaxNotes} ký tự"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var item = await db.RevisionItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return Results.NotFound();
        var round = await db.RevisionRounds.AsNoTracking().FirstAsync(r => r.Id == item.RoundId);
        db.Entry(item).Property<uint>("xmin").OriginalValue = body.RowVersion;

        var context = await QuotaContextAsync(db, round);
        var roundItems = await db.RevisionItems.Where(i => i.RoundId == round.Id).ToListAsync();   // tracked; contains `item`
        var countedBefore = RevisionQuota.RoundIsCounted(roundItems);
        var before = new { type = item.Type, billing = item.Billing };
        try
        {
            item.Edit(body.Description!, body.Type!, body.Billing!, body.InternalNotes ?? "",
                round.DeliverableId != null, await LinkedQuotationStatusAsync(db, item));
        }
        catch (DomainException ex)
        {
            return RevisionResults.FromDomain(ex);
        }
        item.UpdatedAt = clock.UtcNow;   // always touch the row so the xmin check runs

        var usedAfter = UsedAfter(context.UsedRounds, countedBefore, RevisionQuota.RoundIsCounted(roundItems));
        activity.Write("revision_item", item.Id, "revision_item.updated",
            clientId: context.ClientId, projectId: round.ProjectId,
            data: new
            {
                before, after = new { type = item.Type, billing = item.Billing }, deliverableId = round.DeliverableId,
                usedRoundsBefore = context.UsedRounds, usedRoundsAfter = usedAfter,
            });
        return await SaveItemAsync(db, item.Id, clock);
    }

    /// <summary>BR-60 scope guard (428 unless the linked quotation is ACCEPTED or overrideReason ≥ 5 chars).</summary>
    private static async Task<IResult> ChangeWorkStatus(Guid id, WorkStatusBody body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var errors = RevisionValidation.WorkStatus(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var item = await db.RevisionItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return Results.NotFound();
        var round = await db.RevisionRounds.AsNoTracking().FirstAsync(r => r.Id == item.RoundId);
        db.Entry(item).Property<uint>("xmin").OriginalValue = body.RowVersion;

        var context = await QuotaContextAsync(db, round);
        var roundItems = await db.RevisionItems.Where(i => i.RoundId == round.Id).ToListAsync();   // tracked; contains `item`
        var countedBefore = RevisionQuota.RoundIsCounted(roundItems);
        var from = item.WorkStatus;
        bool overridden;
        try
        {
            overridden = item.ChangeWorkStatus(body.To!, body.OverrideReason,
                await LinkedQuotationStatusAsync(db, item) == QuotationStatuses.Accepted);
        }
        catch (DomainException ex)
        {
            return RevisionResults.FromDomain(ex);
        }

        var usedAfter = UsedAfter(context.UsedRounds, countedBefore, RevisionQuota.RoundIsCounted(roundItems));
        activity.Write("revision_item", item.Id, "revision_item.work_status_changed",
            clientId: context.ClientId, projectId: round.ProjectId,
            data: new
            {
                from, to = item.WorkStatus, deliverableId = round.DeliverableId,
                usedRoundsBefore = context.UsedRounds, usedRoundsAfter = usedAfter,
            });
        if (overridden)
            activity.Write("revision_item", item.Id, "revision_item.scope_override",
                clientId: context.ClientId, projectId: round.ProjectId,
                data: new { to = item.WorkStatus, reason = item.ScopeOverrideReason });
        return await SaveItemAsync(db, item.Id, clock);
    }

    /// <summary>REV-06: a task linked to the item (tasks.revision_item_id); the item lists its tasks.</summary>
    private static async Task<IResult> CreateTask(Guid id, CreateItemTaskBody body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var item = await db.RevisionItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return Results.NotFound();
        var errors = RevisionValidation.ItemTask(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var round = await db.RevisionRounds.AsNoTracking().FirstAsync(r => r.Id == item.RoundId);

        var title = string.IsNullOrWhiteSpace(body.Title) ? item.Description : body.Title.Trim();
        if (title.Length > TaskValidation.TitleMaxLength) title = title[..(TaskValidation.TitleMaxLength - 1)] + "…";
        var task = ProjectTask.Create(round.ProjectId, title, item.Description, "TODO", body.Priority ?? "MEDIUM",
            null, body.DueDate, round.DeliverableId,
            await TasksEndpoints.NextSortOrderAsync(db, round.ProjectId, "TODO"), clock);
        task.RevisionItemId = item.Id;
        db.Tasks.Add(task);
        activity.Write("task", task.Id, "task.created",
            clientId: await db.Projects.Where(p => p.Id == round.ProjectId).Select(p => p.ClientId).FirstAsync(),
            projectId: round.ProjectId,
            data: new { title = task.Title, status = task.Status, revisionItemId = item.Id });
        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/tasks/{task.Id}", await TasksEndpoints.LoadDetailAsync(db, task.Id, clock.TodayVn));
    }

    private static async Task<QuotaContext> QuotaContextAsync(AppDbContext db, RevisionRound round)
    {
        var clientId = await db.Projects.Where(p => p.Id == round.ProjectId).Select(p => p.ClientId).FirstAsync();
        if (round.DeliverableId is not { } did) return new QuotaContext(clientId, null, 0);
        var d = await db.Deliverables.AsNoTracking().FirstAsync(x => x.Id == did);
        var used = await RevisionQuota.UsedRounds(db.RevisionRounds.Where(r => r.DeliverableId == did), db.RevisionItems)
            .Select(x => x.UsedRounds)
            .FirstOrDefaultAsync();
        return new QuotaContext(clientId, new DeliverableQuota(d.Name, d.IncludedRounds, used, d.ExtraRoundPrice), used);
    }

    /// <summary>Status of the quotation owning the item's line (null when unlinked).</summary>
    private static async Task<string?> LinkedQuotationStatusAsync(AppDbContext db, RevisionItem item)
    {
        if (item.QuotationItemId is not { } line) return null;
        return await (from qi in db.QuotationItems
                      join q in db.Quotations on qi.QuotationId equals q.Id
                      where qi.Id == line
                      select q.Status).FirstOrDefaultAsync();
    }

    /// <summary>The changed round keeps, gains or loses its one quota round (BR-42).</summary>
    private static int UsedAfter(int usedBefore, bool countedBefore, bool countedAfter) =>
        usedBefore - (countedBefore ? 1 : 0) + (countedAfter ? 1 : 0);

    private static async Task<IResult> SaveItemAsync(AppDbContext db, Guid itemId, IClock clock)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return RevisionResults.Concurrency();
        }
        return Results.Ok(await RevisionReads.LoadItemAsync(db, itemId, clock.TodayVn));
    }
}
