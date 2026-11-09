using System.Text;
using FreelanceFlow.Api.Domain.Projects;
using static FreelanceFlow.Api.Domain.RequestTemplates.TemplateFieldTypes;

namespace FreelanceFlow.Api.Domain.RequestTemplates;

public sealed record RequestTemplatePreset(string Name, string Description, string DesignType, IReadOnlyList<TemplateField> Fields);

/// <summary>
/// The six templates seeded ONCE by migration 20261113090000_AddRequestTemplatesAndIntake (decision #1).
/// The migration holds a frozen copy of <see cref="SeedSql"/>'s output (R2 M6), so editing a preset here does not
/// change existing or fresh databases; seeding changed presets needs a new migration.
/// The designer edits or deletes the seeded rows freely afterwards.
/// </summary>
public static class RequestTemplatePresets
{
    private static readonly string[] Styles =
        ["Hiện đại, tối giản", "Sang trọng", "Trẻ trung, nhiều màu", "Dễ thương", "Truyền thống", "Để designer đề xuất"];
    private static readonly string[] RequiredInfo =
        ["Giá", "Địa chỉ", "Hotline", "Website / Fanpage", "Mã QR", "Logo", "Thời gian chương trình"];

    private static TemplateField Short(string key, string label, bool required = false, string? help = null) => new(key, label, ShortText, required, help, []);
    private static TemplateField Long(string key, string label, bool required = false, string? help = null) => new(key, label, LongText, required, help, []);
    private static TemplateField One(string key, string label, bool required, string? help, params string[] options) => new(key, label, SingleChoice, required, help, options);
    private static TemplateField Many(string key, string label, bool required, string? help, params string[] options) => new(key, label, MultiChoice, required, help, options);
    private static TemplateField Sizes(string key, string label, bool required, params string[] presets) =>
        new(key, label, Size, required, "Chọn kích thước có sẵn hoặc nhập kích thước tuỳ chỉnh", presets);
    private static TemplateField Day(string key, string label, bool required = false) => new(key, label, Date, required, null, []);
    private static TemplateField Num(string key, string label, bool required = false, string? help = null) => new(key, label, Number, required, help, []);
    private static TemplateField Refs(string label) =>
        new("references", label, Images, false, "Logo, ảnh sản phẩm hoặc mẫu bạn thích — JPG, PNG, WebP", []);

    private static TemplateField Brand() => Short("brand", "Màu sắc / thương hiệu", help: "Màu chủ đạo, font hoặc bộ nhận diện có sẵn");
    private static TemplateField Style() => One("style", "Phong cách", false, null, Styles);
    private static TemplateField Budget() => Num("budget", "Ngân sách dự kiến (đồng)", help: "Không bắt buộc — giúp designer đề xuất phương án phù hợp");
    private static TemplateField Deadline() => Day("deadline", "Hạn mong muốn");

