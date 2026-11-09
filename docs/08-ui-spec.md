# 08 — UI/UX Specification

Ngôn ngữ UI: tiếng Việt. Định dạng tiền `1.200.000 ₫`; ngày `07/11/2026`; giờ `14:30`.
Admin tối ưu desktop (≥ 1280px) nhưng dùng được trên tablet; Portal **mobile-first** (≥ 360px).

## 1. Thành phần dùng chung

| Component | Mô tả |
|---|---|
| `StatusBadge` | Màu theo trạng thái (xem §5) |
| `MoneyText` | Định dạng VND, căn phải trong bảng |
| `MoneyInput` | Nhập số, tự chèn dấu chấm, lưu số nguyên |
| `QuotaMeter` | `2/3 vòng` + thanh tiến độ; vàng khi còn 1, đỏ khi hết |
| `BillingChip` | FREE_FIX "Sửa lỗi – miễn phí" · IN_QUOTA "Trong quota" · CHARGEABLE "Tính phí" |
| `FileUploader` | Kéo thả, hiển thị tiến độ, lỗi định dạng/kích thước |
| `FilePreview` | Lightbox ảnh, PDF viewer nhúng |
| `QrCard` | QR + ngân hàng, số TK (copy), tên TK, số tiền, nội dung CK (copy), nút tải PNG |
| `ConfirmDialog` | Dùng cho thao tác không hoàn tác (Gửi báo giá, Confirm brief, Tạo link mới) |
| `ReasonDialog` | Bắt buộc nhập lý do (ON_HOLD, hủy, scope override, hoàn tác thanh toán) |
| `WarningDialog` | Hiển thị `warnings[]` từ API, nút "Vẫn tiếp tục" gửi `confirmWarnings=true` |
| `EmptyState` | Mỗi danh sách rỗng có hướng dẫn + nút hành động chính |

## 2. Admin

### 2.1 Bố cục

Sidebar: **Tổng quan** · **Yêu cầu** (badge NEW) · **Dự án** · **Công việc** · **Báo giá** · **Khách hàng** ·
**Dịch vụ** · **Cài đặt**. Header: tìm kiếm nhanh (khách/dự án/số báo giá), nút "+ Tạo" (Dự án/Khách/Báo giá/Task).

### 2.2 Danh sách màn hình

