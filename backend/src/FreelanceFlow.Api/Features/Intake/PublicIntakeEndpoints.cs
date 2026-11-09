using FreelanceFlow.Api.Common;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Domain.Intake;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.RequestTemplates;
using FreelanceFlow.Api.Features.Images;
using FreelanceFlow.Api.Features.RequestTemplates;
using FreelanceFlow.Api.Features.Storage;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SettingsEntity = FreelanceFlow.Api.Domain.Settings.Settings;

namespace FreelanceFlow.Api.Features.Intake;

/// <summary>
/// Plan 13 public form for NEW clients (07-security §4.5): anonymous on purpose, rate-limited per IP, size-limited,
/// honeypot + minimum fill time, daily cap, no echo. Never reads HttpContext.User — admin/portal cookies are ignored.
/// Bodies, names, phones, e-mails, the slug and the form token are never logged.
/// </summary>
public static class PublicIntakeEndpoints
{
    /// <summary>Per IP per minute, reading the form.</summary>
    public const string RateLimitPolicy = "public-intake";
    /// <summary>Per IP per hour, posting the form (review P13-BE #2) — stricter than the read limit, so it replaces it on POST.</summary>
    public const string SubmitRateLimitPolicy = "public-intake-submit";
    /// <summary>Serialises the studio-wide daily count with the insert (pg_advisory_xact_lock key, plan 13).</summary>
    private const long DailyCapLockKey = 20261113090000;

    public static void MapPublicIntakeEndpoints(this IEndpointRouteBuilder app)
    {
        // the body limit follows the advertised photo size, so raising App:MaxUploadMb never turns valid photos into 413
        var maxBodyBytes = MaxBodyBytes(app.ServiceProvider.GetRequiredService<IOptions<AppSettings>>().Value);
        var pub = app.MapGroup("/api/public/intake").AllowAnonymous();
        pub.MapGet("{slug}", GetForm).RequireRateLimiting(RateLimitPolicy);
        pub.MapPost("{slug}", Submit).RequireRateLimiting(SubmitRateLimitPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(maxBodyBytes));
    }

    private static long MaxBodyBytes(AppSettings app) => ImageLimits.PublicIntakeBodyBytes(app.MaxUploadMb, IntakeSubmission.MaxImages);

    private static IResult DailyCapReached() => Results.Json(
        new { code = "RATE_LIMITED", detail = "Hôm nay studio đã nhận nhiều yêu cầu — vui lòng liên hệ trực tiếp hoặc gửi lại vào ngày mai" },
        statusCode: StatusCodes.Status429TooManyRequests);

    private static IResult PhotosRefused() => Results.Json(
        new { code = "STORAGE_FULL", detail = "Hiện studio chưa nhận ảnh đính kèm — vui lòng gửi yêu cầu không kèm ảnh" },
        statusCode: StatusCodes.Status507InsufficientStorage);

    private static IResult FormExpired() =>
        Results.BadRequest(new { code = "FORM_EXPIRED", detail = "Biểu mẫu đã hết hạn — vui lòng tải lại trang rồi gửi lại" });

    private sealed record StudioRow(string DisplayName, string? ContactPhone, string? ContactEmail);

    /// <summary>An enabled form with exactly this slug; every other case is the same 404.</summary>
    private static async Task<StudioRow?> FindFormAsync(AppDbContext db, string slug, CancellationToken ct) =>
        !SettingsEntity.IsWellFormedIntakeSlug(slug) ? null
            : await db.Settings.AsNoTracking()
                .Where(s => s.IntakeEnabled && s.IntakeSlug == slug)
                .Select(s => new StudioRow(s.DisplayName, s.ContactPhone, s.ContactEmail))
                .FirstOrDefaultAsync(ct);

