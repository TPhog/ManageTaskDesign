using FreelanceFlow.Api.Domain.Quotations;

namespace FreelanceFlow.Api.Features.Quotations;

/// <summary>400 field errors (docs/05 §1.2). Domain methods re-check the same invariants.</summary>
internal static class QuotationValidation
{
    private const int MaxTitle = 200;
    private const int MaxText = 5000;
    private const int MaxQuantity = 10_000;
    internal const long MaxUnitPrice = 100_000_000_000; // 100 tỷ đồng: keeps quantity × unitPrice far from long overflow

    public static Dictionary<string, string[]> ValidateCreate(CreateQuotationRequest body)
    {
        var errors = new Dictionary<string, string[]>();
        if (body.Kind is not null && body.Kind != QuotationKinds.Initial)
            errors["kind"] = ["Chỉ tạo báo giá INITIAL ở đây; báo giá phát sinh được tạo từ revision"];
        if (body.Title is { Length: > MaxTitle })
            errors["title"] = [$"Tối đa {MaxTitle} ký tự"];
        if (!body.FromDeliverables && body.DeliverableIds is { Length: > 0 })
            errors["deliverableIds"] = ["Chỉ dùng khi lập báo giá từ deliverable"];
        if (body.DeliverablePrices is { Length: > 0 } prices)
        {
            if (!body.FromDeliverables)
                errors["deliverablePrices"] = ["Chỉ dùng khi lập báo giá từ deliverable"];
            else if (prices.Any(p => p.UnitPrice is < 0 or > MaxUnitPrice))
                errors["deliverablePrices"] = ["Đơn giá từ 0 đến 100.000.000.000 ₫"];
        }
        return errors;
    }

    public static Dictionary<string, string[]> ValidateUpdate(UpdateQuotationRequest body)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(body.Title)) errors["title"] = ["Vui lòng nhập tiêu đề"];
        else if (body.Title.Trim().Length > MaxTitle) errors["title"] = [$"Tối đa {MaxTitle} ký tự"];
        if (body.ValidityDays is < 1 or > 365) errors["validityDays"] = ["Từ 1 đến 365 ngày"];
        if (body.DiscountAmount < 0) errors["discountAmount"] = ["Không được âm"];
        if (body.VatPercent is < 0 or > 20) errors["vatPercent"] = ["VAT từ 0 đến 20%"];
        if (body.DepositPercent is < 0 or > 100) errors["depositPercent"] = ["Cọc từ 0 đến 100%"];
        if ((body.Terms?.Length ?? 0) > MaxText) errors["terms"] = [$"Tối đa {MaxText} ký tự"];
        if ((body.NotesToClient?.Length ?? 0) > MaxText) errors["notesToClient"] = [$"Tối đa {MaxText} ký tự"];
        if ((body.InternalNotes?.Length ?? 0) > MaxText) errors["internalNotes"] = [$"Tối đa {MaxText} ký tự"];
        if (body.Items is null)
        {
            errors["items"] = ["Thiếu danh sách dòng báo giá"];
            return errors;
        }
        for (var i = 0; i < body.Items.Length; i++)
        {
            var item = body.Items[i];
            if (string.IsNullOrWhiteSpace(item.Description) || item.Description.Trim().Length > 500)
                errors[$"items[{i}].description"] = ["Mô tả dài 1–500 ký tự"];
            if (item.Quantity is < 1 or > MaxQuantity)
                errors[$"items[{i}].quantity"] = [$"Số lượng từ 1 đến {MaxQuantity}"];
            if (item.UnitPrice is < 0 or > MaxUnitPrice)
                errors[$"items[{i}].unitPrice"] = ["Đơn giá không hợp lệ"];
            if (item.SourceType is not null && !QuotationItemSources.All.Contains(item.SourceType))
                errors[$"items[{i}].sourceType"] = ["Nguồn dòng không hợp lệ"];
        }
        return errors;
    }

    public static Dictionary<string, string[]> ValidateReject(RejectQuotationRequest body) =>
        string.IsNullOrWhiteSpace(body.Reason)
            ? new() { ["reason"] = ["Bắt buộc lý do từ chối"] }
            : new();

    public static Dictionary<string, string[]> ValidatePayment(PaymentStatusRequest body) =>
        body.To is null || !PaymentStatuses.All.Contains(body.To)
            ? new() { ["to"] = ["Trạng thái thanh toán không hợp lệ"] }
            : new();
}
