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
/// Stages uploaded images: quota check (STORAGE_FULL at ≥ 90 %), size and magic-byte checks, WebP variants (client photos: original + thumb only),
/// objects written, ProjectFile rows added to the DbContext (not saved). The caller writes the activity and commits
/// with one SaveChangesAsync; on any failure it calls DiscardAsync so no orphan object stays behind.
/// </summary>
public sealed class ImageUploader(
    AppDbContext db, IFileStorage storage, StorageAlertService storageAlerts, IOptions<AppSettings> app, IClock clock,
    ImageProcessingGate gate, ILogger<ImageUploader> logger)
{
    private const string WebpContentType = "image/webp";
    private readonly List<string> _written = [];
    private readonly Dictionary<Guid, ImageDuplicateDto> _duplicates = [];

    /// <summary>P10: the identical image already in the project (designer uploads only), after StageAsync.</summary>
    public ImageDuplicateDto? DuplicateOf(Guid imageId) => _duplicates.GetValueOrDefault(imageId);

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
            var hash = SHA256.HashData(bytes);
            var separatePreview = target.UploadedBy == "DESIGNER";   // P10: a client photo is stored once
            var processed = await gate.RunAsync(() => ImageProcessor.Process(bytes, watermark, separatePreview), ct);
            var image = ProjectFile.CreateImage(target.ProjectId, target.OwnerType, target.OwnerId, target.RequestId,
                target.UploadedBy, target.VisibleToClient, file.FileName, processed.Width, processed.Height,
                processed.Original.LongLength, processed.Preview?.LongLength ?? 0, processed.Thumb.LongLength,
                hash, clock.UtcNow, previewIsOriginal: processed.Preview is null);
            if (target.UploadedBy == "DESIGNER"
                && await FindDuplicateAsync(staged, target.ProjectId, hash, ct) is { } duplicate)
                _duplicates[image.Id] = duplicate;
            await PutAsync(image.VariantKey(ImageVariants.Original), processed.Original, ct);
            if (processed.Preview is not null) await PutAsync(image.VariantKey(ImageVariants.Preview), processed.Preview, ct);
            await PutAsync(image.VariantKey(ImageVariants.Thumb), processed.Thumb, ct);
            db.Files.Add(image);
            staged.Add(image);
        }
        return staged;
    }

    /// <summary>An earlier file of this request first (not saved yet), else the earliest live image of the project with the same hash.</summary>
    private async Task<ImageDuplicateDto?> FindDuplicateAsync(
        IReadOnlyList<ProjectFile> staged, Guid projectId, byte[] hash, CancellationToken ct)
    {
        var same = staged.FirstOrDefault(s => s.Sha256.AsSpan().SequenceEqual(hash));
        if (same is not null)
            return new ImageDuplicateDto(same.Id, same.OwnerType!, same.OwnerId!.Value, null, same.CreatedAt, SameUpload: true);
        return await (from f in db.Files.AsNoTracking()
                      where f.ProjectId == projectId && f.OwnerType != null && f.DeletedAt == null && f.Sha256 == hash
                      orderby f.CreatedAt, f.Id
                      select new ImageDuplicateDto(f.Id, f.OwnerType!, f.OwnerId!.Value,
                          f.OwnerType == ImageOwnerTypes.Task
                              ? db.Tasks.Where(t => t.Id == f.OwnerId).Select(t => t.Title).FirstOrDefault()
                              : null,
                          f.CreatedAt, false))
            .FirstOrDefaultAsync(ct);
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

    /// <summary>One buffer of exactly the upload size (≤ App:MaxUploadMb, checked above) — no intermediate copy.</summary>
    private static async Task<byte[]> ReadAllAsync(IFormFile file, CancellationToken ct)
    {
        var bytes = new byte[file.Length];
        await using var input = file.OpenReadStream();
        await input.ReadExactlyAsync(bytes, ct);
        return bytes;
    }
}