    private static async Task<IResult> GetForm(
        string slug, HttpContext http, AppDbContext db, IntakeFormToken tokens, StorageAlertService storageAlerts,
        IOptions<AppSettings> app, CancellationToken ct)
    {
        var studio = await FindFormAsync(db, slug, ct);
        if (studio is null) return Results.NotFound();
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers["X-Robots-Tag"] = "noindex";
        var name = string.IsNullOrWhiteSpace(studio.DisplayName) ? "Studio thiết kế" : studio.DisplayName.Trim();
        return Results.Ok(new PublicIntakeFormDto(
            new PublicStudioDto(name, studio.ContactPhone, studio.ContactEmail),
            await RequestTemplateDtos.ListPublicAsync(db, ct),
            await storageAlerts.IntakePhotosBlockedAsync(ct) ? 0 : IntakeSubmission.MaxImages,   // 0 = text only for now
            app.Value.MaxUploadMb, tokens.Issue(slug)));
    }

    private static async Task<IResult> Submit(
        string slug, HttpRequest http, AppDbContext db, ActivityWriter activity, ImageUploader uploader,
        StorageAlertService storageAlerts, IntakeFormToken tokens, IntakeIpDailyCounter perIp, IOptions<IntakeOptions> options,
        IClock clock, INotifier notifier, IOptions<AppSettings> appSettings, ILogger<PublicIntakeEndpointsLog> logger)
    {
        var ct = http.HttpContext.RequestAborted;
        if (await FindFormAsync(db, slug, ct) is null) return Results.NotFound();

        // cheap refusals before the (up to 3 photos + 1 MB) body is read
        var today = clock.TodayVn;
        var dayStart = ClientRequest.DayStartUtc(today);   // BR-137: studio-wide, then per IP
        if (await db.IntakeSubmissions.CountAsync(s => s.CreatedAt >= dayStart, ct) >= IntakeSubmission.DailyLimit)
            return DailyCapReached();
        var ip = http.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (perIp.Count(ip, today) >= options.Value.MaxSubmissionsPerIpPerDay)
            return Results.Json(
                new { code = "RATE_LIMITED", detail = "Bạn đã gửi nhiều yêu cầu hôm nay — vui lòng liên hệ trực tiếp với studio hoặc gửi lại vào ngày mai" },
                statusCode: StatusCodes.Status429TooManyRequests);
        // photos stop at 80 % for anonymous senders (the designer's own block stays at 90 %); text is always accepted
        var mayCarryPhotos = http.HasFormContentType && http.ContentLength is not (<= ImageForms.TextOnlyBodyBytes);
        if (mayCarryPhotos && await storageAlerts.IntakePhotosBlockedAsync(ct)) return PhotosRefused();
        var (form, readError) = await ImageForms.ReadAsync(http, MaxBodyBytes(appSettings.Value), ct);
        if (readError is not null) return readError;
        if (form!.Files.Count > 0 && await storageAlerts.IntakePhotosBlockedAsync(ct)) return PhotosRefused();

        // BR-138: bots get the same empty 204 as people, and nothing is stored
        if (form!["website"].ToString().Length > 0)
        {
            logger.LogInformation("Public intake dropped (honeypot)");
            return Results.NoContent();
        }
        var ticket = tokens.Read(form["formToken"].ToString(), slug);
        if (ticket is null) return FormExpired();   // malformed, other link, older than 24 h or already used
        if (ticket.AgeSeconds < options.Value.MinFillSeconds)
        {
            tokens.TryConsume(ticket.Id);
            logger.LogInformation("Public intake dropped (filled too fast)");
            return Results.NoContent();
        }

        var contactName = form["contactName"].ToString();
        var contactPhone = form["contactPhone"].ToString();
        var contactEmail = form["contactEmail"].ToString();
        var note = form["note"].ToString();
        var errors = IntakeSubmission.ValidateContact(contactName, contactPhone, contactEmail)
            .ToDictionary(e => e.Key, e => new[] { e.Value });
        RequestTemplate? template = null;
        IReadOnlyList<TemplateAnswer> answers = [];
        var rawTemplate = form["templateId"].ToString();
        if (rawTemplate.Length > 0)
        {
            if (Guid.TryParse(rawTemplate, out var templateId))
                template = await db.RequestTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId && t.Active, ct);
            if (template is null)
                errors["templateId"] = ["Mẫu yêu cầu không còn dùng được — vui lòng tải lại trang"];
            else
            {
                var result = TemplateAnswers.Evaluate(template.Fields(), form["answers"].ToString(), form.Files.Count, clock.TodayVn);
                foreach (var (key, message) in result.Errors)
                    errors[key == TemplateAnswers.FormErrorKey ? "answers" : $"answers.{key}"] = [message];
                answers = result.Answers;
                if (result.IsValid && answers.Count == 0 && note.Trim().Length == 0)
                    errors["answers"] = [TemplateAnswers.NothingAnswered];
            }
        }
        else if (note.Trim().Length == 0)
            errors["note"] = ["Mô tả yêu cầu của bạn"];
        if (note.Trim().Length > TemplateLimits.MaxNote) errors["note"] = [$"Tối đa {TemplateLimits.MaxNote} ký tự"];
        foreach (var (field, messages) in ImageLimits.ValidateCount(form.Files.Count, IntakeSubmission.MaxImages, required: false))
            errors[field] = messages;
        if (errors.Count == 0 && TemplateRenderer.Render(template?.Name, answers, note).Length > IntakeSubmission.MaxBody)
            errors["answers"] = ["Nội dung quá dài — vui lòng rút gọn câu trả lời"];
        if (errors.Count > 0) return Results.ValidationProblem(errors);   // the token stays valid for the corrected resend
        if (!tokens.TryConsume(ticket.Id)) return FormExpired();   // a concurrent post of the same token won