| Route | Màn hình | Nội dung chính | Hành động |
|---|---|---|---|
| `/admin/login` | Đăng nhập | Email, mật khẩu | Đăng nhập |
| `/admin` | Tổng quan | 3 khối cũ (DSH-01..04): **Cần xử lý** (Việc cần làm), **Dự án**, **Tiền** + biểu đồ 6 tháng. P11 thêm các khối (region có tên): **Bắt đầu thiết lập** (checklist 6 mục, chỉ khi chưa xong/chưa ẩn), **Lịch làm việc 7 ngày** (nhóm **Quá hạn** nổi bật, mỗi ngày một nhóm, tick xong có Hoàn tác, tiêu đề mở TaskDrawer), **Sức khỏe dự án** (tiến độ, hạn, quota, huy hiệu rủi ro, tối đa 12 dòng), **Tiền đã thu và công nợ** (chọn tháng/quý/năm, so kỳ trước, công nợ có tuổi nợ, Copy nhắc thanh toán, mở báo giá và QR), **Hoạt động của khách**, **Dung lượng ảnh** (link tới Dung lượng). Ở 390px mọi bảng/thanh có bản chữ và không tràn ngang | Click từng chỉ số → danh sách đã lọc |
| `/admin/requests` | Hộp thư yêu cầu | Tab NEW / Đã xử lý / Từ chối; card: khách, dự án, nội dung, ảnh thumbnail, thời gian | Mở triage |
| `/admin/requests/:id` | Triage | Trái: nội dung + file gốc. Phải: trình tạo round — chọn deliverable, thêm item (mô tả, type), hiển thị **billing gợi ý + lý do**, cho đổi | "Lưu & tạo round", "Từ chối", sau khi lưu: "Tạo báo giá phát sinh" nếu có CHARGEABLE |
| `/admin/projects` | Danh sách dự án | Bảng: tên, khách, loại, trạng thái, deadline (đỏ nếu quá), tiến độ deliverable, chờ thu | Lọc, sort, tạo |
| `/admin/projects/:id` | Chi tiết dự án | Header: tên, khách, `StatusBadge`, nút đổi trạng thái, link "Xem như khách" | Tabs ↓ |
| `…/overview` | Overview | Thẻ deliverable với `QuotaMeter`, tiền (đã chốt, chờ thu, đang báo giá), task sắp tới, activity gần đây | |
| `…/brief` | Brief | Chọn version; DRAFT: editor Markdown + preview; CONFIRMED: chỉ đọc + nhãn "Đã xác nhận dd/MM" | Lưu, Confirm, Tạo version mới, So sánh |
| `…/deliverables` | Deliverables | Bảng: tên, SL, dịch vụ, quota, giá/vòng vượt, trạng thái, deadline | Thêm (chọn service → tự điền), sửa, đổi trạng thái |
| `…/revisions` | Yêu cầu & Revision | Nhóm theo deliverable → round (số vòng, ngày, request nguồn) → items (mô tả, type, `BillingChip`, work status, link báo giá + trạng thái báo giá) | Thêm round thủ công, đổi work status (scope guard dialog), chọn nhiều item CHARGEABLE → "Tạo báo giá phát sinh" |
| `…/tasks` | Task | Toggle List/Kanban; cột TODO, IN_PROGRESS, REVIEW, DONE, BLOCKED | Kéo thả, tạo nhanh, mở drawer chi tiết (checklist) |
| `…/quotations` | Báo giá | Danh sách theo chain: số, version, loại, tổng, effectiveStatus, paymentStatus | Tạo INITIAL, mở |
| `…/files` | File | Lưới thumbnail, lọc theo kind/deliverable, icon mắt = visible_to_client | Upload, bật/tắt chia sẻ, xóa |
| `…/activity` | Hoạt động | Timeline, icon 👁 cho mục khách thấy được | |
| `/admin/quotations/:id` | Trình soạn / xem báo giá | **DRAFT**: form header + bảng dòng (thêm/xóa/kéo sắp xếp), tổng tính trực tiếp (server là chuẩn), panel phải "Xem trước như khách". **Khác DRAFT**: render từ snapshot, khung trạng thái, `QrCard` theo BR-75, lịch sử version | DRAFT: Lưu, Xóa, **Gửi** (ConfirmDialog nêu rõ "sẽ khóa nội dung"). SENT: Đánh dấu đã chấp nhận (ngày, ghi chú, upload bằng chứng), Từ chối, Tạo version mới. ACCEPTED: Đã nhận cọc / Đã nhận đủ / Hoàn tác |
| `/admin/tasks` | Tất cả task | Bảng xuyên dự án; bộ lọc nhanh: Quá hạn, Hôm nay, Tuần này | |
| `/admin/quotations` | Tất cả báo giá | Lọc trạng thái hiệu lực, thanh toán, khách | |
| `/admin/clients` | Khách hàng | Bảng: tên, công ty, SĐT, số dự án, đã chốt, chờ thu, portal on/off | Tạo, tìm |
| `/admin/clients/:id` | Chi tiết khách | Thông tin, dự án, tiền, timeline; khối **Portal**: trạng thái, ngày tạo link | **Tạo link** → dialog hiển thị URL + nút Copy + cảnh báo "Link chỉ hiển thị 1 lần; tạo link mới sẽ vô hiệu link cũ"; Bật/Tắt portal |
| `/admin/services` | Dịch vụ | Bảng giá | CRUD, ẩn/hiện |
| `/admin/settings` | Cài đặt | Hồ sơ, ngân hàng (select tìm kiếm theo tên/BIN) + **QR thử** 10.000 ₫ để designer tự quét kiểm tra, mặc định báo giá, **Nhắc việc hằng ngày** (bật/tắt, giờ gửi giờ VN, cảnh báo khi chưa có Discord webhook), đổi mật khẩu | Lưu |

### 2.3 Luồng triage → báo giá (chi tiết tương tác)

1. Mở request NEW → chọn deliverable (gợi ý theo `deliverableId` khách chọn).
2. Nhập từng item; mỗi khi đổi type, UI gọi `suggest-billing` và hiển thị chip gợi ý + lý do
   (VD "Logo đã dùng 3/3 vòng → Tính phí 200.000 ₫/vòng").
3. Có thể thêm round thứ 2 cho deliverable khác trong cùng request.
4. "Lưu" → toast + nếu có item CHARGEABLE hiện banner "3 mục tính phí chưa báo giá — Tạo báo giá phát sinh".
5. "Tạo báo giá phát sinh" → dialog xem trước từng dòng (BR-46): "Hạng mục mới" có ô chọn dịch vụ mẫu (tìm được, điền tên +
   giá gốc) và ô đơn giá; dòng vòng sửa / đổi phạm vi có ô đơn giá điền sẵn giá gợi ý; dòng còn 0 ₫ được nêu tên.
   "Tạo báo giá nháp" → trình soạn → designer chỉnh (mỗi dòng có "Chọn từ dịch vụ") → Gửi. Còn dòng 0 ₫: hộp xác nhận liệt kê
   tên dòng, nút "Vẫn gửi". "Đánh dấu đã chấp nhận" liệt kê các hạng mục mới sẽ thêm vào tab Deliverables (BR-48).
   Dialog tạo báo giá ban đầu: deliverable chưa có dịch vụ mẫu có ô đơn giá ngay trong dialog.
