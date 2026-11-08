using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Features.Images;

/// <summary>docs/05 §1.2 + plan 08: body { code, detail } like the other feature endpoints.</summary>
public static class ImageResults
{
    public static IResult FromDomain(DomainException ex)
    {
        if (ex.Code == "VALIDATION_FAILED")
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["files"] = [ex.Detail ?? "Ảnh không hợp lệ"] });
        var status = ex.Code switch
        {
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "FILE_IN_USE" => StatusCodes.Status409Conflict,
            "FILE_TOO_LARGE" => StatusCodes.Status413PayloadTooLarge,
            "UNSUPPORTED_FILE_TYPE" => StatusCodes.Status415UnsupportedMediaType,
            "STORAGE_FULL" => StatusCodes.Status507InsufficientStorage,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
        return Results.Json(new { code = ex.Code, detail = ex.Detail }, statusCode: status);
    }
}
