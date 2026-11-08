namespace FreelanceFlow.Api.Features.Images;

/// <summary>Reads a multipart upload; over-limit bodies are 413 and malformed ones 400, never 500. Bodies are never logged.</summary>
public static class ImageForms
{
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
