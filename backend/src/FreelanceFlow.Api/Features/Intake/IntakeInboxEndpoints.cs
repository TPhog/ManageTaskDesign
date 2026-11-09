using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Catalog;
using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Domain.Intake;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Features.Images;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Intake;

/// <summary>Plan 13 "Khách mới" inbox (designer only). BR-01: every outcome is a designer click.</summary>
public static class IntakeInboxEndpoints
{
    private const string LogCategory = "FreelanceFlow.Api.Features.Intake.IntakeInbox";

    public static void MapIntakeInboxEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/intake").RequireAuthorization();
        admin.MapGet("", List);
        admin.MapGet("new-count", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(new { count = await db.IntakeSubmissions.CountAsync(s => s.Status == IntakeStatuses.New, ct) }));
        admin.MapGet("{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
            await LoadDetailAsync(db, id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound());
        admin.MapPost("{id:guid}/convert", Convert);
        admin.MapPost("{id:guid}/reject", Reject);
        admin.MapDelete("{id:guid}", Delete);
    }

    private static IQueryable<ProjectFile> ImagesOf(AppDbContext db, Guid id) =>
        db.Files.Where(f => f.OwnerType == ImageOwnerTypes.IntakeSubmission && f.OwnerId == id && f.DeletedAt == null);

