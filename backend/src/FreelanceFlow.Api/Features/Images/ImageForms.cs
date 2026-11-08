using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Features.Storage;

namespace FreelanceFlow.Api.Features.Images;

/// <summary>Reads a multipart upload; over-limit bodies are 413 and malformed ones 400, never 500. Bodies are never logged.</summary>
public static class ImageForms
{
    /// <summary>A text-only multipart body (no images) never exceeds this; larger bodies carry images.</summary>
    public const long TextOnlyBodyBytes = 64 * 1024;

    /// <summary>
    /// Plan 08: refuses with STORAGE_FULL (507) before the body is read. With <paramref name="textOnlyAllowance"/>
    /// (endpoints whose images are optional) small bodies pass, so a full disk never blocks plain text.
    /// </summary>
    public static async Task<IResult?> EnsureUploadsAllowedAsync(
        StorageAlertService storageAlerts, HttpRequest http, bool imagesOptional, CancellationToken ct)
    {
        if (imagesOptional && !http.HasFormContentType) return null;
        if (imagesOptional && http.ContentLength is { } length && length <= TextOnlyBodyBytes) return null;
        try
        {
            await storageAlerts.EnsureUploadsAllowedAsync(ct);
            return null;
        }
        catch (DomainException ex)
        {
            return ImageResults.FromDomain(ex);
        }
    }

    public static async Task<(IFormCollection? Form, IResult? Error)> ReadAsync(HttpRequest http, long maxBytes, CancellationToken ct)
    {
        if (!http.HasFormContentType)
            return (null, Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["files"] = ["Gửi ảnh dạng multipart/form-data"],
            }));
        if (http.ContentLength > maxBytes) return (null, TooLarge());
        try
        {
            return (await http.ReadFormAsync(ct), null);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return (null, TooLarge());
        }
        catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException or IOException)
        {
            return (null, Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["files"] = ["Dữ liệu gửi lên không hợp lệ"],
            }));
        }
    }

    private static IResult TooLarge() =>
        Results.Json(new { code = "PAYLOAD_TOO_LARGE", detail = "Nội dung gửi lên quá lớn" },
            statusCode: StatusCodes.Status413PayloadTooLarge);
}
