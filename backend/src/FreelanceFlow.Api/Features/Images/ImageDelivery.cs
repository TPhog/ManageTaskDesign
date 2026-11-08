using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.Net.Http.Headers;

namespace FreelanceFlow.Api.Features.Images;

/// <summary>
/// Streams one variant after the caller's permission check (spec §5) — there is no static URL to the folder.
/// The ETag names the object actually sent (id + served variant; P10 alias/fallback) and is the only validator:
/// no Last-Modified is sent, so If-Modified-Since alone never yields a 304 for a URL whose served object changed.
/// Cache policy: portal responses <see cref="ShortCache"/> (hide/delete/logout must take effect on a shared device);
/// admin preview/thumb <see cref="ImmutableCache"/> (those objects never change); the admin original
/// <see cref="ShortCache"/> because it falls back to the preview once cleaned up.
/// </summary>
public static class ImageDelivery
{
    public const string ShortCache = "private, max-age=600";
    public const string ImmutableCache = "private, max-age=31536000, immutable";

    public static async Task<IResult> ServeAsync(
        ProjectFile image, string variant, bool portal, IFileStorage storage, HttpContext http, CancellationToken ct)
    {
        // P10: a client photo stored once serves its original as "preview"; a cleaned-up original falls back to the preview
        var served = image.ServedVariant(variant);
        var etag = new EntityTagHeaderValue($"\"{image.Id:N}-{served}\"");
        var cacheControl = portal || variant == ImageVariants.Original ? ShortCache : ImmutableCache;
        http.Response.Headers.XContentTypeOptions = "nosniff";
        if (http.Request.GetTypedHeaders().IfNoneMatch.Any(t => t.Compare(etag, useStrongComparison: false)))
        {
            http.Response.Headers.CacheControl = cacheControl;
            http.Response.Headers.ETag = etag.ToString();
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        var stream = await storage.OpenReadAsync(image.VariantKey(served), ct);
        if (stream is null) return Results.NotFound();   // not cacheable: the object may be restored
        http.Response.Headers.CacheControl = cacheControl;
        http.Response.Headers.ContentDisposition = "inline";
        return Results.Stream(stream, "image/webp", entityTag: etag);
    }
}
