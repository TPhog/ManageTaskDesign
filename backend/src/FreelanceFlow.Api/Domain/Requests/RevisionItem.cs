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
    /// <summary>Plan 09: service picked for a NEW_DELIVERABLE line in from-revisions; used when the quotation is accepted (BR-48).</summary>
    public Guid? ServiceId { get; set; }
    /// <summary>BR-48: deliverable created from this NEW_DELIVERABLE item when its quotation was accepted.</summary>
    public Guid? CreatedDeliverableId { get; set; }
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
        if (CreatedDeliverableId is not null && type != RevisionItemTypes.NewDeliverable)
            throw new DomainException("BUSINESS_RULE", "Mục đã thành hạng mục trong dự án — không đổi loại được (BR-48)");
        SetContent(description, type, billing);
        InternalNotes = internalNotes;
        if (Billing != RevisionBillings.Chargeable) QuotationItemId = null;
        if (Type != RevisionItemTypes.NewDeliverable) ServiceId = null;
    }

    /// <summary>
    /// BR-60 diagram: TODO → IN_PROGRESS → DONE, TODO/IN_PROGRESS → CANCELLED, DONE → IN_PROGRESS.
    /// CHARGEABLE work (IN_PROGRESS/DONE) needs a line on an ACCEPTED quotation, otherwise SCOPE_GUARD unless a
    /// reason of ≥ 5 characters is given. Returns true when the guard was overridden (caller logs revision_item.scope_override).
    /// </summary>
    public bool ChangeWorkStatus(string to, string? overrideReason, bool linkedToAcceptedQuotation)
    {
        if (!RevisionWorkStatuses.All.Contains(to)) throw Invalid("Trạng thái không hợp lệ");
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

    /// <summary>BR-46: link to the ADDITIONAL draft line built for this item; a NEW_DELIVERABLE keeps the picked service (plan 09).</summary>
    public void LinkQuotationLine(Guid quotationItemId, Guid? serviceId = null)
    {
        if (Billing != RevisionBillings.Chargeable || QuotationItemId is not null || WorkStatus == RevisionWorkStatuses.Cancelled)
            throw new DomainException("BUSINESS_RULE", "Chỉ mục tính phí chưa báo giá mới được đưa vào báo giá phát sinh (BR-46)");
        if (serviceId is not null && Type != RevisionItemTypes.NewDeliverable)
            throw new DomainException("VALIDATION_FAILED", "Chỉ mục \"Hạng mục mới\" mới gắn dịch vụ");
        QuotationItemId = quotationItemId;
        ServiceId = Type == RevisionItemTypes.NewDeliverable ? serviceId : null;
    }

    /// <summary>BR-48: the accepted NEW_DELIVERABLE line became deliverable <paramref name="deliverableId"/> — once only.</summary>
    public void MarkDeliverableCreated(Guid deliverableId)
    {
        if (Type != RevisionItemTypes.NewDeliverable || CreatedDeliverableId is not null)
            throw new DomainException("BUSINESS_RULE", "Mục này đã thành hạng mục hoặc không phải \"Hạng mục mới\" (BR-48)");
        CreatedDeliverableId = deliverableId;
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
