using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Clients;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Features.Clients;

public static class ClientsEndpoints
{
    public static void MapClientsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/clients").RequireAuthorization();

        group.MapGet("", ListClients);
        group.MapPost("", CreateClient);
        group.MapGet("{id:guid}", GetClient);
        group.MapPut("{id:guid}", UpdateClient);
        group.MapPost("{id:guid}/archive", ArchiveClient);
        group.MapPost("{id:guid}/portal-link", CreatePortalLink);
        group.MapPost("{id:guid}/portal/enable", EnablePortal);
        group.MapPost("{id:guid}/portal/disable", DisablePortal);
        group.MapPost("{id:guid}/portal/revoke", RevokePortalLink);
        group.MapGet("{id:guid}/activity", GetActivity);
    }

    private static async Task<IResult> ListClients(
        AppDbContext db,
        string? q = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        string? sort = null)
    {
        pageSize = Math.Min(pageSize, 100);
        var query = db.Clients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.ToLowerInvariant();
            query = query.Where(c =>
                c.Name.ToLower().Contains(lower) ||
                (c.Company != null && c.Company.ToLower().Contains(lower)) ||
                (c.Phone != null && c.Phone.Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status.ToUpperInvariant());

        var total = await query.CountAsync();

        // QA D-07: pickers show the newest clients first; the list page keeps the name order
        var ordered = sort == "createdAt:desc"
            ? query.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
            : query.OrderBy(c => c.Name).ThenBy(c => c.Id);
        var clients = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Company,
                c.Phone,
                c.Status,
                c.PortalEnabled,
                ProjectCount = db.Projects.Count(p => p.ClientId == c.Id),
                AcceptedTotal = db.Quotations
                    .Where(q => db.Projects.Any(p => p.Id == q.ProjectId && p.ClientId == c.Id) &&
                                q.Status == "ACCEPTED")
                    .Sum(q => (long?)q.Total) ?? 0L,
                // BR-73: UNPAID -> total, DEPOSIT_PAID -> total - deposit, PAID -> 0 (same as GET /clients/{id})
                Outstanding = db.Quotations
                    .Where(q => db.Projects.Any(p => p.Id == q.ProjectId && p.ClientId == c.Id) &&
                                q.Status == "ACCEPTED" && q.PaymentStatus != "PAID")
                    .Sum(q => (long?)(q.PaymentStatus == "DEPOSIT_PAID" ? q.Total - q.DepositAmount : q.Total)) ?? 0L,
            })
            .ToListAsync();

        return Results.Ok(new { items = clients, page, pageSize, total });
    }

    private static async Task<IResult> CreateClient(
        CreateClientRequest body,
        AppDbContext db,
        ActivityWriter activity)
    {
        var client = new Client
        {
            Name = body.Name,
            Company = body.Company,
            Phone = body.Phone,
            Email = body.Email,
            ContactChannel = body.ContactChannel,
            Notes = body.Notes ?? "",
        };

        db.Clients.Add(client);
        activity.Write("client", client.Id, "client.created", clientId: client.Id);
        await db.SaveChangesAsync();

        return Results.Created($"/api/admin/clients/{client.Id}", ToDetailDto(client, [], 0, 0, 0, 0, 0));
    }

    private static async Task<IResult> GetClient(Guid id, AppDbContext db, IClock clock)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (client == null) return Results.NotFound();

        var projectCount = await db.Projects.CountAsync(p => p.ClientId == id);

        var quotationStats = await db.Quotations
            .Where(q => db.Projects.Any(p => p.Id == q.ProjectId && p.ClientId == id) &&
                        q.Status == "ACCEPTED")
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AcceptedTotal = g.Sum(q => q.Total),
                Outstanding = g.Sum(q =>
                    q.PaymentStatus == "PAID" ? 0L :
                    q.PaymentStatus == "DEPOSIT_PAID" ? q.Total - q.DepositAmount :
                    q.Total),
            })
            .FirstOrDefaultAsync();

        var pendingQuotations = await db.Quotations
            .Where(q => db.Projects.Any(p => p.Id == q.ProjectId && p.ClientId == id) &&
                        q.Status == "SENT")
            .SumAsync(q => (long?)q.Total) ?? 0L;

        var newRequests = await db.ClientRequests
            .CountAsync(r => r.Status == "NEW" &&
                             db.Projects.Any(p => p.Id == r.ProjectId && p.ClientId == id));

        var rowVersion = await db.Clients
            .Where(c => c.Id == id)
            .Select(c => EF.Property<uint>(c, "xmin"))
            .FirstOrDefaultAsync();

        return Results.Ok(new
        {
            client.Id,
            client.Name,
            client.Company,
            client.Phone,
            client.Email,
            client.ContactChannel,
            client.Notes,
            client.Status,
            client.PortalEnabled,
            client.PortalTokenVersion,
            client.PortalTokenCreatedAt,
            HasPortalLink = client.PortalTokenHash != null,
            client.ArchivedAt,
            client.CreatedAt,
            client.UpdatedAt,
            RowVersion = rowVersion,
            ProjectCount = projectCount,
            AcceptedTotal = quotationStats?.AcceptedTotal ?? 0,
            Outstanding = quotationStats?.Outstanding ?? 0,
            PendingQuotationTotal = pendingQuotations,
            NewRequests = newRequests,
        });
    }

    private static async Task<IResult> UpdateClient(
        Guid id,
        UpdateClientRequest body,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var client = await db.Clients.FindAsync(id);
        if (client == null) return Results.NotFound();

        if (client.Status == "INACTIVE")
            return Results.Conflict(new { code = "BUSINESS_RULE", detail = "Không thể sửa khách đã lưu trữ" });

        client.Name = body.Name;
        client.Company = body.Company;
        client.Phone = body.Phone;
        client.Email = body.Email;
        client.ContactChannel = body.ContactChannel;
        client.Notes = body.Notes ?? "";

        activity.Write("client", client.Id, "client.updated", clientId: client.Id,
            data: new { name = body.Name });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { code = "CONCURRENCY_CONFLICT", detail = "Dữ liệu đã thay đổi, vui lòng tải lại" });
        }

        return Results.Ok(new { client.Id, client.Name, client.Company, client.Phone, client.Status, client.UpdatedAt });
    }

    private static async Task<IResult> ArchiveClient(
        Guid id,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock)
    {
        var client = await db.Clients.FindAsync(id);
        if (client == null) return Results.NotFound();
        if (client.Status == "INACTIVE")
            return Results.Ok(new { message = "Đã lưu trữ" });

        var hasActive = await db.Projects
            .AnyAsync(p => p.ClientId == id &&
                           (p.Status == "IN_PROGRESS" || p.Status == "ON_HOLD"));

        try
        {
            client.Archive(clock, hasActive);
        }
        catch (DomainException ex)
        {
            return Results.Conflict(new { code = ex.Code, detail = ex.Detail });
        }

        activity.Write("client", client.Id, "client.archived",
            clientId: client.Id,
            data: new { previousStatus = "ACTIVE" });

        await db.SaveChangesAsync();
        return Results.Ok(new { client.Id, client.Status, client.ArchivedAt });
    }

    private static async Task<IResult> CreatePortalLink(
        Guid id,
        AppDbContext db,
        ActivityWriter activity,
        IClock clock,
        IOptions<AppSettings> settings)
    {
        var client = await db.Clients.FindAsync(id);
        if (client == null) return Results.NotFound();

        var regenerated = client.PortalTokenHash != null;
        string rawToken;
        try
        {
            rawToken = client.IssuePortalLink(clock);
        }
        catch (DomainException ex)
        {
            return Results.Conflict(new { code = ex.Code, detail = ex.Detail });
        }

        // The raw token goes only into this response — never into logs or activity data (07-security §4).
        var portalUrl = $"{settings.Value.PublicBaseUrl.TrimEnd('/')}/p/{rawToken}";

        activity.Write("client", client.Id,
            regenerated ? "client.portal_link_regenerated" : "client.portal_link_created",
            clientId: client.Id,
            data: new { version = client.PortalTokenVersion },
            visibleToClient: false);

        await db.SaveChangesAsync();

        return Results.Ok(new { url = portalUrl, createdAt = client.PortalTokenCreatedAt });
    }

    private static async Task<IResult> EnablePortal(
        Guid id,
        AppDbContext db,
        ActivityWriter activity)
    {
        var client = await db.Clients.FindAsync(id);
        if (client == null) return Results.NotFound();

        try
        {
            client.EnablePortal();
        }
        catch (DomainException ex)
        {
            return Results.Conflict(new { code = ex.Code, detail = ex.Detail });
        }

        activity.Write("client", client.Id, "client.portal_enabled", clientId: client.Id);
        await db.SaveChangesAsync();
        return Results.Ok(new { client.Id, client.PortalEnabled });
    }

    private static async Task<IResult> DisablePortal(
        Guid id,
        AppDbContext db,
        ActivityWriter activity)
    {
        var client = await db.Clients.FindAsync(id);
        if (client == null) return Results.NotFound();

        client.DisablePortal();
        activity.Write("client", client.Id, "client.portal_disabled", clientId: client.Id);
        await db.SaveChangesAsync();
        return Results.Ok(new { client.Id, client.PortalEnabled });
    }

    private static async Task<IResult> RevokePortalLink(
        Guid id,
        AppDbContext db,
        ActivityWriter activity)
    {
        var client = await db.Clients.FindAsync(id);
        if (client == null) return Results.NotFound();

        try
        {
            client.RevokePortalLink();
        }
        catch (DomainException ex)
        {
            return Results.Conflict(new { code = ex.Code, detail = ex.Detail });
        }

        activity.Write("client", client.Id, "client.portal_link_revoked",
            clientId: client.Id,
            data: new { version = client.PortalTokenVersion },
            visibleToClient: false);

        await db.SaveChangesAsync();
        return Results.Ok(new { client.Id, client.PortalEnabled, hasPortalLink = false });
    }

    private static async Task<IResult> GetActivity(
        Guid id,
        AppDbContext db,
        int page = 1,
        int pageSize = 20)
    {
        pageSize = Math.Min(pageSize, 100);
        var exists = await db.Clients.AnyAsync(c => c.Id == id);
        if (!exists) return Results.NotFound();

        var total = await db.ActivityLogs.CountAsync(a => a.ClientId == id);
        var items = await db.ActivityLogs
            .Where(a => a.ClientId == id)
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.OccurredAt,
                a.ActorType,
                a.EntityType,
                a.EntityId,
                a.Action,
                a.Data,
                a.VisibleToClient,
                a.ClientMessage,
            })
            .ToListAsync();

        return Results.Ok(new { items, page, pageSize, total });
    }

    private static object ToDetailDto(Client c, object[] projects, int projectCount,
        long acceptedTotal, long outstanding, long pendingQuotationTotal, int newRequests)
        => new
        {
            c.Id, c.Name, c.Company, c.Phone, c.Email, c.ContactChannel, c.Notes,
            c.Status, c.PortalEnabled, c.PortalTokenVersion, c.PortalTokenCreatedAt,
            HasPortalLink = c.PortalTokenHash != null,
            c.ArchivedAt, c.CreatedAt, c.UpdatedAt,
            ProjectCount = projectCount, AcceptedTotal = acceptedTotal,
            Outstanding = outstanding, PendingQuotationTotal = pendingQuotationTotal,
            NewRequests = newRequests,
        };
}

record CreateClientRequest(
    string Name,
    string? Company,
    string? Phone,
    string? Email,
    string? ContactChannel,
    string? Notes);

record UpdateClientRequest(
    string Name,
    string? Company,
    string? Phone,
    string? Email,
    string? ContactChannel,
    string? Notes);
