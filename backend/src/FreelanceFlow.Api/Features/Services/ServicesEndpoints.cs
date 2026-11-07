using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Services;

public static class ServicesEndpoints
{
    public static void MapServicesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/services")
            .RequireAuthorization();

        group.MapGet("", GetServices);
        group.MapPost("", CreateService);
        group.MapPut("{id:guid}", UpdateService);
    }

    private static async Task<IResult> GetServices(
        AppDbContext db,
        bool includeInactive = false)
    {
        var query = db.Services.AsNoTracking();
        if (!includeInactive)
            query = query.Where(s => s.Active);

        var items = await query
            .OrderBy(s => s.Name)
            .Select(s => ToDto(s))
            .ToListAsync();

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateService(
        CreateServiceRequest body,
        AppDbContext db)
    {
        var service = new Service
        {
            Id = Guid.CreateVersion7(),
            Name = body.Name,
            Description = body.Description ?? "",
            BasePrice = body.BasePrice,
            DefaultIncludedRounds = body.DefaultIncludedRounds,
            ExtraRoundPrice = body.ExtraRoundPrice,
            Active = true,
        };

        db.Services.Add(service);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("ux_services_name_active") == true)
        {
            return Results.Conflict(new { code = "DUPLICATE", detail = "Tên dịch vụ đã tồn tại" });
        }

        return Results.Created($"/api/admin/services/{service.Id}", ToDto(service));
    }

    private static async Task<IResult> UpdateService(
        Guid id,
        UpdateServiceRequest body,
        AppDbContext db)
    {
        var service = await db.Services.FindAsync(id);
        if (service == null) return Results.NotFound();

        service.Name = body.Name;
        service.Description = body.Description ?? "";
        service.BasePrice = body.BasePrice;
        service.DefaultIncludedRounds = body.DefaultIncludedRounds;
        service.ExtraRoundPrice = body.ExtraRoundPrice;
        service.Active = body.Active;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("ux_services_name_active") == true)
        {
            return Results.Conflict(new { code = "DUPLICATE", detail = "Tên dịch vụ đã tồn tại" });
        }

        return Results.Ok(ToDto(service));
    }

    private static ServiceDto ToDto(Service s) => new(
        s.Id, s.Name, s.Description, s.BasePrice,
        s.DefaultIncludedRounds, s.ExtraRoundPrice, s.Active,
        s.CreatedAt, s.UpdatedAt);
}

record ServiceDto(
    Guid Id, string Name, string Description, long BasePrice,
    int DefaultIncludedRounds, long ExtraRoundPrice, bool Active,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

record CreateServiceRequest(
    string Name, string? Description, long BasePrice,
    int DefaultIncludedRounds, long ExtraRoundPrice);

record UpdateServiceRequest(
    string Name, string? Description, long BasePrice,
    int DefaultIncludedRounds, long ExtraRoundPrice, bool Active);
