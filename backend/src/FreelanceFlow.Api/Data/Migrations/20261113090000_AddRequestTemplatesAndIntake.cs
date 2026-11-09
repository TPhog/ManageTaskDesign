using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestTemplatesAndIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_files_owner_type",
                table: "files");

            migrationBuilder.AddColumn<bool>(
                name: "intake_enabled",
                table: "settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "intake_slug",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "answers",
                table: "client_requests",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<Guid>(
                name: "template_id",
                table: "client_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "template_name",
                table: "client_requests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "request_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    design_type = table.Column<string>(type: "text", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fields = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_templates", x => x.id);
                    table.CheckConstraint("ck_request_templates_description", "char_length(description) <= 500");
                    table.CheckConstraint("ck_request_templates_design_type", "design_type IN ('LOGO','BANNER','POSTER','BRANDING','UI_UX','SOCIAL_MEDIA','PACKAGING','PRINT','OTHER')");
                    table.CheckConstraint("ck_request_templates_fields", "jsonb_typeof(fields) = 'array' AND jsonb_array_length(fields) <= 30");
                    table.CheckConstraint("ck_request_templates_name", "char_length(name) BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_request_templates_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "intake_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    template_name = table.Column<string>(type: "text", nullable: true),
                    contact_name = table.Column<string>(type: "text", nullable: false),
                    contact_phone = table.Column<string>(type: "text", nullable: false),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    answers = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    note = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "NEW"),
                    reject_reason = table.Column<string>(type: "text", nullable: true),
                    converted_client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    converted_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    converted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intake_submissions", x => x.id);
                    table.CheckConstraint("ck_intake_answers", "jsonb_typeof(answers) = 'array'");
                    table.CheckConstraint("ck_intake_body", "char_length(body) BETWEEN 1 AND 10000");
                    table.CheckConstraint("ck_intake_contact_email", "contact_email IS NULL OR char_length(contact_email) <= 200");
                    table.CheckConstraint("ck_intake_contact_name", "char_length(contact_name) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_intake_contact_phone", "char_length(contact_phone) BETWEEN 1 AND 20");
                    table.CheckConstraint("ck_intake_converted", "(status = 'CONVERTED') = (converted_client_id IS NOT NULL AND converted_project_id IS NOT NULL AND converted_at IS NOT NULL)");
                    table.CheckConstraint("ck_intake_note", "char_length(note) <= 2000");
                    table.CheckConstraint("ck_intake_reject_reason", "reject_reason IS NULL OR char_length(reject_reason) <= 500");
                    table.CheckConstraint("ck_intake_rejected", "(status = 'REJECTED') = (rejected_at IS NOT NULL)");
                    table.CheckConstraint("ck_intake_status", "status IN ('NEW','CONVERTED','REJECTED')");
                    table.ForeignKey(
                        name: "FK_intake_submissions_clients_converted_client_id",
                        column: x => x.converted_client_id,
                        principalTable: "clients",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_intake_submissions_projects_converted_project_id",
                        column: x => x.converted_project_id,
                        principalTable: "projects",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_intake_submissions_request_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "request_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ux_settings_intake_slug",
                table: "settings",
                column: "intake_slug",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_settings_intake_enabled",
                table: "settings",
                sql: "NOT intake_enabled OR intake_slug IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_settings_intake_slug",
                table: "settings",
                sql: "intake_slug IS NULL OR intake_slug ~ '^[A-Za-z0-9_-]{22}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_files_image_project",
                table: "files",
                sql: "owner_type IS NULL OR owner_type = 'INTAKE_SUBMISSION' OR project_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_files_intake_request",
                table: "files",
                sql: "owner_type IS DISTINCT FROM 'INTAKE_SUBMISSION' OR request_id IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_files_owner_type",
                table: "files",
                sql: "owner_type IS NULL OR owner_type IN ('TASK','PROGRESS_UPDATE','CLIENT_REQUEST','DELIVERABLE','INTAKE_SUBMISSION')");

            migrationBuilder.CreateIndex(
                name: "IX_client_requests_template_id",
                table: "client_requests",
                column: "template_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_requests_answers",
                table: "client_requests",
                sql: "jsonb_typeof(answers) = 'array'");

            migrationBuilder.CreateIndex(
                name: "ix_intake_status",
                table: "intake_submissions",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_intake_submissions_converted_client_id",
                table: "intake_submissions",
                column: "converted_client_id");

            migrationBuilder.CreateIndex(
                name: "IX_intake_submissions_converted_project_id",
                table: "intake_submissions",
                column: "converted_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_intake_submissions_template_id",
                table: "intake_submissions",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_templates_order",
                table: "request_templates",
                columns: new[] { "sort_order", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_request_templates_service_id",
                table: "request_templates",
                column: "service_id");

            migrationBuilder.AddForeignKey(
                name: "FK_client_requests_request_templates_template_id",
                table: "client_requests",
                column: "template_id",
                principalTable: "request_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Plan 13 decision #1: the six presets, seeded once; the designer edits/deletes them afterwards.
            migrationBuilder.Sql(PresetSeedSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_client_requests_request_templates_template_id",
                table: "client_requests");

            migrationBuilder.DropTable(
                name: "intake_submissions");

            migrationBuilder.DropTable(
                name: "request_templates");

            migrationBuilder.DropIndex(
                name: "ux_settings_intake_slug",
                table: "settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_settings_intake_enabled",
                table: "settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_settings_intake_slug",
                table: "settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_files_image_project",
                table: "files");

            migrationBuilder.DropCheckConstraint(
                name: "ck_files_intake_request",
                table: "files");

            migrationBuilder.DropCheckConstraint(
                name: "ck_files_owner_type",
                table: "files");

            migrationBuilder.DropIndex(
                name: "IX_client_requests_template_id",
                table: "client_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_requests_answers",
                table: "client_requests");

            migrationBuilder.DropColumn(
                name: "intake_enabled",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "intake_slug",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "answers",
                table: "client_requests");

            migrationBuilder.DropColumn(
                name: "template_id",
                table: "client_requests");

            migrationBuilder.DropColumn(
                name: "template_name",
                table: "client_requests");

            migrationBuilder.AddCheckConstraint(
                name: "ck_files_owner_type",
                table: "files",
                sql: "owner_type IS NULL OR owner_type IN ('TASK','PROGRESS_UPDATE','CLIENT_REQUEST','DELIVERABLE')");
        }

        /// <summary>
        /// Frozen copy of RequestTemplatePresets.SeedSql() as released (R2 M6): a migration must replay the same rows on a
        /// fresh database even when the preset class changes later or loses SeedSql().
        /// </summary>
        private const string PresetSeedSql = """
            INSERT INTO request_templates (id, name, description, design_type, service_id, fields, active, sort_order, created_at, updated_at) VALUES (gen_random_uuid(), 'Banner quảng cáo', 'Banner online (Facebook, website, sàn TMĐT) hoặc banner in.', 'BANNER', NULL, '[{"key":"purpose","label":"Mục đích","type":"SINGLE_CHOICE","required":true,"help":null,"options":["Quảng cáo sản phẩm / dịch vụ","Khuyến mãi / giảm giá","Khai trương","Tuyển dụng","Thông báo","Khác"]},{"key":"channel","label":"Kênh / nơi đặt","type":"MULTI_CHOICE","required":true,"help":null,"options":["Facebook / Instagram","Website","Shopee / Lazada / TikTok Shop","Google Ads","In bạt / hiflex","Standee / X-banner","Khác"]},{"key":"size","label":"Kích thước","type":"SIZE","required":true,"help":"Chọn kích thước có sẵn hoặc nhập kích thước tuỳ chỉnh","options":["1080 × 1080 px (vuông)","1200 × 628 px (quảng cáo Facebook)","1080 × 1920 px (story/reels)","1920 × 1080 px (ngang 16:9)","60 × 160 cm (standee cuốn)","80 × 200 cm (standee cuốn)"]},{"key":"headline","label":"Nội dung chữ chính","type":"SHORT_TEXT","required":true,"help":"Câu chữ lớn nhất trên banner, ví dụ: GIẢM 50% TOÀN BỘ MENU","options":[]},{"key":"details","label":"Nội dung phụ","type":"LONG_TEXT","required":false,"help":"Mô tả ngắn, điều kiện chương trình…","options":[]},{"key":"required_info","label":"Thông tin bắt buộc có","type":"MULTI_CHOICE","required":false,"help":null,"options":["Giá","Địa chỉ","Hotline","Website / Fanpage","Mã QR","Logo","Thời gian chương trình"]},{"key":"brand","label":"Màu sắc / thương hiệu","type":"SHORT_TEXT","required":false,"help":"Màu chủ đạo, font hoặc bộ nhận diện có sẵn","options":[]},{"key":"style","label":"Phong cách","type":"SINGLE_CHOICE","required":false,"help":null,"options":["Hiện đại, tối giản","Sang trọng","Trẻ trung, nhiều màu","Dễ thương","Truyền thống","Để designer đề xuất"]},{"key":"references","label":"Ảnh / logo tham khảo","type":"IMAGES","required":false,"help":"Logo, ảnh sản phẩm hoặc mẫu bạn thích — JPG, PNG, WebP","options":[]},{"key":"quantity","label":"Số lượng mẫu / kích thước cần làm","type":"NUMBER","required":false,"help":null,"options":[]},{"key":"deadline","label":"Hạn mong muốn","type":"DATE","required":false,"help":null,"options":[]},{"key":"budget","label":"Ngân sách dự kiến (đồng)","type":"NUMBER","required":false,"help":"Không bắt buộc — giúp designer đề xuất phương án phù hợp","options":[]}]'::jsonb, true, 10, now(), now());
            INSERT INTO request_templates (id, name, description, design_type, service_id, fields, active, sort_order, created_at, updated_at) VALUES (gen_random_uuid(), 'Poster', 'Poster in hoặc đăng online cho sản phẩm, sự kiện, thông báo.', 'POSTER', NULL, '[{"key":"purpose","label":"Mục đích poster","type":"SHORT_TEXT","required":true,"help":"Ví dụ: giới thiệu món mới, thông báo giờ mở cửa","options":[]},{"key":"format","label":"Hình thức","type":"SINGLE_CHOICE","required":true,"help":null,"options":["In ấn","Đăng online","Cả hai"]},{"key":"size","label":"Kích thước","type":"SIZE","required":true,"help":"Chọn kích thước có sẵn hoặc nhập kích thước tuỳ chỉnh","options":["A4 (210 × 297 mm)","A3 (297 × 420 mm)","A2 (420 × 594 mm)","1080 × 1350 px (dọc 4:5)","1080 × 1920 px (story/reels)"]},{"key":"headline","label":"Tiêu đề chính","type":"SHORT_TEXT","required":true,"help":null,"options":[]},{"key":"details","label":"Nội dung chi tiết","type":"LONG_TEXT","required":false,"help":null,"options":[]},{"key":"required_info","label":"Thông tin bắt buộc có","type":"MULTI_CHOICE","required":false,"help":null,"options":["Giá","Địa chỉ","Hotline","Website / Fanpage","Mã QR","Logo","Thời gian chương trình"]},{"key":"brand","label":"Màu sắc / thương hiệu","type":"SHORT_TEXT","required":false,"help":"Màu chủ đạo, font hoặc bộ nhận diện có sẵn","options":[]},{"key":"style","label":"Phong cách","type":"SINGLE_CHOICE","required":false,"help":null,"options":["Hiện đại, tối giản","Sang trọng","Trẻ trung, nhiều màu","Dễ thương","Truyền thống","Để designer đề xuất"]},{"key":"references","label":"Ảnh / logo tham khảo","type":"IMAGES","required":false,"help":"Logo, ảnh sản phẩm hoặc mẫu bạn thích — JPG, PNG, WebP","options":[]},{"key":"quantity","label":"Số phương án mong muốn","type":"NUMBER","required":false,"help":null,"options":[]},{"key":"deadline","label":"Hạn mong muốn","type":"DATE","required":false,"help":null,"options":[]},{"key":"budget","label":"Ngân sách dự kiến (đồng)","type":"NUMBER","required":false,"help":"Không bắt buộc — giúp designer đề xuất phương án phù hợp","options":[]}]'::jsonb, true, 20, now(), now());
            INSERT INTO request_templates (id, name, description, design_type, service_id, fields, active, sort_order, created_at, updated_at) VALUES (gen_random_uuid(), 'Menu cửa hàng', 'Menu quán ăn, cà phê, trà sữa — bản in hoặc bản online.', 'PRINT', NULL, '[{"key":"shop_type","label":"Loại cửa hàng","type":"SINGLE_CHOICE","required":true,"help":null,"options":["Quán cà phê","Trà sữa","Nhà hàng / quán ăn","Tiệm bánh","Quán nhậu","Khác"]},{"key":"format","label":"Dạng menu","type":"SINGLE_CHOICE","required":true,"help":null,"options":["1 mặt","2 mặt","Gập 3 (tri-fold)","Cuốn nhiều trang","Bảng menu treo tường","Menu online / QR"]},{"key":"size","label":"Kích thước","type":"SIZE","required":true,"help":"Chọn kích thước có sẵn hoặc nhập kích thước tuỳ chỉnh","options":["A4 (210 × 297 mm)","A3 (297 × 420 mm)","A5 (148 × 210 mm)","1080 × 1350 px (dọc 4:5)"]},{"key":"item_count","label":"Số món (ước lượng)","type":"NUMBER","required":true,"help":null,"options":[]},{"key":"items","label":"Danh sách món và giá","type":"LONG_TEXT","required":false,"help":"Mỗi món một dòng: Tên món — giá. Có thể gửi ảnh menu cũ ở mục ảnh.","options":[]},{"key":"required_info","label":"Thông tin cần có","type":"MULTI_CHOICE","required":false,"help":null,"options":["Logo","Địa chỉ","Hotline","Wifi","Mã QR","Giờ mở cửa","Fanpage"]},{"key":"photos","label":"Ảnh món ăn","type":"SINGLE_CHOICE","required":false,"help":null,"options":["Tôi có ảnh sẵn","Cần designer tìm ảnh minh hoạ","Không dùng ảnh"]},{"key":"brand","label":"Màu sắc / thương hiệu","type":"SHORT_TEXT","required":false,"help":"Màu chủ đạo, font hoặc bộ nhận diện có sẵn","options":[]},{"key":"style","label":"Phong cách","type":"SINGLE_CHOICE","required":false,"help":null,"options":["Hiện đại, tối giản","Sang trọng","Trẻ trung, nhiều màu","Dễ thương","Truyền thống","Để designer đề xuất"]},{"key":"references","label":"Ảnh menu cũ / logo / ảnh món","type":"IMAGES","required":false,"help":"Logo, ảnh sản phẩm hoặc mẫu bạn thích — JPG, PNG, WebP","options":[]},{"key":"deadline","label":"Hạn mong muốn","type":"DATE","required":false,"help":null,"options":[]},{"key":"budget","label":"Ngân sách dự kiến (đồng)","type":"NUMBER","required":false,"help":"Không bắt buộc — giúp designer đề xuất phương án phù hợp","options":[]}]'::jsonb, true, 30, now(), now());
            INSERT INTO request_templates (id, name, description, design_type, service_id, fields, active, sort_order, created_at, updated_at) VALUES (gen_random_uuid(), 'Banner sự kiện / chương trình', 'Backdrop, banner, standee cho sự kiện, khai trương, hội thảo, chương trình khuyến mãi.', 'BANNER', NULL, '[{"key":"event_name","label":"Tên sự kiện / chương trình","type":"SHORT_TEXT","required":true,"help":null,"options":[]},{"key":"event_date","label":"Ngày diễn ra","type":"DATE","required":true,"help":null,"options":[]},{"key":"venue","label":"Địa điểm","type":"SHORT_TEXT","required":false,"help":null,"options":[]},{"key":"items","label":"Cần thiết kế","type":"MULTI_CHOICE","required":true,"help":null,"options":["Backdrop sân khấu","Banner ngang","Standee","Bài đăng mạng xã hội","Thư mời","Vé / thẻ đeo","Khác"]},{"key":"size","label":"Kích thước (nếu đã có)","type":"SIZE","required":false,"help":"Chọn kích thước có sẵn hoặc nhập kích thước tuỳ chỉnh","options":["3 × 2 m (backdrop)","4 × 2,5 m (backdrop)","60 × 160 cm (standee cuốn)","80 × 200 cm (standee cuốn)","1200 × 628 px (quảng cáo Facebook)","1920 × 1080 px (ngang 16:9)"]},{"key":"headline","label":"Nội dung chữ chính","type":"SHORT_TEXT","required":true,"help":null,"options":[]},{"key":"details","label":"Thông tin khác","type":"LONG_TEXT","required":false,"help":"Nhà tài trợ, lịch chương trình, khách mời…","options":[]},{"key":"required_info","label":"Thông tin bắt buộc có","type":"MULTI_CHOICE","required":false,"help":null,"options":["Giá","Địa chỉ","Hotline","Website / Fanpage","Mã QR","Logo","Thời gian chương trình"]},{"key":"brand","label":"Màu sắc / thương hiệu","type":"SHORT_TEXT","required":false,"help":"Màu chủ đạo, font hoặc bộ nhận diện có sẵn","options":[]},{"key":"style","label":"Phong cách","type":"SINGLE_CHOICE","required":false,"help":null,"options":["Hiện đại, tối giản","Sang trọng","Trẻ trung, nhiều màu","Dễ thương","Truyền thống","Để designer đề xuất"]},{"key":"references","label":"Ảnh / logo tham khảo","type":"IMAGES","required":false,"help":"Logo, ảnh sản phẩm hoặc mẫu bạn thích — JPG, PNG, WebP","options":[]},{"key":"deadline","label":"Hạn mong muốn nhận file","type":"DATE","required":false,"help":null,"options":[]},{"key":"budget","label":"Ngân sách dự kiến (đồng)","type":"NUMBER","required":false,"help":"Không bắt buộc — giúp designer đề xuất phương án phù hợp","options":[]}]'::jsonb, true, 40, now(), now());
            INSERT INTO request_templates (id, name, description, design_type, service_id, fields, active, sort_order, created_at, updated_at) VALUES (gen_random_uuid(), 'Bài đăng mạng xã hội', 'Ảnh bài đăng Facebook, Instagram, TikTok, Zalo.', 'SOCIAL_MEDIA', NULL, '[{"key":"platform","label":"Nền tảng","type":"MULTI_CHOICE","required":true,"help":null,"options":["Facebook","Instagram","TikTok","Zalo","Khác"]},{"key":"post_type","label":"Dạng bài","type":"SINGLE_CHOICE","required":true,"help":null,"options":["Ảnh đơn","Album nhiều ảnh (carousel)","Story / Reels","Ảnh bìa (cover)"]},{"key":"size","label":"Kích thước","type":"SIZE","required":true,"help":"Chọn kích thước có sẵn hoặc nhập kích thước tuỳ chỉnh","options":["1080 × 1080 px (vuông)","1080 × 1350 px (dọc 4:5)","1080 × 1920 px (story/reels)","851 × 315 px (ảnh bìa Facebook)"]},{"key":"post_count","label":"Số lượng bài / ảnh","type":"NUMBER","required":true,"help":null,"options":[]},{"key":"headline","label":"Nội dung chữ chính","type":"SHORT_TEXT","required":false,"help":null,"options":[]},{"key":"details","label":"Nội dung / caption tham khảo","type":"LONG_TEXT","required":false,"help":null,"options":[]},{"key":"brand","label":"Màu sắc / thương hiệu","type":"SHORT_TEXT","required":false,"help":"Màu chủ đạo, font hoặc bộ nhận diện có sẵn","options":[]},{"key":"style","label":"Phong cách","type":"SINGLE_CHOICE","required":false,"help":null,"options":["Hiện đại, tối giản","Sang trọng","Trẻ trung, nhiều màu","Dễ thương","Truyền thống","Để designer đề xuất"]},{"key":"references","label":"Ảnh / logo tham khảo","type":"IMAGES","required":false,"help":"Logo, ảnh sản phẩm hoặc mẫu bạn thích — JPG, PNG, WebP","options":[]},{"key":"deadline","label":"Hạn mong muốn","type":"DATE","required":false,"help":null,"options":[]},{"key":"budget","label":"Ngân sách dự kiến (đồng)","type":"NUMBER","required":false,"help":"Không bắt buộc — giúp designer đề xuất phương án phù hợp","options":[]}]'::jsonb, true, 50, now(), now());
            INSERT INTO request_templates (id, name, description, design_type, service_id, fields, active, sort_order, created_at, updated_at) VALUES (gen_random_uuid(), 'Logo', 'Thiết kế logo mới hoặc làm mới logo cũ.', 'LOGO', NULL, '[{"key":"brand_name","label":"Tên thương hiệu (chữ trên logo)","type":"SHORT_TEXT","required":true,"help":null,"options":[]},{"key":"slogan","label":"Khẩu hiệu (nếu có)","type":"SHORT_TEXT","required":false,"help":null,"options":[]},{"key":"industry","label":"Ngành nghề / sản phẩm","type":"SHORT_TEXT","required":true,"help":null,"options":[]},{"key":"kind","label":"Loại logo","type":"SINGLE_CHOICE","required":false,"help":null,"options":["Chỉ chữ","Biểu tượng + chữ","Biểu tượng riêng","Để designer đề xuất"]},{"key":"colors","label":"Màu sắc mong muốn / không muốn","type":"SHORT_TEXT","required":false,"help":null,"options":[]},{"key":"style","label":"Phong cách","type":"SINGLE_CHOICE","required":false,"help":null,"options":["Hiện đại, tối giản","Sang trọng","Trẻ trung, nhiều màu","Dễ thương","Truyền thống","Để designer đề xuất"]},{"key":"usage","label":"Nơi sử dụng logo","type":"MULTI_CHOICE","required":false,"help":null,"options":["Biển hiệu","Bao bì / nhãn","Mạng xã hội","Website","Đồng phục","Danh thiếp"]},{"key":"references","label":"Logo hiện tại / mẫu tham khảo","type":"IMAGES","required":false,"help":"Logo, ảnh sản phẩm hoặc mẫu bạn thích — JPG, PNG, WebP","options":[]},{"key":"concept_count","label":"Số phương án mong muốn","type":"NUMBER","required":false,"help":null,"options":[]},{"key":"deadline","label":"Hạn mong muốn","type":"DATE","required":false,"help":null,"options":[]},{"key":"budget","label":"Ngân sách dự kiến (đồng)","type":"NUMBER","required":false,"help":"Không bắt buộc — giúp designer đề xuất phương án phù hợp","options":[]}]'::jsonb, true, 60, now(), now());
            """;
    }
}
