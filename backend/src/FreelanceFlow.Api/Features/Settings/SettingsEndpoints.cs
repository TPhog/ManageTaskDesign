using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Infrastructure.VietQr;
using Microsoft.EntityFrameworkCore;

namespace FreelanceFlow.Api.Features.Settings;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/admin/banks", GetBanks)
           .RequireAuthorization("DesignerOnly");

        app.MapGet("/api/admin/settings", GetSettings)
           .RequireAuthorization("DesignerOnly");

        app.MapPut("/api/admin/settings", UpdateSettings)
           .RequireAuthorization("DesignerOnly");

        app.MapGet("/api/admin/settings/test-qr.png", GetTestQr)
           .RequireAuthorization("DesignerOnly");
    }

    private static async Task<IResult> GetBanks(AppDbContext db)
    {
        var banks = await db.Banks
            .Where(b => b.Active)
            .OrderBy(b => b.ShortName)
            .AsNoTracking()
            .Select(b => new BankDto(b.Bin, b.Code, b.ShortName, b.Name))
            .ToListAsync();

        return Results.Ok(banks);
    }

    private static async Task<IResult> GetSettings(AppDbContext db)
    {
        // Use tracking so xmin shadow property is accessible via Entry
        var s = await db.Settings.FirstOrDefaultAsync();
        if (s is null) return Results.NotFound();

        var xmin = db.Entry(s).Property<uint>("xmin").CurrentValue;
        return Results.Ok(new SettingsDto(
            s.DisplayName, s.ContactEmail, s.ContactPhone, s.LogoFileId,
            s.BankBin, s.BankAccountNo, s.BankAccountName,
            s.QuotationPrefix, s.DefaultValidityDays, s.DefaultDepositPercent,
            s.DefaultTerms, s.UpdatedAt, (long)xmin, s.PortalResponseTime));
    }

    private static async Task<IResult> UpdateSettings(
        UpdateSettingsRequest body, AppDbContext db, IClock clock)
    {
        // Validate
        if (body.QuotationPrefix is not null &&
            !System.Text.RegularExpressions.Regex.IsMatch(body.QuotationPrefix, @"^[A-Z]{1,5}$"))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["quotationPrefix"] = ["Must be 1–5 uppercase letters"] });

        if (body.DefaultValidityDays is < 1 or > 365)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["defaultValidityDays"] = ["Must be between 1 and 365"] });

        if (body.DefaultDepositPercent is < 0 or > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["defaultDepositPercent"] = ["Must be between 0 and 100"] });

        if (body.BankAccountNo is not null &&
            !System.Text.RegularExpressions.Regex.IsMatch(body.BankAccountNo, @"^[0-9A-Za-z]{4,19}$"))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["bankAccountNo"] = ["Must be 4–19 alphanumeric characters"] });

        var settings = await db.Settings.FirstOrDefaultAsync();
        if (settings is null) return Results.NotFound();

        // Set OriginalValue for xmin so EF uses the client's rowVersion for concurrency check
        db.Entry(settings).Property<uint>("xmin").OriginalValue = (uint)body.RowVersion;

        settings.DisplayName = body.DisplayName;
        settings.ContactEmail = body.ContactEmail;
        settings.ContactPhone = body.ContactPhone;
        settings.LogoFileId = body.LogoFileId;
        settings.BankBin = body.BankBin;
        settings.BankAccountNo = body.BankAccountNo;
        settings.BankAccountName = body.BankAccountName;
        if (body.QuotationPrefix is not null) settings.QuotationPrefix = body.QuotationPrefix;
        if (body.DefaultValidityDays.HasValue) settings.DefaultValidityDays = body.DefaultValidityDays.Value;
        if (body.DefaultDepositPercent.HasValue) settings.DefaultDepositPercent = body.DefaultDepositPercent.Value;
        if (body.DefaultTerms is not null) settings.DefaultTerms = body.DefaultTerms;
        try { settings.SetPortalResponseTime(body.PortalResponseTime); }
        catch (DomainException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["portalResponseTime"] = [ex.Detail ?? ex.Code] });
        }
        settings.UpdatedAt = clock.UtcNow;

        try
        {
            await db.SaveChangesAsync();
            return Results.NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { code = "CONCURRENCY_CONFLICT" });
        }
    }

    private static async Task<IResult> GetTestQr(AppDbContext db)
    {
        var s = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
        if (s?.BankBin is null || s.BankAccountNo is null)
            return Results.Problem("Chưa cấu hình tài khoản ngân hàng", statusCode: 422);

        var payload = VietQrPayloadBuilder.Build(new VietQrRequest(
            Bin: s.BankBin,
            AccountNo: s.BankAccountNo,
            Amount: 10_000,
            Memo: "TEST QR"
        ));
        var png = VietQrPngBuilder.BuildPng(payload);
        return Results.File(png, "image/png", "test-qr.png");
    }

    private record BankDto(string Bin, string Code, string ShortName, string Name);

    private record SettingsDto(
        string DisplayName, string? ContactEmail, string? ContactPhone, Guid? LogoFileId,
        string? BankBin, string? BankAccountNo, string? BankAccountName,
        string QuotationPrefix, int DefaultValidityDays, int DefaultDepositPercent,
        string DefaultTerms, DateTimeOffset UpdatedAt, long RowVersion, string PortalResponseTime);

    private record UpdateSettingsRequest(
        string DisplayName, string? ContactEmail, string? ContactPhone, Guid? LogoFileId,
        string? BankBin, string? BankAccountNo, string? BankAccountName,
        string? QuotationPrefix, int? DefaultValidityDays, int? DefaultDepositPercent,
        string? DefaultTerms, long RowVersion, string? PortalResponseTime = null);
}
