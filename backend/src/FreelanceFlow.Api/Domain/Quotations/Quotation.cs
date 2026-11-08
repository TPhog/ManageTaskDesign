using FreelanceFlow.Api.Domain.Common;

namespace FreelanceFlow.Api.Domain.Quotations;

public class Quotation : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProjectId { get; set; }
    public string Kind { get; set; } = "";
    public string? Number { get; set; }
    public int Version { get; set; } = 1;
    public Guid? PreviousVersionId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "DRAFT";
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public int ValidityDays { get; set; }
    public long Subtotal { get; set; }
    public long DiscountAmount { get; set; }
    public int VatPercent { get; set; }
    public long VatAmount { get; set; }
    public long Total { get; set; }
    public int DepositPercent { get; set; }
    public long DepositAmount { get; set; }
    public string Terms { get; set; } = "";
    public string NotesToClient { get; set; } = "";
    public string InternalNotes { get; set; } = "";
    public string? Snapshot { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateOnly? AcceptedAt { get; set; }
    public string? AcceptedNote { get; set; }
    public Guid? EvidenceFileId { get; set; }
    public DateOnly? RejectedAt { get; set; }
    public string? RejectedReason { get; set; }
    public string PaymentStatus { get; set; } = "UNPAID";
    public DateOnly? DepositPaidAt { get; set; }
    public DateOnly? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<QuotationItem> Items { get; set; } = [];

    // NOTE: computed values are methods, never properties — a new property would change the EF model.

    // ---------- Draft (BR-30, §8.3) ----------

    /// <summary>New DRAFT with money already computed. Defaults (validity, deposit %, terms) come from settings.</summary>
    public static Quotation CreateDraft(Guid projectId, string kind, string title, int validityDays,
        int depositPercent, string terms, IEnumerable<QuotationItemInput> items)
    {
        var q = new Quotation
        {
            ProjectId = projectId,
            Kind = kind,
            Title = title,
            ValidityDays = validityDays,
            DepositPercent = depositPercent,
            Terms = terms,
        };
        q.Items = items.Select((input, index) => q.NewItem(input, index)).ToList();
        q.Recalculate();
        return q;
    }

    /// <summary>
    /// BR-30: replaces header + lines of a DRAFT. A line whose Id matches an existing row updates that row in place
    /// (keeps copied_from_item_id and future revision links, BR-47); the caller adds/removes the reported rows.
    /// </summary>
    public ItemChanges EditDraft(QuotationDraftEdit edit)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(edit.Title)) throw Invalid("Vui lòng nhập tiêu đề báo giá");
        if (edit.ValidityDays is < 1 or > 365) throw Invalid("Hiệu lực từ 1 đến 365 ngày");
        if (edit.VatPercent is < 0 or > 20) throw Invalid("VAT từ 0 đến 20%");
        if (edit.DepositPercent is < 0 or > 100) throw Invalid("Cọc từ 0 đến 100%");

        var kept = new List<QuotationItem>();
        for (var index = 0; index < edit.Items.Count; index++)
        {
            var input = edit.Items[index];
            var existing = input.Id is { } itemId ? Items.FirstOrDefault(x => x.Id == itemId) : null;
            if (existing is null || kept.Contains(existing))
            {
                kept.Add(NewItem(input, index));
                continue;
            }
            Apply(existing, input, index);
            kept.Add(existing);
        }
        var removed = Items.Where(x => !kept.Contains(x)).ToList();
        var added = kept.Where(x => !Items.Contains(x)).ToList();

        Title = edit.Title.Trim();
        ValidityDays = edit.ValidityDays;
        DiscountAmount = edit.DiscountAmount;
        VatPercent = edit.VatPercent;
        DepositPercent = edit.DepositPercent;
        Terms = edit.Terms;
        NotesToClient = edit.NotesToClient;
        InternalNotes = edit.InternalNotes;
        Items = kept;
        Recalculate();
        return new ItemChanges(added, removed);
    }

    // ---------- Send & versions (BR-31..35) ----------

    /// <summary>BR-31 preconditions, checked before a number is drawn.</summary>
    public void EnsureSendable()
    {
        if (Status != QuotationStatuses.Draft)
            throw Transition($"{Status} → {QuotationStatuses.Sent}");
        if (Items.Count == 0)
            throw new DomainException("BUSINESS_RULE", "Báo giá cần ít nhất 1 dòng (BR-31)");
    }

    /// <summary>
    /// BR-31: DRAFT → SENT. Version 1 takes <paramref name="newNumber"/> (from quotation_sequences, BR-32);
    /// later versions keep their number and pass null. issue_date = today (VN), expiry = today + validity.
    /// </summary>
    public void Send(DateOnly today, DateTimeOffset now, string? newNumber, Func<Quotation, QuotationSnapshot> buildSnapshot)
    {
        EnsureSendable();
        Number ??= newNumber ?? throw new InvalidOperationException("Version 1 needs a number from quotation_sequences (BR-32)");
        IssueDate = today;
        ExpiryDate = today.AddDays(ValidityDays);
        SentAt = now;
        Snapshot = buildSnapshot(this).ToJson();
        Status = QuotationStatuses.Sent;
    }

    /// <summary>BR-34: the live SENT version is superseded when version N+1 is sent.</summary>
    public void MarkSuperseded()
    {
        if (Status != QuotationStatuses.Sent)
            throw Transition($"{Status} → {QuotationStatuses.Superseded}");
        Status = QuotationStatuses.Superseded;
    }

    /// <summary>BR-33: copy a SENT (incl. EXPIRED) or REJECTED version into a new DRAFT with the same number.</summary>
    public Quotation CreateNextVersion(int nextVersion)
    {
        if (Status is not (QuotationStatuses.Sent or QuotationStatuses.Rejected))
            throw Transition($"Không tạo được version mới từ {Status} (BR-33)");

        var next = new Quotation
        {
            ProjectId = ProjectId,
            Kind = Kind,
            Number = Number,
            Version = nextVersion,
            PreviousVersionId = Id,
            Title = Title,
            ValidityDays = ValidityDays,
            DiscountAmount = DiscountAmount,
            VatPercent = VatPercent,
            DepositPercent = DepositPercent,
            Terms = Terms,
            NotesToClient = NotesToClient,
            InternalNotes = InternalNotes,
        };
        next.Items = Items.OrderBy(i => i.SortOrder).Select(i => new QuotationItem
        {
            QuotationId = next.Id,
            Description = i.Description,
            Details = i.Details,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Amount = i.Amount,
            SourceType = i.SourceType,
            SourceId = i.SourceId,
            CopiedFromItemId = i.Id,   // BR-47
            SortOrder = i.SortOrder,
        }).ToList();
        next.Recalculate();
        return next;
    }

    public static string FormatNumber(string prefix, int year, int sequence) => $"{prefix}-{year}-{sequence:0000}";

    // ---------- Client decision, recorded by the designer (BR-36, BR-37, ADR-004) ----------

    public void MarkAccepted(DateOnly acceptedAt, string? note, Guid? evidenceFileId)
    {
        if (Status != QuotationStatuses.Sent)
            throw Transition($"{Status} → {QuotationStatuses.Accepted}");
        Status = QuotationStatuses.Accepted;
        AcceptedAt = acceptedAt;
        AcceptedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        EvidenceFileId = evidenceFileId;
    }

    public void MarkRejected(DateOnly rejectedAt, string? reason)
    {
        if (Status != QuotationStatuses.Sent)
            throw Transition($"{Status} → {QuotationStatuses.Rejected}");
        if (string.IsNullOrWhiteSpace(reason))
            throw Invalid("Bắt buộc lý do từ chối (BR-37)");
        Status = QuotationStatuses.Rejected;
        RejectedAt = rejectedAt;
        RejectedReason = reason.Trim();
    }

    // ---------- Payment, marked by hand (BR-70..73, ADR-001) ----------

    /// <summary>Returns true when the change is an undo (moves backwards), which requires a reason.</summary>
    public bool ChangePaymentStatus(string to, DateOnly date, string? reason)
    {
        if (Status != QuotationStatuses.Accepted)
            throw new DomainException("BUSINESS_RULE", "Chỉ báo giá đã chấp nhận mới đổi được trạng thái thanh toán (BR-70)");
        var toRank = PaymentRank(to);
        if (to == PaymentStatus)
            throw Transition($"{PaymentStatus} → {to}");
        if (to == PaymentStatuses.DepositPaid && DepositPercent == 0)
            throw new DomainException("BUSINESS_RULE", "Báo giá không có tiền cọc (BR-71)");

        var isUndo = toRank < PaymentRank(PaymentStatus);
        if (isUndo && string.IsNullOrWhiteSpace(reason))
            throw Invalid("Bắt buộc lý do khi hoàn tác thanh toán");

        switch (to)
        {
            case PaymentStatuses.Unpaid:
                DepositPaidAt = null;
                PaidAt = null;
                break;
            case PaymentStatuses.DepositPaid:
                if (isUndo) PaidAt = null;
                else DepositPaidAt = date;
                break;
            case PaymentStatuses.Paid:
                PaidAt = date;
                break;
        }
        PaymentStatus = to;
        return isUndo;
    }

    /// <summary>BR-73, defined for ACCEPTED quotations only (0 otherwise).</summary>
    public long OutstandingAmount() => Status != QuotationStatuses.Accepted ? 0 : PaymentStatus switch
    {
        PaymentStatuses.Paid => 0,
        PaymentStatuses.DepositPaid => Total - DepositAmount,
        _ => Total,
    };

    // ---------- Read-time state (§8.1, BR-75) ----------

    public string EffectiveStatus(DateOnly today) =>
        Status == QuotationStatuses.Sent && ExpiryDate is { } expiry && today > expiry
            ? QuotationStatuses.Expired
            : Status;

    /// <summary>BR-75: which amounts get a VietQR. Zero amounts are dropped.</summary>
    public IReadOnlyList<PaymentOption> PaymentOptions(DateOnly today)
    {
        var effective = EffectiveStatus(today);
        var options = new List<PaymentOption>();
        var awaitingPayment = effective is QuotationStatuses.Sent or QuotationStatuses.Expired
            || (effective == QuotationStatuses.Accepted && PaymentStatus == PaymentStatuses.Unpaid);

        if (awaitingPayment)
        {
            if (DepositPercent > 0)
                options.Add(new PaymentOption(QrPurposes.Deposit, $"Thanh toán cọc {DepositPercent}%", DepositAmount));
            options.Add(new PaymentOption(QrPurposes.Full, "Thanh toán toàn bộ", Total));
        }
        else if (effective == QuotationStatuses.Accepted && PaymentStatus == PaymentStatuses.DepositPaid)
        {
            options.Add(new PaymentOption(QrPurposes.Remaining, "Thanh toán phần còn lại", Total - DepositAmount));
        }
        return options.Where(o => o.Amount > 0).ToList();
    }

    /// <summary>BR-38: only a DRAFT can be deleted (hard delete).</summary>
    public void EnsureDeletable()
    {
        if (Status != QuotationStatuses.Draft)
            throw new DomainException("QUOTATION_LOCKED", "Chỉ xóa được báo giá nháp (BR-38)");
    }

    // ---------- helpers ----------

    private void EnsureDraft()
    {
        if (Status != QuotationStatuses.Draft)
            throw new DomainException("QUOTATION_LOCKED", "Báo giá đã gửi bị khóa — tạo version mới để sửa (BR-30)");
    }

    /// <summary>§8.3 in order: amount → subtotal → taxable → vat (half-up) → total → deposit (half-up).</summary>
    private void Recalculate()
    {
        foreach (var item in Items)
            item.Amount = checked(item.Quantity * item.UnitPrice);
        Subtotal = Items.Sum(i => i.Amount);
        if (DiscountAmount < 0 || DiscountAmount > Subtotal)
            throw Invalid("Giảm giá phải từ 0 đến tạm tính");
        var taxable = Subtotal - DiscountAmount;
        VatAmount = Money.Percent(taxable, VatPercent);
        Total = taxable + VatAmount;
        DepositAmount = Money.Percent(Total, DepositPercent);
    }

    private QuotationItem NewItem(QuotationItemInput input, int index)
    {
        var item = new QuotationItem { QuotationId = Id };
        Apply(item, input, index);
        return item;
    }

    private static void Apply(QuotationItem item, QuotationItemInput input, int index)
    {
        var description = input.Description.Trim();
        if (description.Length is < 1 or > 500) throw Invalid("Mô tả dòng báo giá dài 1–500 ký tự");
        if (input.Quantity < 1) throw Invalid("Số lượng tối thiểu là 1");
        if (input.UnitPrice < 0) throw Invalid("Đơn giá không được âm");
        if (!QuotationItemSources.All.Contains(input.SourceType)) throw Invalid("Nguồn dòng báo giá không hợp lệ");

        item.Description = description;
        item.Details = input.Details;
        item.Quantity = input.Quantity;
        item.UnitPrice = input.UnitPrice;
        item.SourceType = input.SourceType;
        item.SourceId = input.SourceId;
        item.SortOrder = (index + 1) * 10;
    }

    private static int PaymentRank(string status) => status switch
    {
        PaymentStatuses.Unpaid => 0,
        PaymentStatuses.DepositPaid => 1,
        PaymentStatuses.Paid => 2,
        _ => throw Invalid($"Trạng thái thanh toán không hợp lệ: {status}"),
    };

    private static DomainException Invalid(string detail) => new("VALIDATION_FAILED", detail);

    private static DomainException Transition(string detail) => new("INVALID_TRANSITION", detail);
}
