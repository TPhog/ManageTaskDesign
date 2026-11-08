using FreelanceFlow.Api.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreelanceFlow.Api.Features.Revisions;

/// <summary>docs/05 §1.2 status per domain code; body { code, detail } like the quotation endpoints.</summary>
internal static class RevisionResults
{
    public static IResult FromDomain(DomainException ex)
    {
        var status = ex.Code switch
        {
            "VALIDATION_FAILED" => StatusCodes.Status400BadRequest,
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "INVALID_TRANSITION" or "BILLING_LOCKED" or "CONCURRENCY_CONFLICT" or "DUPLICATE" => StatusCodes.Status409Conflict,
            "SCOPE_GUARD" => StatusCodes.Status428PreconditionRequired,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
        return Results.Json(new { code = ex.Code, detail = ex.Detail }, statusCode: status);
    }

    public static IResult Concurrency() =>
        Results.Conflict(new { code = "CONCURRENCY_CONFLICT", detail = "Dữ liệu đã thay đổi, vui lòng tải lại" });

    public static IResult BusinessRule(string detail) =>
        Results.Json(new { code = "BUSINESS_RULE", detail }, statusCode: StatusCodes.Status422UnprocessableEntity);

    /// <summary>ux_rounds_deliverable_no / ux_rounds_project_no: a concurrent command took the same round_no.</summary>
    public static bool IsUniqueViolation(DbUpdateException e) =>
        e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