    private static async Task<IResult> List(AppDbContext db, CancellationToken ct, string? status = null, int page = 1, int pageSize = 20)
    {
        status ??= IntakeStatuses.New;
        if (!IntakeStatuses.All.Contains(status))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Trạng thái không hợp lệ (NEW | CONVERTED | REJECTED)"] });
        page = Math.Clamp(page, 1, 100_000);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.IntakeSubmissions.AsNoTracking().Where(s => s.Status == status);
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.Id, s.CreatedAt, s.Status, s.ContactName, s.ContactPhone, s.ContactEmail, s.TemplateName, s.Body,
                ImageCount = db.Files.Count(f => f.OwnerType == ImageOwnerTypes.IntakeSubmission && f.OwnerId == s.Id && f.DeletedAt == null),
            })
            .ToListAsync(ct);
        var items = rows.Select(r => new IntakeListItemDto(r.Id, r.CreatedAt, r.Status, r.ContactName, r.ContactPhone, r.ContactEmail,
            r.TemplateName, IntakeDtos.Excerpt(r.Body), r.ImageCount)).ToList();
        return Results.Ok(new { items, page, pageSize, total });
    }

    private static async Task<IntakeDetailDto?> LoadDetailAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        var row = await db.IntakeSubmissions.AsNoTracking().Where(s => s.Id == id)
            .Select(s => new { Submission = s, RowVersion = EF.Property<uint>(s, "xmin") })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;
        var s = row.Submission;

        var images = (await ImagesOf(db, id).AsNoTracking().OrderBy(f => f.CreatedAt).ThenBy(f => f.Id).ToListAsync(ct))
            .Select(f => new IntakeImageDto(f.Id, f.Width ?? 0, f.Height ?? 0, f.SizeBytes, f.CreatedAt,
                ImageDtos.AdminUrl(f.Id, ImageVariants.Thumb), ImageDtos.AdminUrl(f.Id, ImageVariants.Preview),
                ImageDtos.AdminUrl(f.Id, ImageVariants.Original)))
            .ToList();

        // suggestions come from the template as it is now; a hidden service is not suggested
        var template = s.TemplateId is not { } tid ? null : await (
                from t in db.RequestTemplates.AsNoTracking()
                join sv in db.Services on t.ServiceId equals (Guid?)sv.Id into services
                from sv in services.DefaultIfEmpty()
                where t.Id == tid
                select new
                {
                    t.DesignType,
                    ServiceId = sv != null && sv.Active ? (Guid?)sv.Id : null,
                    ServiceName = sv != null && sv.Active ? sv.Name : null,
                })
            .FirstOrDefaultAsync(ct);

        var email = s.ContactEmail?.Trim().ToLowerInvariant();
        var candidates = await db.Clients.AsNoTracking()
            .Where(c => c.Status == "ACTIVE" && (c.Phone != null || c.Email != null))
            .Select(c => new { c.Id, c.Name, c.Phone, c.Email })
            .ToListAsync(ct);   // single designer: a few hundred rows at most
        var matching = candidates
            .Where(c => IntakeDtos.SamePhone(c.Phone, s.ContactPhone)
                        || (email is not null && c.Email is not null && c.Email.Trim().ToLowerInvariant() == email))
            .Take(5)
            .Select(c => new IntakeMatchingClientDto(c.Id, c.Name, c.Phone, c.Email))
            .ToList();

        var answers = s.Answers();
        return new IntakeDetailDto(
            s.Id, s.CreatedAt, s.Status, s.ContactName, s.ContactPhone, s.ContactEmail, s.TemplateName, IntakeDtos.Excerpt(s.Body),
            images.Count, s.TemplateId, answers, s.Note, s.Body, images,
            template?.DesignType ?? ProjectDesignTypes.Other, template?.ServiceId, template?.ServiceName, IntakeDtos.SuggestedDueDate(answers),
            s.RejectReason, s.RejectedAt, s.ConvertedClientId, s.ConvertedProjectId, s.ConvertedAt, matching, row.RowVersion);
    }

    /// <summary>
    /// BR-136, one transaction: client (new or existing ACTIVE) + project DRAFT + brief v1 DRAFT (= body) + deliverable from
    /// the chosen service + the photos joined to the project (their intake/ storage keys stay) + submission CONVERTED + activities.
    /// </summary>
    private static async Task<IResult> Convert(Guid id, ConvertIntakeBody body, AppDbContext db, ActivityWriter activity, IClock clock,
        CancellationToken ct)
    {
        var submission = await db.IntakeSubmissions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (submission is null) return Results.NotFound();

        var errors = new Dictionary<string, string[]>();
        if (body.RowVersion is null) errors["rowVersion"] = ["Bắt buộc"];
        var projectName = body.ProjectName?.Trim() ?? "";
        if (projectName.Length is < 1 or > IntakeDtos.MaxName) errors["projectName"] = [$"Tên dự án dài 1–{IntakeDtos.MaxName} ký tự"];
        if (!ProjectDesignTypes.All.Contains(body.DesignType)) errors["designType"] = ["Loại thiết kế không hợp lệ"];
        var clientName = body.ClientName?.Trim() ?? "";
        if (body.ExistingClientId is null && clientName.Length is < 1 or > IntakeDtos.MaxName)
            errors["clientName"] = [$"Nhập tên khách (1–{IntakeDtos.MaxName} ký tự) hoặc chọn khách có sẵn"];
        var deliverableName = string.IsNullOrWhiteSpace(body.DeliverableName) ? null : body.DeliverableName.Trim();
        if (deliverableName is { Length: > IntakeDtos.MaxName }) errors["deliverableName"] = [$"Tối đa {IntakeDtos.MaxName} ký tự"];
        Service? service = null;
        if (body.ServiceId is { } serviceId)
        {
            service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId && s.Active, ct);
            if (service is null) errors["serviceId"] = ["Dịch vụ không tồn tại hoặc đã ẩn"];
        }
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (submission.Status != IntakeStatuses.New)
            return Results.Conflict(new { code = "INVALID_TRANSITION", detail = "Hồ sơ này đã được xử lý" });

        Guid clientId;
        if (body.ExistingClientId is { } existing)
        {
            if (!await db.Clients.AnyAsync(c => c.Id == existing && c.Status == "ACTIVE", ct))
                return Results.NotFound(new { code = "NOT_FOUND", detail = "Khách hàng không tồn tại hoặc đã lưu trữ" });
            clientId = existing;
        }
        else
        {
            var client = new Client
            {
                Name = clientName,
                Phone = submission.ContactPhone,
                Email = submission.ContactEmail,
                ContactChannel = "Form khách mới",
            };
            db.Clients.Add(client);
            activity.Write("client", client.Id, "client.created", clientId: client.Id, data: new { source = "intake" });
            clientId = client.Id;
        }

        var project = new Project { ClientId = clientId, Name = projectName, DesignType = body.DesignType!, DueDate = body.DueDate };
        db.Projects.Add(project);
        db.ProjectBriefs.Add(new ProjectBrief { ProjectId = project.Id, Version = 1, Status = "DRAFT", Content = submission.Body });   // BR-25/26
        activity.Write("project", project.Id, "project.created", clientId: clientId, projectId: project.Id,
            data: new { name = project.Name, clientId, source = "intake" });

        Deliverable? deliverable = null;
        if (service is not null)
        {
            deliverable = new Deliverable
            {
                ProjectId = project.Id,
                ServiceId = service.Id,
                Name = deliverableName ?? service.Name,
                Quantity = 1,
                IncludedRounds = service.DefaultIncludedRounds,
                ExtraRoundPrice = service.ExtraRoundPrice,
                DueDate = body.DueDate,
                SortOrder = 10,
            };
            db.Deliverables.Add(deliverable);
            activity.Write("deliverable", deliverable.Id, "deliverable.created", clientId: clientId, projectId: project.Id,
                data: new { name = deliverable.Name, source = "intake" });
        }

        foreach (var image in await ImagesOf(db, id).ToListAsync(ct)) image.AttachToProject(project.Id);
        db.Entry(submission).Property<uint>("xmin").OriginalValue = body.RowVersion!.Value;
        submission.Convert(clientId, project.Id, clock.UtcNow);
        activity.Write("intake_submission", submission.Id, "intake.converted", clientId: clientId, projectId: project.Id,
            data: new { clientId, projectId = project.Id, deliverableId = deliverable?.Id, newClient = body.ExistingClientId is null });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        return Results.Ok(new ConvertIntakeResultDto(clientId, project.Id, deliverable?.Id));
    }

    private static async Task<IResult> Reject(Guid id, RejectIntakeBody body, AppDbContext db, ActivityWriter activity, IClock clock,
        CancellationToken ct)
    {
        var submission = await db.IntakeSubmissions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (submission is null) return Results.NotFound();
        var errors = new Dictionary<string, string[]>();
        if (body.RowVersion is null) errors["rowVersion"] = ["Bắt buộc"];
        if ((body.Reason?.Trim().Length ?? 0) > IntakeSubmission.MaxRejectReason)
            errors["reason"] = [$"Tối đa {IntakeSubmission.MaxRejectReason} ký tự"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (submission.Status != IntakeStatuses.New)
            return Results.Conflict(new { code = "INVALID_TRANSITION", detail = "Hồ sơ này đã được xử lý" });

        db.Entry(submission).Property<uint>("xmin").OriginalValue = body.RowVersion!.Value;
        submission.Reject(body.Reason, clock.UtcNow);
        activity.Write("intake_submission", submission.Id, "intake.rejected", data: new { hasReason = submission.RejectReason is not null });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        return Results.Ok(await LoadDetailAsync(db, id, ct));
    }

    /// <summary>Spam / mistakes: the row goes, its photos are soft-deleted (frees quota), the activity stays.</summary>
    private static async Task<IResult> Delete(Guid id, AppDbContext db, ActivityWriter activity, IClock clock, IFileStorage storage,
        StorageAlertService storageAlerts, ILoggerFactory loggers, CancellationToken ct)
    {
        var submission = await db.IntakeSubmissions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (submission is null) return Results.NotFound();
        try
        {
            submission.EnsureDeletable();
        }
        catch (DomainException ex)
        {
            return Results.Conflict(new { code = ex.Code, detail = ex.Detail });
        }

        var images = await ImagesOf(db, id).ToListAsync(ct);
        db.IntakeSubmissions.Remove(submission);
        activity.Write("intake_submission", id, "intake.deleted", data: new { imageCount = images.Count });
        if (images.Count == 0)
        {
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }
        // one SaveChanges inside: the row removal, the soft deletes and their activities commit together
        await ImageDeletion.DeleteAsync(db, storage, activity, clock, loggers.CreateLogger(LogCategory), images, bulk: false, ct);
        await storageAlerts.EvaluateSafelyAsync(CancellationToken.None);
        return Results.NoContent();
    }

    private static IResult Conflict() =>
        Results.Conflict(new { code = "CONCURRENCY_CONFLICT", detail = "Hồ sơ vừa được cập nhật ở nơi khác — vui lòng tải lại" });
}