6. Sau khi Gửi: dialog "Đã gửi. Sao chép link portal / tin nhắn mẫu" với template:
   `"Chào anh/chị {tên}, em gửi báo giá {số} ({tổng}). Anh/chị xem và quét QR tại: {link portal}"`
   (link portal chỉ có nếu designer vừa tạo; nếu không, nhắc "khách mở link portal cũ để xem").

## 3. Portal (khách)

### 3.1 Bố cục

Header: logo + tên designer, nút liên hệ (gọi/email). Bottom nav (mobile): **Dự án** · **Báo giá** · **Yêu cầu**.

### 3.2 Màn hình

| Route | Màn hình | Nội dung | Hành động |
|---|---|---|---|
| `/p/:token` | Đổi token | Spinner → chuyển `/portal`; lỗi → trang "Liên kết không hợp lệ, vui lòng liên hệ {designer}" | |
| `/portal` | Dự án của tôi | Card mỗi dự án: tên, trạng thái, deadline, "2/5 hạng mục đã bàn giao", chip "Có báo giá mới" | Mở dự án |
| `/portal/projects/:id` | Chi tiết dự án | Trạng thái (+ lý do tạm dừng nếu công khai); **Hạng mục**: tên, trạng thái, "Còn 1/3 lần sửa miễn phí"; **Brief** đã xác nhận (thu gọn); **File** chia sẻ (lưới, xem/tải); **Yêu cầu chỉnh sửa**: round/item, chip Miễn phí/Tính phí, trạng thái; **Tiến trình** (timeline) | "Gửi yêu cầu chỉnh sửa" (nút nổi) |
| `/portal/quotations` | Báo giá | Danh sách: số, dự án, tổng, trạng thái hiệu lực, trạng thái thanh toán | Mở |
| `/portal/quotations/:id` | Chi tiết báo giá | Render từ snapshot: thông tin designer/khách, bảng dòng, tổng, cọc, điều khoản, hiệu lực đến; **QrCard** theo BR-75 (tab Cọc / Toàn bộ / Còn lại); badge "Đã hết hạn" nếu EXPIRED (vẫn hiện QR + ghi chú "Vui lòng liên hệ để xác nhận lại"); dòng hướng dẫn "Sau khi đồng ý/chuyển khoản, vui lòng nhắn cho {designer}" | Tải QR, copy STK/nội dung, xem version cũ |
| `/portal/requests` | Yêu cầu của tôi | Danh sách + trạng thái (Mới / Đã tiếp nhận / Từ chối + lý do) | Tạo mới |
| `/portal/requests/new` | Gửi yêu cầu | Chọn dự án (mặc định dự án hiện tại), hạng mục (tùy chọn), nội dung (textarea, gợi ý "Mỗi ý một dòng"), đính kèm ảnh (camera/thư viện) | Gửi → màn hình cảm ơn |

### 3.3 Nguyên tắc nội dung Portal

- Không dùng thuật ngữ nội bộ (bảng thuật ngữ Portal ở §3.4; "designer", "deliverable", "round", "brief" không xuất hiện).
- Mọi số tiền là từ snapshot báo giá.
- Khách **không** có nút Từ chối. "Đồng ý báo giá" và "Tôi đã chuyển khoản" chỉ ghi nhận lời khách, nhà thiết kế xác nhận (ADR-012, thay một phần ADR-004).

### 3.4 Portal vòng 2 (Plan 12)

