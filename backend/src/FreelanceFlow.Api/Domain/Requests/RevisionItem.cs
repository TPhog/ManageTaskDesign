using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;

namespace FreelanceFlow.Api.Domain.Requests;

public class RevisionItem : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RoundId { get; set; }
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
    public string Billing { get; set; } = "";
    public string WorkStatus { get; set; } = "TODO";
    public Guid? QuotationItemId { get; set; }
    public string? ScopeOverrideReason { get; set; }
    public string InternalNotes { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public const int MaxDescription = 2000;
    public const int MinOverrideReason = 5;

    /// <summary>
    /// BR-61 + BR-41. <paramref name="linkedQuotationStatus"/> = status of the quotation owning QuotationItemId (null when unlinked).
    /// Leaving CHARGEABLE while linked to a DRAFT/REJECTED line drops the link (ck_items_quotation).
    /// </summary>
    public void Edit(string description, string type, string billing, string internalNotes,
        bool roundHasDeliverable, string? linkedQuotationStatus)
    {
        if (billing != Billing && linkedQuotationStatus is QuotationStatuses.Sent or QuotationStatuses.Accepted)
            throw new DomainException("BILLING_LOCKED",
                "Mục đã nằm trong báo giá đã gửi/chấp nhận — tạo version báo giá mới và bỏ dòng này trước (BR-61)");
        if (!roundHasDeliverable && billing == RevisionBillings.InQuota) throw ProjectLevelInQuota();
        SetContent(description, type, billing);
        InternalNotes = internalNotes;
        if (Billing != RevisionBillings.Chargeable) QuotationItemId = null;
    }

    /// <summary>
    /// BR-60 diagram: TODO → IN_PROGRESS → DONE, TODO/IN_PROGRESS → CANCELLED, DONE → IN_PROGRESS.
    /// CHARGEABLE work (IN_PROGRESS/DONE) needs a line on an ACCEPTED quotation, otherwise SCOPE_GUARD unless a
    /// reason of ≥ 5 characters is given. Returns true when the guard was overridden (caller logs revision_item.scope_override).
    /// </summary>
    public bool ChangeWorkStatus(string to, string? overrideReason, bool linkedToAcceptedQuotation)
    {
        var allowed = (WorkStatus, to) switch
        {
            (RevisionWorkStatuses.Todo, RevisionWorkStatuses.InProgress) => true,
            (RevisionWorkStatuses.Todo, RevisionWorkStatuses.Cancelled) => true,
            (RevisionWorkStatuses.InProgress, RevisionWorkStatuses.Done) => true,
            (RevisionWorkStatuses.InProgress, RevisionWorkStatuses.Cancelled) => true,
            (RevisionWorkStatuses.Done, RevisionWorkStatuses.InProgress) => true,
            _ => false,
        };
        if (!allowed) throw new DomainException("INVALID_TRANSITION", $"{WorkStatus} → {to}");

        var overridden = false;
        if (to is RevisionWorkStatuses.InProgress or RevisionWorkStatuses.Done
            && Billing == RevisionBillings.Chargeable && !linkedToAcceptedQuotation)
        {
            var reason = overrideReason?.Trim() ?? "";
            if (reason.Length < MinOverrideReason)
                throw new DomainException("SCOPE_GUARD",
                    "Mục này tính phí nhưng báo giá chưa được chấp nhận — nhập lý do (tối thiểu 5 ký tự) để vẫn làm (BR-60)");
            ScopeOverrideReason = reason;
            overridden = true;
        }
        WorkStatus = to;
        return overridden;
    }

    /// <summary>BR-46: link to the ADDITIONAL draft line built for this item.</summary>
    public void LinkQuotationLine(Guid quotationItemId)
    {
        if (Billing != RevisionBillings.Chargeable || QuotationItemId is not null || WorkStatus == RevisionWorkStatuses.Cancelled)
            throw new DomainException("BUSINESS_RULE", "Chỉ mục tính phí chưa báo giá mới được đưa vào báo giá phát sinh (BR-46)");
        QuotationItemId = quotationItemId;
    }

    /// <summary>BR-47: when the next quotation version is sent, follow the copied line; a dropped line unlinks. True = moved.</summary>
    public bool FollowQuotationLine(IReadOnlyDictionary<Guid, Guid> copiedLineByOldLine)
    {
        if (QuotationItemId is { } old && copiedLineByOldLine.TryGetValue(old, out var next))
        {
            QuotationItemId = next;
            return true;
        }
        QuotationItemId = null;
        return false;
    }

    internal void SetContent(string description, string type, string billing)
    {
        var text = description.Trim();
        if (text.Length is < 1 or > MaxDescription) throw Invalid($"Mô tả dài 1–{MaxDescription} ký tự");
        if (!RevisionItemTypes.All.Contains(type)) throw Invalid("Loại yêu cầu sửa không hợp lệ");
        if (!RevisionBillings.All.Contains(billing)) throw Invalid("Cách tính phí không hợp lệ");
        Description = text;
        Type = type;
        Billing = billing;
    }

    internal static DomainException ProjectLevelInQuota() =>
        new("BUSINESS_RULE", "Vòng sửa cấp dự án không được chứa mục trong quota (BR-41)");

    private static DomainException Invalid(string detail) => new("VALIDATION_FAILED", detail);
}
