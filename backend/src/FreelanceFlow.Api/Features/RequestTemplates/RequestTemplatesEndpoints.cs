using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.RequestTemplates;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.RequestTemplates;

/// <summary>Plan 13: Cài đặt › Mẫu yêu cầu (designer only).</summary>
public static class RequestTemplatesEndpoints
{
    public static void MapRequestTemplatesEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/request-templates").RequireAuthorization();
        admin.MapGet("", async (AppDbContext db, CancellationToken ct) => Results.Ok(await LoadAsync(db, null, ct)));
        admin.MapGet("{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
            (await LoadAsync(db, id, ct)).SingleOrDefault() is { } dto ? Results.Ok(dto) : Results.NotFound());
        admin.MapPost("", Create);
        admin.MapPut("{id:guid}", Update);
        admin.MapDelete("{id:guid}", Delete);
        admin.MapPost("reorder", Reorder);
    }

    private static async Task<List<AdminRequestTemplateDto>> LoadAsync(AppDbContext db, Guid? id, CancellationToken ct)
    {
        var rows = await (from t in db.RequestTemplates.AsNoTracking()
                          join s in db.Services on t.ServiceId equals (Guid?)s.Id into services
                          from s in services.DefaultIfEmpty()
                          where id == null || t.Id == id
                          orderby t.SortOrder, t.CreatedAt
                          select new { Template = t, ServiceName = s == null ? null : s.Name, RowVersion = EF.Property<uint>(t, "xmin") })
            .ToListAsync(ct);
        return rows.Select(r => new AdminRequestTemplateDto(
            r.Template.Id, r.Template.Name, r.Template.Description, r.Template.DesignType, r.Template.ServiceId, r.ServiceName,
            r.Template.Active, r.Template.SortOrder, r.Template.Fields(), r.RowVersion, r.Template.CreatedAt, r.Template.UpdatedAt)).ToList();
    }

    /// <summary>Field errors for the ValidationProblem; <paramref name="currentServiceId"/> may stay even if that service was hidden since.</summary>
    private static async Task<Dictionary<string, string[]>> ValidateAsync(
        RequestTemplateBody body, AppDbContext db, Guid? currentServiceId, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if ((body.Name?.Trim().Length ?? 0) is < 1 or > TemplateLimits.MaxName)
            errors["name"] = [$"Tên mẫu dài 1–{TemplateLimits.MaxName} ký tự"];
        if ((body.Description?.Trim().Length ?? 0) > TemplateLimits.MaxDescription)
            errors["description"] = [$"Mô tả tối đa {TemplateLimits.MaxDescription} ký tự"];
        if (!ProjectDesignTypes.All.Contains(body.DesignType))
            errors["designType"] = ["Loại thiết kế không hợp lệ"];
        if (body.ServiceId is { } sid && sid != currentServiceId && !await db.Services.AnyAsync(s => s.Id == sid && s.Active, ct))
            errors["serviceId"] = ["Dịch vụ không tồn tại hoặc đã ẩn"];
        try
        {
            TemplateFieldRules.Normalize(body.Fields);
        }
        catch (DomainException ex)
        {
            errors["fields"] = [ex.Detail ?? "Câu hỏi không hợp lệ"];
        }
        return errors;
    }

    private static async Task<IResult> Create(RequestTemplateBody body, AppDbContext db, ActivityWriter activity, CancellationToken ct)
    {
        var errors = await ValidateAsync(body, db, null, ct);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (await db.RequestTemplates.CountAsync(ct) >= TemplateLimits.MaxTemplates)
            return Results.Json(new { code = "BUSINESS_RULE", detail = $"Tối đa {TemplateLimits.MaxTemplates} mẫu yêu cầu" },
                statusCode: StatusCodes.Status422UnprocessableEntity);

        var maxOrder = await db.RequestTemplates.MaxAsync(t => (int?)t.SortOrder, ct) ?? 0;
        var template = RequestTemplate.Create(body.Name, body.Description, body.DesignType, body.ServiceId,
            body.Active ?? true, body.Fields, maxOrder + 10);
        db.RequestTemplates.Add(template);
        activity.Write("request_template", template.Id, "request_template.created",
            data: new { name = template.Name, fieldCount = template.Fields().Count, serviceId = template.ServiceId });
        await db.SaveChangesAsync(ct);
        return Results.Json((await LoadAsync(db, template.Id, ct)).Single(), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Update(Guid id, RequestTemplateBody body, AppDbContext db, ActivityWriter activity, CancellationToken ct)
    {
        var template = await db.RequestTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return Results.NotFound();
        var errors = await ValidateAsync(body, db, template.ServiceId, ct);
        if (body.RowVersion is null) errors["rowVersion"] = ["Bắt buộc"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        db.Entry(template).Property<uint>("xmin").OriginalValue = body.RowVersion!.Value;
        template.Update(body.Name, body.Description, body.DesignType, body.ServiceId, body.Active ?? true, body.Fields);
        activity.Write("request_template", template.Id, "request_template.updated",
            data: new { name = template.Name, fieldCount = template.Fields().Count, active = template.Active, serviceId = template.ServiceId });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { code = "CONCURRENCY_CONFLICT", detail = "Mẫu đã được sửa ở nơi khác — vui lòng tải lại trang" });
        }
        return Results.Ok((await LoadAsync(db, id, ct)).Single());
    }

    /// <summary>Hard delete: client_requests / intake_submissions keep template_name + answers (FK ON DELETE SET NULL).</summary>
    private static async Task<IResult> Delete(Guid id, AppDbContext db, ActivityWriter activity, CancellationToken ct)
    {
        var template = await db.RequestTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return Results.NotFound();
        db.RequestTemplates.Remove(template);
        activity.Write("request_template", template.Id, "request_template.deleted", data: new { name = template.Name });
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Reorder(ReorderTemplatesBody body, AppDbContext db, ActivityWriter activity, CancellationToken ct)
    {
        var ids = body.Ids ?? [];
        var all = await db.RequestTemplates.ToListAsync(ct);
        if (ids.Length != all.Count || ids.Distinct().Count() != ids.Length || all.Any(t => !ids.Contains(t.Id)))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["ids"] = ["Danh sách mẫu đã thay đổi — vui lòng tải lại trang"] });
        var byId = all.ToDictionary(t => t.Id);
        for (var i = 0; i < ids.Length; i++) byId[ids[i]].SortOrder = (i + 1) * 10;
        activity.Write("request_template", Guid.Empty, "request_template.reordered", data: new { count = ids.Length });
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