    public static readonly IReadOnlyList<RequestTemplatePreset> All =
    [
        new("Banner quảng cáo", "Banner online (Facebook, website, sàn TMĐT) hoặc banner in.", ProjectDesignTypes.Banner,
        [
            One("purpose", "Mục đích", true, null, "Quảng cáo sản phẩm / dịch vụ", "Khuyến mãi / giảm giá", "Khai trương", "Tuyển dụng", "Thông báo", "Khác"),
            Many("channel", "Kênh / nơi đặt", true, null, "Facebook / Instagram", "Website", "Shopee / Lazada / TikTok Shop", "Google Ads", "In bạt / hiflex", "Standee / X-banner", "Khác"),
            Sizes("size", "Kích thước", true, "1080 × 1080 px (vuông)", "1200 × 628 px (quảng cáo Facebook)", "1080 × 1920 px (story/reels)",
                "1920 × 1080 px (ngang 16:9)", "60 × 160 cm (standee cuốn)", "80 × 200 cm (standee cuốn)"),
            Short("headline", "Nội dung chữ chính", true, "Câu chữ lớn nhất trên banner, ví dụ: GIẢM 50% TOÀN BỘ MENU"),
            Long("details", "Nội dung phụ", help: "Mô tả ngắn, điều kiện chương trình…"),
            Many("required_info", "Thông tin bắt buộc có", false, null, RequiredInfo),
            Brand(), Style(), Refs("Ảnh / logo tham khảo"),
            Num("quantity", "Số lượng mẫu / kích thước cần làm"),
            Deadline(), Budget(),
        ]),
        new("Poster", "Poster in hoặc đăng online cho sản phẩm, sự kiện, thông báo.", ProjectDesignTypes.Poster,
        [
            Short("purpose", "Mục đích poster", true, "Ví dụ: giới thiệu món mới, thông báo giờ mở cửa"),
            One("format", "Hình thức", true, null, "In ấn", "Đăng online", "Cả hai"),
            Sizes("size", "Kích thước", true, "A4 (210 × 297 mm)", "A3 (297 × 420 mm)", "A2 (420 × 594 mm)",
                "1080 × 1350 px (dọc 4:5)", "1080 × 1920 px (story/reels)"),
            Short("headline", "Tiêu đề chính", true),
            Long("details", "Nội dung chi tiết"),
            Many("required_info", "Thông tin bắt buộc có", false, null, RequiredInfo),
            Brand(), Style(), Refs("Ảnh / logo tham khảo"),
            Num("quantity", "Số phương án mong muốn"),
            Deadline(), Budget(),
        ]),
        new("Menu cửa hàng", "Menu quán ăn, cà phê, trà sữa — bản in hoặc bản online.", ProjectDesignTypes.Print,
        [
            One("shop_type", "Loại cửa hàng", true, null, "Quán cà phê", "Trà sữa", "Nhà hàng / quán ăn", "Tiệm bánh", "Quán nhậu", "Khác"),
            One("format", "Dạng menu", true, null, "1 mặt", "2 mặt", "Gập 3 (tri-fold)", "Cuốn nhiều trang", "Bảng menu treo tường", "Menu online / QR"),
            Sizes("size", "Kích thước", true, "A4 (210 × 297 mm)", "A3 (297 × 420 mm)", "A5 (148 × 210 mm)", "1080 × 1350 px (dọc 4:5)"),
            Num("item_count", "Số món (ước lượng)", true),
            Long("items", "Danh sách món và giá", help: "Mỗi món một dòng: Tên món — giá. Có thể gửi ảnh menu cũ ở mục ảnh."),
            Many("required_info", "Thông tin cần có", false, null, "Logo", "Địa chỉ", "Hotline", "Wifi", "Mã QR", "Giờ mở cửa", "Fanpage"),
            One("photos", "Ảnh món ăn", false, null, "Tôi có ảnh sẵn", "Cần designer tìm ảnh minh hoạ", "Không dùng ảnh"),
            Brand(), Style(), Refs("Ảnh menu cũ / logo / ảnh món"),
            Deadline(), Budget(),
        ]),
        new("Banner sự kiện / chương trình", "Backdrop, banner, standee cho sự kiện, khai trương, hội thảo, chương trình khuyến mãi.", ProjectDesignTypes.Banner,
        [
            Short("event_name", "Tên sự kiện / chương trình", true),
            Day("event_date", "Ngày diễn ra", true),
            Short("venue", "Địa điểm"),
            Many("items", "Cần thiết kế", true, null, "Backdrop sân khấu", "Banner ngang", "Standee", "Bài đăng mạng xã hội", "Thư mời", "Vé / thẻ đeo", "Khác"),
            Sizes("size", "Kích thước (nếu đã có)", false, "3 × 2 m (backdrop)", "4 × 2,5 m (backdrop)", "60 × 160 cm (standee cuốn)",
                "80 × 200 cm (standee cuốn)", "1200 × 628 px (quảng cáo Facebook)", "1920 × 1080 px (ngang 16:9)"),
            Short("headline", "Nội dung chữ chính", true),
            Long("details", "Thông tin khác", help: "Nhà tài trợ, lịch chương trình, khách mời…"),
            Many("required_info", "Thông tin bắt buộc có", false, null, RequiredInfo),
            Brand(), Style(), Refs("Ảnh / logo tham khảo"),
            Day("deadline", "Hạn mong muốn nhận file"), Budget(),
        ]),
        new("Bài đăng mạng xã hội", "Ảnh bài đăng Facebook, Instagram, TikTok, Zalo.", ProjectDesignTypes.SocialMedia,
        [
            Many("platform", "Nền tảng", true, null, "Facebook", "Instagram", "TikTok", "Zalo", "Khác"),
            One("post_type", "Dạng bài", true, null, "Ảnh đơn", "Album nhiều ảnh (carousel)", "Story / Reels", "Ảnh bìa (cover)"),
            Sizes("size", "Kích thước", true, "1080 × 1080 px (vuông)", "1080 × 1350 px (dọc 4:5)", "1080 × 1920 px (story/reels)", "851 × 315 px (ảnh bìa Facebook)"),
            Num("post_count", "Số lượng bài / ảnh", true),
            Short("headline", "Nội dung chữ chính"),
            Long("details", "Nội dung / caption tham khảo"),
            Brand(), Style(), Refs("Ảnh / logo tham khảo"),
            Deadline(), Budget(),
        ]),
        new("Logo", "Thiết kế logo mới hoặc làm mới logo cũ.", ProjectDesignTypes.Logo,
        [
            Short("brand_name", "Tên thương hiệu (chữ trên logo)", true),
            Short("slogan", "Khẩu hiệu (nếu có)"),
            Short("industry", "Ngành nghề / sản phẩm", true),
            One("kind", "Loại logo", false, null, "Chỉ chữ", "Biểu tượng + chữ", "Biểu tượng riêng", "Để designer đề xuất"),
            Short("colors", "Màu sắc mong muốn / không muốn"),
            Style(),
            Many("usage", "Nơi sử dụng logo", false, null, "Biển hiệu", "Bao bì / nhãn", "Mạng xã hội", "Website", "Đồng phục", "Danh thiếp"),
            Refs("Logo hiện tại / mẫu tham khảo"),
            Num("concept_count", "Số phương án mong muốn"),
            Deadline(), Budget(),
        ]),
    ];

    /// <summary>INSERT statements for the migration (sort_order 10, 20, …; active; no service).</summary>
    public static string SeedSql()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < All.Count; i++)
        {
            var p = All[i];
            var fields = TemplateFieldRules.Serialize(TemplateFieldRules.Normalize(p.Fields.Cast<TemplateField?>().ToList()));
            sb.Append("INSERT INTO request_templates (id, name, description, design_type, service_id, fields, active, sort_order, created_at, updated_at) VALUES (gen_random_uuid(), ")
              .Append(Literal(p.Name)).Append(", ").Append(Literal(p.Description)).Append(", ").Append(Literal(p.DesignType))
              .Append(", NULL, ").Append(Literal(fields)).Append("::jsonb, true, ").Append((i + 1) * 10).Append(", now(), now());\n");
        }
        return sb.ToString();
    }

    private static string Literal(string s) => "'" + s.Replace("'", "''") + "'";
}
