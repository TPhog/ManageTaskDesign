namespace FreelanceFlow.Api.Domain.Dashboard;

public sealed record SetupFacts(
    bool HasStudioName, bool HasBank, bool HasPricedService, bool HasTaskTemplate, bool WatermarkReviewed, bool HasClient);

public sealed record SetupItem(string Key, string Label, string Hint, bool Done, string Href);

/// <summary>P11 BR-96 (DF-04): first-run checklist shown on the dashboard until complete or dismissed.</summary>
public static class SetupChecklist
{
    public static IReadOnlyList<SetupItem> Build(SetupFacts f) =>
    [
        new("STUDIO", "Điền thông tin studio", "Tên studio hiện trên báo giá gửi khách", f.HasStudioName, "/admin/settings"),
        new("BANK", "Nhập tài khoản ngân hàng", "Để báo giá có mã VietQR cho khách quét", f.HasBank, "/admin/settings"),
        new("SERVICE", "Tạo dịch vụ đầu tiên có giá", "Dịch vụ giúp điền sẵn đơn giá khi lập báo giá", f.HasPricedService, "/admin/services"),
        new("TASK_TEMPLATE", "Thêm các bước công việc mẫu cho dịch vụ", "Task tự sinh khi bắt đầu dự án", f.HasTaskTemplate, "/admin/services"),
        new("WATERMARK", "Xem lại watermark ảnh", "Chữ chìm trên ảnh khách xem; lưu lại để xác nhận", f.WatermarkReviewed, "/admin/settings"),
        new("CLIENT", "Thêm khách hàng đầu tiên", "Rồi tạo dự án cho khách đó", f.HasClient, "/admin/clients"),
    ];
}