- **Trang chủ** (`/portal`): lời chào theo tên khách, khối "Việc bạn cần làm" (liên kết lớn theo thứ tự: "Xem và đồng ý báo giá …", "Chuyển tiền cọc … (số báo giá)" / "Chuyển phần còn lại …", "Duyệt N sản phẩm của …"), rồi danh sách dự án; dự án có sản phẩm chờ duyệt lên đầu. Tải lỗi hiện "Không tải được …" + nút Thử lại (không giả làm "không có việc gì"). Khi studio bật form khách mới (BR-133), dưới danh sách có "Đặt thiết kế mới" (`designer.intakeUrl`).
- **Sản phẩm**: một badge duy nhất theo `reviewState` (BR-97): "Chờ bạn duyệt" / "Bạn đã duyệt — chờ xác nhận" / "Đang sửa theo yêu cầu của bạn"; đang sửa thì ẩn nút Duyệt và Yêu cầu sửa. Ảnh cần duyệt nằm ngay trong thẻ sản phẩm; bấm ảnh mở lightbox toàn màn hình (100dvh, nút đóng và điều khiển ≥ 44 px, zoom bằng pinch / chạm đúp một lần, vuốt đổi ảnh).
- **Báo giá**: nút "Đồng ý báo giá" (hộp xác nhận, nút ≥ 44 px) → dòng "Bạn đã đồng ý lúc … — đang chờ nhà thiết kế xác nhận"; khối thanh toán ở docs/06 §6.1 với "Tôi đã chuyển khoản" (hộp có ghi chú tuỳ chọn) → "Bạn đã báo chuyển …".
- **Yêu cầu mới / Yêu cầu sửa**: cùng một form (`PortalRequestForm`): nội dung, ảnh minh hoạ (≤ 5), gửi xong hiện tóm tắt "Đã gửi yêu cầu" + "Nhà thiết kế thường phản hồi {responseTime}" + câu về lượt miễn phí còn lại ("Nếu yêu cầu này dùng lượt miễn phí, bạn còn N lượt chỉnh sửa miễn phí" hoặc "Nhà thiết kế sẽ báo giá trước khi sửa …"). Trang "Gửi yêu cầu mới" có thêm bộ chọn mẫu (BR-130..132). Ô "Dự án" chỉ hiện khi khách có hơn một dự án đang mở.
- **Không có nút nổi** che nội dung; "Thoát" có hộp xác nhận. Ở 390 px không cuộn ngang, mọi nút bấm chính ≥ 44 px.

| Thuật ngữ nội bộ | Khách thấy |
|---|---|
| Deliverable / hạng mục | **Sản phẩm** |
| Revision round / lần sửa | **Lượt chỉnh sửa** ("Còn 2 lượt chỉnh sửa miễn phí") |
| FREE_FIX / IN_QUOTA / CHARGEABLE | "Miễn phí (lỗi của nhà thiết kế)" / "Dùng lượt miễn phí" / "Tính phí" |
| Timeline / tiến trình | **Hoạt động gần đây** |
| IN_REVIEW (chưa duyệt) | "Chờ bạn duyệt" |
| Quotation `SENT` / `ACCEPTED` / `REJECTED` / `SUPERSEDED` | "Chờ bạn đồng ý" / "Đã chốt" / "Không tiếp tục" / "Phiên bản cũ" |
| Request `NEW` / `TRIAGED` / `DECLINED` | "Đã gửi — chờ nhà thiết kế xem" / "Nhà thiết kế đã nhận, đang xử lý" / "Nhà thiết kế không nhận yêu cầu này" |
| Tải ảnh QR | **Lưu ảnh QR** |
| Gửi yêu cầu chỉnh sửa | **Gửi yêu cầu mới** |
| Hạng mục (tuỳ chọn) trong form | "Sản phẩm cần sửa (tuỳ chọn)" |

## 4. Trạng thái rỗng / lỗi / loading

- Skeleton cho danh sách và chi tiết; không spinner toàn trang trừ đổi token portal.
- Lỗi mạng: toast + nút thử lại. `409 CONCURRENCY_CONFLICT`: dialog "Dữ liệu đã thay đổi ở tab khác — Tải lại".
- `428 SCOPE_GUARD`: mở `ReasonDialog` với nội dung "Mục này tính phí nhưng báo giá chưa được chấp nhận".

## 5. Màu trạng thái

| Nhóm | Trạng thái → màu |
|---|---|
| Project | DRAFT xám · QUOTED tím · IN_PROGRESS xanh dương · ON_HOLD cam · COMPLETED xanh lá · CANCELLED đỏ nhạt |
| Quotation | DRAFT xám · SENT xanh dương · ACCEPTED xanh lá · REJECTED đỏ · EXPIRED cam · SUPERSEDED xám gạch ngang |
| Payment | UNPAID xám · DEPOSIT_PAID vàng · PAID xanh lá |
| Request | NEW xanh dương · TRIAGED xanh lá · DECLINED đỏ |
| Billing | FREE_FIX xám · IN_QUOTA xanh lá · CHARGEABLE cam |

Màu luôn đi kèm chữ (không chỉ dựa vào màu — a11y). Độ tương phản chữ ≥ 4.5:1.
