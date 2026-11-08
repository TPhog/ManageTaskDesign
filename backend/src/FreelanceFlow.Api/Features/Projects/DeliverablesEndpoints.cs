using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Revisions;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Projects;

public static class DeliverablesEndpoints
{
    public static void MapDeliverablesEndpoints(this IEndpointRouteBuilder app)
    {
        var adminGroup = app.MapGroup("/api/admin").RequireAuthorization();

        adminGroup.MapPost("projects/{projectId:guid}/deliverables", CreateDeliverable);
        adminGroup.MapPut("deliverables/{id:guid}", UpdateDeliverable);
        adminGroup.MapPost("deliverables/{id:guid}/status", ChangeStatus);
    }

    private static async Task<IResult> CreateDeliverable(
        Guid projectId,
        CreateDeliverableRequest body,
        AppDbContext db,
        ActivityWriter activity)
    {
        var project = await db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) return Results.NotFound();

        int includedRounds = body.IncludedRounds ?? 0;
        long extraRoundPrice = body.ExtraRoundPrice ?? 0;

        // If serviceId provided, copy defaults from service when not explicitly specified
        if (body.ServiceId.HasValue)
        {
            var service = await db.Services.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == body.ServiceId.Value);
            if (service == null)
                return Results.NotFound(new { code = "NOT_FOUND", detail = "Dịch vụ không tồn tại" });

            if (!body.IncludedRounds.HasValue)
                includedRounds = service.DefaultIncludedRounds;
            if (!body.ExtraRoundPrice.HasValue)
                extraRoundPrice = service.ExtraRoundPrice;
        }
        else
        {
            // No service: includedRounds and extraRoundPrice are required
            if (!body.IncludedRounds.HasValue)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["includedRounds"] = ["Bắt buộc khi không chọn dịch vụ"]
                });
        }

        var maxSortOrder = await db.Deliverables
            .Where(d => d.ProjectId == projectId)
            .MaxAsync(d => (int?)d.SortOrder) ?? 0;

        var deliverable = new Deliverable
        {
            ProjectId = projectId,
            ServiceId = body.ServiceId,
            Name = body.Name,
            Quantity = body.Quantity > 0 ? body.Quantity : 1,
            IncludedRounds = includedRounds,
            ExtraRoundPrice = extraRoundPrice,
            DueDate = body.DueDate,
            SortOrder = maxSortOrder + 10,
        };

        db.Deliverables.Add(deliverable);

        activity.Write("deliverable", deliverable.Id, "deliverable.created",
            clientId: project.ClientId, projectId: projectId,
            data: new { name = body.Name });

        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/deliverables/{deliverable.Id}", ToDto(deliverable, 0));
    }

    private static async Task<IResult> UpdateDeliverable(
        Guid id,
        UpdateDeliverableRequest body,
        AppDbContext db,
        ActivityWriter activity)
    {
        var deliverable = await db.Deliverables.FindAsync(id);
        if (deliverable == null) return Results.NotFound();

        // Compute usedRounds for BR-44 warning
        var usedRounds = await ComputeUsedRounds(db, id);
        var warning = deliverable.ValidateIncludedRoundsChange(body.IncludedRounds, usedRounds);

        deliverable.Name = body.Name;
        deliverable.Quantity = body.Quantity > 0 ? body.Quantity : 1;
        deliverable.IncludedRounds = body.IncludedRounds;
        deliverable.ExtraRoundPrice = body.ExtraRoundPrice;
        deliverable.DueDate = body.DueDate;
        deliverable.SortOrder = body.SortOrder;

        activity.Write("deliverable", deliverable.Id, "deliverable.updated",
            projectId: deliverable.ProjectId,
            data: new { name = body.Name, includedRounds = body.IncludedRounds });

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            Deliverable = ToDto(deliverable, usedRounds),
            Warnings = warning != null ? new[] { warning } : Array.Empty<string>(),
        });
    }

    private static async Task<IResult> ChangeStatus(
        Guid id,
        ChangeDeliverableStatusRequest body,
        AppDbContext db,
        ActivityWriter activity)
    {
        var deliverable = await db.Deliverables.FindAsync(id);
        if (deliverable == null) return Results.NotFound();

        var warning = deliverable.ValidateStatusTransition(body.To);
        if (warning != null)
            return Results.Conflict(new { code = "INVALID_TRANSITION", detail = warning });

        var fromStatus = deliverable.Status;
        deliverable.Status = body.To;

        // Get clientId for activity log
        var clientId = await db.Projects
            .Where(p => p.Id == deliverable.ProjectId)
            .Select(p => p.ClientId)
            .FirstOrDefaultAsync();

        var clientMessage = body.To switch
        {
            "IN_REVIEW" => $"{deliverable.Name}: Chờ duyệt",
            "APPROVED" => $"{deliverable.Name}: Đã duyệt",
            "DELIVERED" => $"{deliverable.Name}: Đã bàn giao",
            _ => null,
        };

        activity.Write("deliverable", deliverable.Id, "deliverable.status_changed",
            clientId: clientId == Guid.Empty ? null : clientId,
            projectId: deliverable.ProjectId,
            data: new { from = fromStatus, to = body.To, name = deliverable.Name },
            visibleToClient: clientMessage != null,
            clientMessage: clientMessage);

        await db.SaveChangesAsync();

        var usedRounds = await ComputeUsedRounds(db, id);
        return Results.Ok(ToDto(deliverable, usedRounds));
    }

    // used_rounds (docs/04 §4.1): the single query lives in Domain/Revisions
    private static Task<int> ComputeUsedRounds(AppDbContext db, Guid deliverableId) =>
        RevisionQuota.UsedRounds(db.RevisionRounds.Where(r => r.DeliverableId == deliverableId), db.RevisionItems)
            .Select(x => x.UsedRounds)
            .FirstOrDefaultAsync();

    private static object ToDto(Deliverable d, int usedRounds) => new
    {
        d.Id,
        d.ProjectId,
        d.ServiceId,
        d.Name,
        d.Quantity,
        d.IncludedRounds,
        UsedRounds = usedRounds,
        RemainingRounds = Math.Max(0, d.IncludedRounds - usedRounds),
        d.ExtraRoundPrice,
        d.Status,
        d.DueDate,
        d.SortOrder,
        d.CreatedAt,
        d.UpdatedAt,
    };
}

record CreateDeliverableRequest(
    string Name,
    int Quantity,
    Guid? ServiceId,
    int? IncludedRounds,
    long? ExtraRoundPrice,
    DateOnly? DueDate);

record UpdateDeliverableRequest(
    string Name,
    int Quantity,
    int IncludedRounds,
    long ExtraRoundPrice,
    DateOnly? DueDate,
    int SortOrder);

record ChangeDeliverableStatusRequest(string To);