        var submission = IntakeSubmission.Create(contactName, contactPhone, contactEmail, template?.Id, template?.Name, answers, note);
        db.IntakeSubmissions.Add(submission);
        activity.Write("intake_submission", submission.Id, "intake.submitted", actorType: "CLIENT",
            data: new { templateId = submission.TemplateId, imageCount = form.Files.Count });   // no personal data
        IReadOnlyList<ProjectFile> images = [];
        try
        {
            if (form.Files.Count > 0)
            {
                // the submission and its photos commit together; a refused photo stores nothing (BR-135)
                images = await uploader.StageAsync(form.Files,
                    new ImageTarget(null, ImageOwnerTypes.IntakeSubmission, submission.Id, null, "CLIENT", VisibleToClient: false), ct);
                activity.Write("intake_submission", submission.Id, "image.uploaded", actorType: "CLIENT",
                    data: new { count = images.Count, bytes = images.Sum(i => i.SizeBytes), ownerType = ImageOwnerTypes.IntakeSubmission });
            }
            // BR-137: concurrent posts re-count under one advisory lock, so the studio cap cannot be overshot
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({DailyCapLockKey})", ct);
            if (await db.IntakeSubmissions.CountAsync(s => s.CreatedAt >= dayStart, ct) >= IntakeSubmission.DailyLimit)
            {
                await uploader.DiscardAsync();
                return DailyCapReached();
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DomainException ex)
        {
            await uploader.DiscardAsync();
            return ImageResults.FromDomain(ex);
        }
        catch
        {
            await uploader.DiscardAsync();
            throw;
        }
        perIp.Record(ip, today);
        if (images.Count > 0) await storageAlerts.EvaluateSafelyAsync(CancellationToken.None);

        await NotifyDesignerAsync(notifier, appSettings.Value, logger, submission);
        return Results.NoContent();
    }

    /// <summary>
    /// Discord: client name + template name ONLY (decision 2b). The anonymous free text (answers, note) never goes to the
    /// third party — it may hold phone numbers, e-mails or markdown links. After commit; never fails the request.
    /// </summary>
    private static async Task NotifyDesignerAsync(INotifier notifier, AppSettings app, ILogger logger, IntakeSubmission s)
    {
        try
        {
            var fields = new List<NotificationField>
            {
                new("Khách", s.ContactName),
                new("Mẫu", s.TemplateName ?? "Yêu cầu khác"),
            };
            var url = Uri.TryCreate(app.PublicBaseUrl, UriKind.Absolute, out var baseUri)
                ? $"{baseUri.AbsoluteUri.TrimEnd('/')}/admin/intake/{s.Id}"
                : null;
            await notifier.EnqueueAsync(new Notification("Khách mới gửi yêu cầu", "Xem chi tiết trong hộp Khách mới.", url,
                NotificationSeverity.Info, fields));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not enqueue the intake notification ({ExceptionType})", ex.GetType().Name);
        }
    }
}

/// <summary>Logger category (static classes cannot be type arguments).</summary>
internal sealed class PublicIntakeEndpointsLog;
