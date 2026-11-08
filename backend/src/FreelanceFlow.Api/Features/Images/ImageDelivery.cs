using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.Net.Http.Headers;

namespace FreelanceFlow.Api.Features.Images;

/// <summary>
/// Streams one variant after the caller's permission check (spec §5) — there is no static URL to the folder.
/// Variants never change once written, so the ETag is id + variant and a matching If-None-Match is a 304
/// answered without touching the disk.
/// </summary>
public static class ImageDelivery
{
    public static async Task<IResult> ServeAsync(
        ProjectFile image, string variant, IFileStorage storage, HttpContext http, CancellationToken ct)
    {
        var etag = new EntityTagHeaderValue($"\"{image.Id:N}-{variant}\"");
        const string cacheControl = "private, max-age=600";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        if (http.Request.GetTypedHeaders().IfNoneMatch.Any(t => t.Compare(etag, useStrongComparison: false)))
        {
            http.Response.Headers.CacheControl = cacheControl;
            http.Response.Headers.ETag = etag.ToString();
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        var stream = await storage.OpenReadAsync(image.VariantKey(variant), ct);
        if (stream is null) return Results.NotFound();   // not cacheable: the object may be restored
        http.Response.Headers.CacheControl = cacheControl;
        http.Response.Headers.ContentDisposition = "inline";
        return Results.Stream(stream, "image/webp", lastModified: image.CreatedAt, entityTag: etag);
    }
}
