using System.Security.Cryptography;
using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure.Images;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Features.Images;

public sealed record ImageTarget(Guid ProjectId, string OwnerType, Guid OwnerId, Guid? RequestId, string UploadedBy, bool VisibleToClient);

/// <summary>
/// Stages uploaded images: quota check (STORAGE_FULL at ≥ 90 %), size and magic-byte checks, three WebP variants,
/// objects written, ProjectFile rows added to the DbContext (not saved). The caller writes the activity and commits
/// with one SaveChangesAsync; on any failure it calls DiscardAsync so no orphan object stays behind.
/// </summary>
public sealed class ImageUploader(
    AppDbContext db, IFileStorage storage, StorageAlertService storageAlerts, IOptions<AppSettings> app, IClock clock,
    ILogger<ImageUploader> logger)
{
    private const string WebpContentType = "image/webp";
    private readonly List<string> _written = [];

    public async Task<IReadOnlyList<ProjectFile>> StageAsync(IReadOnlyList<IFormFile> files, ImageTarget target, CancellationToken ct)
    {
        if (files.Count == 0) return [];
        await storageAlerts.EnsureUploadsAllowedAsync(ct);

        var maxBytes = app.Value.MaxUploadMb * 1024L * 1024L;
        var tooLarge = files.FirstOrDefault(f => f.Length > maxBytes);
        if (tooLarge is not null)
            throw new DomainException("FILE_TOO_LARGE", $"Mỗi ảnh tối đa {app.Value.MaxUploadMb} MB");

        WatermarkSpec? watermark = null;
        if (target.UploadedBy == "DESIGNER")
        {
            var settings = await db.Settings.AsNoTracking().FirstAsync(ct);
            if (settings.WatermarkEnabled) watermark = new WatermarkSpec(settings.EffectiveWatermarkText, settings.WatermarkOpacity);
        }

        var staged = new List<ProjectFile>();
        foreach (var file in files)
        {
            var bytes = await ReadAllAsync(file, ct);
            var processed = ImageProcessor.Process(bytes, watermark);
            var image = ProjectFile.CreateImage(target.ProjectId, target.OwnerType, target.OwnerId, target.RequestId,
                target.UploadedBy, target.VisibleToClient, file.FileName, processed.Width, processed.Height,
                processed.Original.LongLength, processed.Preview.LongLength, processed.Thumb.LongLength,
                SHA256.HashData(bytes), clock.UtcNow);
            await PutAsync(image.VariantKey(ImageVariants.Original), processed.Original, ct);
            await PutAsync(image.VariantKey(ImageVariants.Preview), processed.Preview, ct);
            await PutAsync(image.VariantKey(ImageVariants.Thumb), processed.Thumb, ct);
            db.Files.Add(image);
            staged.Add(image);
        }
        return staged;
    }

    /// <summary>Best effort: removes every object written by StageAsync (the commit failed or was refused).</summary>
    public async Task DiscardAsync()
    {
        foreach (var key in _written)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Could not discard a staged image object ({ExceptionType})", ex.GetType().Name);
            }
        }
        _written.Clear();
    }

    private async Task PutAsync(string key, byte[] content, CancellationToken ct)
    {
        _written.Add(key); // before the call: a half-written object is discarded too
        await storage.PutAsync(key, content, WebpContentType, ct);
    }

    private static async Task<byte[]> ReadAllAsync(IFormFile file, CancellationToken ct)
    {
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream((int)Math.Min(file.Length, int.MaxValue));
        await input.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
