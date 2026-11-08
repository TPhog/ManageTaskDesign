using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Infrastructure;
using FreelanceFlow.Api.Infrastructure.VietQr;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreelanceFlow.Api.Features.Quotations;

public static class QuotationWorkflowEndpoints
{
    // "deposit.png" must not match the JSON route, hence the anchored regex on both
    private const string Purpose = "{purpose:regex(^(deposit|full|remaining)$)}";

    public static void MapQuotationWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/quotations").RequireAuthorization();

        group.MapPost("{id:guid}/send", Send);
        group.MapPost("{id:guid}/new-version", NewVersion);
        group.MapPost("{id:guid}/accept", Accept);
        group.MapPost("{id:guid}/reject", Reject);
        group.MapPost("{id:guid}/payment-status", ChangePaymentStatus);
        group.MapGet("{id:guid}/qr/" + Purpose, GetQr);
        group.MapGet("{id:guid}/qr/" + Purpose + ".png", GetQrPng);
    }

    private static async Task<IResult> Send(
        Guid id, RowVersionRequest body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var q = await db.Quotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();
        try
        {
            q.EnsureSendable();
        }
        catch (DomainException ex)
        {
            return QuotationReads.FromDomain(ex);
        }

        // docs/06 §5: no receiving account → no send
        var settings = await db.Settings.AsNoTracking().Include(s => s.Bank).FirstAsync();
        if (settings.BankBin is null || settings.BankAccountNo is null || settings.Bank is null)
            return QuotationReads.BusinessRule("Chưa cấu hình tài khoản nhận tiền");

        var project = await db.Projects.AsNoTracking().FirstAsync(p => p.Id == q.ProjectId);
        var client = await db.Clients.AsNoTracking().FirstAsync(c => c.Id == project.ClientId);
        var designer = new SnapshotDesigner(settings.DisplayName, settings.ContactEmail, settings.ContactPhone);
        var clientSnapshot = new SnapshotClient(client.Name, client.Company);
        var projectSnapshot = new SnapshotProject(project.Id, project.Name);
        var bank = new SnapshotBank(settings.BankBin, settings.Bank.ShortName, settings.BankAccountNo, settings.BankAccountName ?? "");

        db.Entry(q).Property<uint>("xmin").OriginalValue = body.RowVersion;
        var today = clock.TodayVn;

        // One transaction: number draw (row-locked), supersede, send — all or nothing (BR-32, BR-34)
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            string? number = null;
            if (q.Number is null)
            {
                number = Quotation.FormatNumber(settings.QuotationPrefix, today.Year, await NextSequenceAsync(db, today.Year));
            }
            else
            {
                var live = await db.Quotations
                    .Where(x => x.Number == q.Number && x.Id != q.Id
                                && (x.Status == QuotationStatuses.Sent || x.Status == QuotationStatuses.Accepted))
                    .ToListAsync();
                if (live.Any(x => x.Status == QuotationStatuses.Accepted))
                    return QuotationReads.FromDomain(new DomainException("INVALID_TRANSITION",
                        "Đã có version được chấp nhận — không gửi version mới được (BR-34)"));
                foreach (var old in live)
                {
                    old.MarkSuperseded();
                    activity.Write("quotation", old.Id, "quotation.superseded",
                        clientId: project.ClientId, projectId: project.Id,
                        data: new { number = old.Number, version = old.Version, supersededByVersion = q.Version });
                }
                // frees the ux_quotations_one_live slot (BR-35) before this version becomes SENT
                await db.SaveChangesAsync();
            }

            q.Send(today, clock.UtcNow, number, x => QuotationSnapshot.Build(x, designer, clientSnapshot, projectSnapshot, bank));
            q.UpdatedAt = clock.UtcNow;
            activity.Write("quotation", q.Id, "quotation.sent",
                clientId: project.ClientId, projectId: project.Id,
                data: new { number = q.Number, version = q.Version, total = q.Total },
                visibleToClient: true,
                clientMessage: $"Báo giá {q.Number} (v{q.Version}) đã được gửi");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DomainException ex)
        {
            return QuotationReads.FromDomain(ex);
        }
        catch (DbUpdateConcurrencyException)
        {
            return QuotationReads.Concurrency();
        }
        catch (DbUpdateException e) when (QuotationReads.IsLockViolation(e))
        {
            return QuotationReads.Locked();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // ux_quotations_one_live: a concurrent send of the same chain won the slot
            return QuotationReads.Concurrency();
        }

        return Results.Ok(await QuotationReads.LoadDetailAsync(db, id, today));
    }

    /// <summary>docs/04 §4.3: the UPDATE takes the row lock for the rest of the transaction (same guarantee as SELECT … FOR UPDATE).</summary>
    private static async Task<int> NextSequenceAsync(AppDbContext db, int year)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO quotation_sequences (year, last_seq) VALUES ({year}, 0) ON CONFLICT (year) DO NOTHING");
        var next = await db.Database
            .SqlQuery<int>($"UPDATE quotation_sequences SET last_seq = last_seq + 1 WHERE year = {year} RETURNING last_seq AS \"Value\"")
            .ToListAsync();
        return next.Single();
    }

    private static async Task<IResult> NewVersion(Guid id, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var q = await db.Quotations.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();

        Quotation next;
        try
        {
            next = q.CreateNextVersion(q.Version + 1);   // DRAFT / ACCEPTED / SUPERSEDED → INVALID_TRANSITION
        }
        catch (DomainException ex)
        {
            return QuotationReads.FromDomain(ex);
        }

        var chain = await db.Quotations.AsNoTracking()
            .Where(x => x.Number == q.Number)
            .Select(x => new { x.Version, x.Status })
            .ToListAsync();
        if (chain.Any(x => x.Status == QuotationStatuses.Accepted))
            return QuotationReads.FromDomain(new DomainException("INVALID_TRANSITION", "Đã có version được chấp nhận — không tạo version mới (BR-34)"));
        if (chain.Any(x => x.Status == QuotationStatuses.Draft))
            return QuotationReads.FromDomain(new DomainException("DUPLICATE", "Báo giá này đã có một version nháp (BR-33)"));
        if (chain.Max(x => x.Version) != q.Version)
            return QuotationReads.FromDomain(new DomainException("INVALID_TRANSITION", "Chỉ tạo version mới từ bản mới nhất"));

        db.Quotations.Add(next);
        activity.Write("quotation", next.Id, "quotation.version_created",
            clientId: await QuotationReads.ClientIdOfAsync(db, q.ProjectId), projectId: q.ProjectId,
            data: new { number = next.Number, version = next.Version, fromQuotationId = q.Id });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // ux_quotations_one_draft_per_chain: a concurrent request created the draft first
            return QuotationReads.FromDomain(new DomainException("DUPLICATE", "Báo giá này đã có một version nháp (BR-33)"));
        }

        return Results.Ok(await QuotationReads.LoadDetailAsync(db, next.Id, clock.TodayVn));
    }

    private static async Task<IResult> Accept(
        Guid id, AcceptQuotationRequest body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var q = await db.Quotations.FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();
        if (body.EvidenceFileId is { } fileId &&
            !await db.Files.AnyAsync(f => f.Id == fileId && f.ProjectId == q.ProjectId && f.DeletedAt == null))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["evidenceFileId"] = ["File bằng chứng không thuộc dự án này"],
            });

        db.Entry(q).Property<uint>("xmin").OriginalValue = body.RowVersion;
        try
        {
            q.MarkAccepted(body.AcceptedAt ?? clock.TodayVn, body.Note, body.EvidenceFileId);
        }
        catch (DomainException ex)
        {
            return QuotationReads.FromDomain(ex);
        }

        q.UpdatedAt = clock.UtcNow;
        activity.Write("quotation", q.Id, "quotation.accepted",
            clientId: await QuotationReads.ClientIdOfAsync(db, q.ProjectId), projectId: q.ProjectId,
            data: new { number = q.Number, version = q.Version, acceptedAt = q.AcceptedAt, note = q.AcceptedNote, evidenceFileId = q.EvidenceFileId },
            visibleToClient: true,
            clientMessage: $"Báo giá {q.Number} (v{q.Version}) đã được chấp nhận");
        return await SaveAndLoadAsync(db, q.Id, clock);
    }

    private static async Task<IResult> Reject(
        Guid id, RejectQuotationRequest body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var errors = QuotationValidation.ValidateReject(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var q = await db.Quotations.FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();
        db.Entry(q).Property<uint>("xmin").OriginalValue = body.RowVersion;
        try
        {
            q.MarkRejected(body.RejectedAt ?? clock.TodayVn, body.Reason);
        }
        catch (DomainException ex)
        {
            return QuotationReads.FromDomain(ex);
        }

        q.UpdatedAt = clock.UtcNow;
        activity.Write("quotation", q.Id, "quotation.rejected",
            clientId: await QuotationReads.ClientIdOfAsync(db, q.ProjectId), projectId: q.ProjectId,
            data: new { number = q.Number, version = q.Version, rejectedAt = q.RejectedAt, reason = q.RejectedReason },
            visibleToClient: true,
            clientMessage: $"Báo giá {q.Number} (v{q.Version}) đã bị từ chối");
        return await SaveAndLoadAsync(db, q.Id, clock);
    }

    private static async Task<IResult> ChangePaymentStatus(
        Guid id, PaymentStatusRequest body, AppDbContext db, ActivityWriter activity, IClock clock)
    {
        var errors = QuotationValidation.ValidatePayment(body);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var q = await db.Quotations.FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();
        db.Entry(q).Property<uint>("xmin").OriginalValue = body.RowVersion;

        var from = q.PaymentStatus;
        var date = body.Date ?? clock.TodayVn;
        bool isUndo;
        try
        {
            isUndo = q.ChangePaymentStatus(body.To!, date, body.Reason);
        }
        catch (DomainException ex)
        {
            return QuotationReads.FromDomain(ex);
        }

        // BR-95: only forward steps are announced to the client
        var clientMessage = isUndo ? null : body.To switch
        {
            PaymentStatuses.DepositPaid => $"Đã nhận tiền cọc cho {q.Number}",
            PaymentStatuses.Paid => $"Đã nhận đủ thanh toán cho {q.Number}",
            _ => null,
        };
        q.UpdatedAt = clock.UtcNow;
        activity.Write("quotation", q.Id, "quotation.payment_status_changed",
            clientId: await QuotationReads.ClientIdOfAsync(db, q.ProjectId), projectId: q.ProjectId,
            data: new { number = q.Number, from, to = body.To, date, undo = isUndo, reason = body.Reason },
            visibleToClient: clientMessage is not null,
            clientMessage: clientMessage);
        return await SaveAndLoadAsync(db, q.Id, clock);
    }

    private static async Task<IResult> GetQr(Guid id, string purpose, AppDbContext db, IClock clock)
    {
        var q = await db.Quotations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();
        var snapshot = q.Snapshot is null ? null : QuotationSnapshot.FromJson(q.Snapshot);
        var option = QuotationReads.QrOptions(q, snapshot, clock.TodayVn).FirstOrDefault(o => o.Purpose == purpose);
        if (option is null || snapshot is null)
            return QuotationReads.BusinessRule("Không có mã QR này ở trạng thái hiện tại của báo giá (BR-75)");
        return Results.Ok(new QrPayloadDto(option.Purpose, option.Amount, option.Memo, option.Payload, snapshot.Bank));
    }

    private static async Task<IResult> GetQrPng(Guid id, string purpose, AppDbContext db, IClock clock)
    {
        var q = await db.Quotations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return Results.NotFound();
        var snapshot = q.Snapshot is null ? null : QuotationSnapshot.FromJson(q.Snapshot);
        var option = QuotationReads.QrOptions(q, snapshot, clock.TodayVn).FirstOrDefault(o => o.Purpose == purpose);
        if (option is null)
            return QuotationReads.BusinessRule("Không có mã QR này ở trạng thái hiện tại của báo giá (BR-75)");
        var png = VietQrPngBuilder.BuildPng(option.Payload, pixelsPerModule: 10);
        return Results.File(png, "image/png", $"{q.Number!.Replace("-", "")}-{purpose}.png");
    }

    private static async Task<IResult> SaveAndLoadAsync(AppDbContext db, Guid id, IClock clock)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return QuotationReads.Concurrency();
        }
        catch (DbUpdateException e) when (QuotationReads.IsLockViolation(e))
        {
            return QuotationReads.Locked();
        }
        return Results.Ok(await QuotationReads.LoadDetailAsync(db, id, clock.TodayVn));
    }
}
