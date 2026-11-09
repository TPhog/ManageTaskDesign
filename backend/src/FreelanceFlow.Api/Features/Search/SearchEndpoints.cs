using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Search;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGroup("/api/admin").RequireAuthorization().MapGet("search", Search);

    /// <summary>
    /// docs/08 §2.1 header quick search: clients (name / company / phone), projects (name), quotations (number / title),
    /// tasks (title) and client requests (content, newest first) — P14 DF-26.
    /// Each group matches only its own columns (a client's name never returns its projects). Case- and accent-insensitive
    /// via ILIKE + unaccent (migration AddUnaccentExtension); prefix matches rank first; at most 5 rows per group.
    /// </summary>
    private static async Task<IResult> Search(AppDbContext db, IClock clock, string? q = null)
    {
        var term = SearchQuery.Normalize(q);
        if (term.Length > SearchQuery.MaxLength)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["q"] = [$"Từ khóa tối đa {SearchQuery.MaxLength} ký tự"],
            });
        if (term.Length < SearchQuery.MinLength) return Results.Ok(SearchResultDto.Empty);

        var contains = SearchQuery.ContainsPattern(term);
        var prefix = SearchQuery.PrefixPattern(term);
        var byNumber = SearchQuery.HasNumberChars(term);
        var number = SearchQuery.NumberContainsPattern(term);
        var byPhone = SearchQuery.HasPhoneChars(term);
        var phone = SearchQuery.PhoneContainsPattern(term);
        var exactNumber = SearchQuery.NumberExactPattern(term);
        const string esc = SearchQuery.EscapeChar;
        const int limit = SearchQuery.GroupLimit;
        var today = clock.TodayVn;

        var clients = await db.Clients.AsNoTracking()
            .Where(c => EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(contains), esc)
                || (c.Company != null && EF.Functions.ILike(EF.Functions.Unaccent(c.Company), EF.Functions.Unaccent(contains), esc))
                || (byPhone && c.Phone != null
                    && EF.Functions.ILike(c.Phone.Replace(" ", "").Replace(".", "").Replace("-", ""), phone, esc)))
            .OrderBy(c => EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(prefix), esc) ? 0 : 1)
            .ThenBy(c => c.Name)
            .Take(limit)
            .Select(c => new SearchClientDto(c.Id, c.Name, c.Company, c.Phone, c.Status))
            .ToListAsync();

        var projects = await (
                from p in db.Projects.AsNoTracking()
                join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
                where EF.Functions.ILike(EF.Functions.Unaccent(p.Name), EF.Functions.Unaccent(contains), esc)
                select new { Project = p, ClientName = c.Name })
            .OrderBy(x => EF.Functions.ILike(EF.Functions.Unaccent(x.Project.Name), EF.Functions.Unaccent(prefix), esc) ? 0 : 1)
            .ThenBy(x => x.Project.Name)
            .Take(limit)
            .Select(x => new SearchProjectDto(x.Project.Id, x.Project.Name, x.ClientName, x.Project.Status, x.Project.DueDate))
            .ToListAsync();

        // SUPERSEDED versions are history (BR-34): the chain is found through its live version or its next DRAFT
        var quotationRows = await (
                from x in db.Quotations.AsNoTracking()
                join p in db.Projects.AsNoTracking() on x.ProjectId equals p.Id
                join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
                where x.Status != QuotationStatuses.Superseded
                      && ((byNumber && x.Number != null && EF.Functions.ILike(x.Number.Replace("-", ""), number, esc))
                          || EF.Functions.ILike(EF.Functions.Unaccent(x.Title), EF.Functions.Unaccent(contains), esc))
                select new { Quotation = x, ProjectName = p.Name, ClientName = c.Name })
            .OrderBy(r => r.Quotation.Number != null && EF.Functions.ILike(r.Quotation.Number.Replace("-", ""), exactNumber, esc) ? 0 : 1)
            .ThenByDescending(r => r.Quotation.CreatedAt)
            .Take(limit)
            .ToListAsync();
        var quotations = quotationRows
            .Select(r => new SearchQuotationDto(
                r.Quotation.Id, r.Quotation.ProjectId, r.Quotation.Number, r.Quotation.Version, r.Quotation.Title,
                r.ProjectName, r.ClientName, r.Quotation.EffectiveStatus(today), r.Quotation.Total))
            .ToList();

        var tasks = await (
                from t in db.Tasks.AsNoTracking()
                join p in db.Projects.AsNoTracking() on t.ProjectId equals p.Id
                where EF.Functions.ILike(EF.Functions.Unaccent(t.Title), EF.Functions.Unaccent(contains), esc)
                select new { Task = t, ProjectName = p.Name })
            .OrderBy(x => EF.Functions.ILike(EF.Functions.Unaccent(x.Task.Title), EF.Functions.Unaccent(prefix), esc) ? 0 : 1)
            .ThenBy(x => x.Task.Title)
            .Take(limit)
            .Select(x => new SearchTaskDto(x.Task.Id, x.Task.ProjectId, x.Task.Title, x.ProjectName, x.Task.Status))
            .ToListAsync();

        var requestRows = await (
                from r in db.ClientRequests.AsNoTracking()
                join p in db.Projects.AsNoTracking() on r.ProjectId equals p.Id
                join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
                where EF.Functions.ILike(EF.Functions.Unaccent(r.Content), EF.Functions.Unaccent(contains), esc)
                orderby r.CreatedAt descending
                select new { r.Id, r.ProjectId, ProjectName = p.Name, ClientName = c.Name, r.Content, r.Status })
            .Take(limit)
            .ToListAsync();
        var requests = requestRows
            .Select(r => new SearchRequestDto(r.Id, r.ProjectId, r.ProjectName, r.ClientName,
                r.Content.Length > SearchRequestDto.ExcerptLength ? r.Content[..SearchRequestDto.ExcerptLength] : r.Content,
                r.Status))
            .ToList();

        return Results.Ok(new SearchResultDto(clients, projects, quotations, tasks, requests));
    }
}
