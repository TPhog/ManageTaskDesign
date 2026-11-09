# 08 — UI/UX Specification

Ngôn ngữ UI: tiếng Việt. Định dạng tiền `1.200.000 ₫`; ngày `07/11/2026`; giờ `14:30`.
Admin tối ưu desktop (≥ 1280px) nhưng dùng được trên tablet; Portal **mobile-first** (≥ 360px).

## 1. Thành phần dùng chung

| Component | Mô tả |
|---|---|
| `StatusBadge` | Tông semantic theo trạng thái (xem §5) |
| `MoneyText` | Định dạng VND, căn phải trong bảng |
| `MoneyInput` | Nhập số, tự chèn dấu chấm ngăn nghìn **ngay khi gõ** (`2.000.000`), lưu số nguyên; dán `2,500,000` hay `2.500.000` đều hiểu là nghìn; phần thập phân (`1,5`) bị bỏ qua vì VND là số nguyên; Backspace trên dấu chấm giữ nguyên vị trí con trỏ |
| `QuotaMeter` | `2/3 vòng` + thanh tiến độ; mực khi còn, vàng khi còn 1, đỏ khi hết |
| `BillingChip` | FREE_FIX "Sửa lỗi – miễn phí" · IN_QUOTA "Trong quota" · CHARGEABLE "Tính phí" |
| `FileUploader` | Kéo thả, hiển thị tiến độ, lỗi định dạng/kích thước |
| `FilePreview` | Lightbox ảnh, PDF viewer nhúng |
| `QrCard` | QR + ngân hàng, số TK (copy), tên TK, số tiền, nội dung CK (copy), nút tải PNG |
| `ConfirmDialog` | Dùng cho thao tác không hoàn tác (Gửi báo giá, Confirm brief, Tạo link mới) |
| `ReasonDialog` | Bắt buộc nhập lý do (ON_HOLD, hủy, scope override, hoàn tác thanh toán) |
| `WarningDialog` | Hiển thị `warnings[]` từ API, nút "Vẫn tiếp tục" gửi `confirmWarnings=true` |
| `EmptyState` | Mỗi danh sách rỗng có hướng dẫn + nút hành động chính |
| `Tile` / `TileHeader` (`components/Tile.tsx`) | Ô bento của admin; `tone` = `default` · `inverse` (ô mực) · `highlight` (ô cam son); `TileHeader` có tiêu đề, icon, meta, action |
| `StatTile` | Ô số liệu lớn, có thể là link (`to`), `tone`, `size` |
| `PageHeader` | Tiêu đề trang (font display) + `eyebrow`, mô tả, nút hành động |
| `dataTable` (`tableHeadRowClass`, `tableRowClass`) | Class dùng chung cho hàng đầu/hàng thân của bảng admin |
| Editorial kit (`features/portal/kit/`) | `Masthead`, `EditorialSection`, `Kicker`, `LeaderList`/`LeaderRow`/`LeaderTotal`, `NumberedList`/`NumberedListItem`, `Letter`, `Figure`, `PullQuote`, `StatusText`; `usePortalTheme` đặt `data-theme="editorial"` |

## 2. Admin

### 2.1 Bố cục

Sidebar kiểu Studio Bento: mục chính, rồi nhóm "Studio" (**Dịch vụ**, **Cài đặt**); mục đang mở là ô mực. Tổng quan là lưới bento: 12 cột ở ≥ 1280px, 6 cột ở 768–1279px, 1 cột dưới 768px.

Sidebar: **Tổng quan** · **Yêu cầu** (badge NEW) · **Khách mới** (badge NEW, P13) · **Dự án** · **Công việc** · **Báo giá** · **Khách hàng** ·
**Dịch vụ** · **Cài đặt**. Header: tìm kiếm nhanh (khách/dự án/số báo giá), nút "+ Tạo" (Dự án/Khách/Báo giá/Task).

### 2.2 Danh sách màn hình

| Route | Màn hình | Nội dung chính | Hành động |
|---|---|---|---|
| `/admin/login` | Đăng nhập | Email, mật khẩu | Đăng nhập |
| `/admin` | Tổng quan | 3 khối cũ (DSH-01..04): **Cần xử lý** (Việc cần làm), **Dự án**, **Tiền** + biểu đồ 6 tháng. P11 thêm các khối (region có tên): **Bắt đầu thiết lập** (checklist 6 mục, chỉ khi chưa xong/chưa ẩn), **Lịch làm việc 7 ngày** (nhóm **Quá hạn** nổi bật, mỗi ngày một nhóm, tick xong có Hoàn tác, tiêu đề mở TaskDrawer), **Sức khỏe dự án** (tiến độ, hạn, quota, huy hiệu rủi ro, tối đa 12 dòng), **Tiền đã thu và công nợ** (chọn tháng/quý/năm, so kỳ trước, công nợ có tuổi nợ, Copy nhắc thanh toán, mở báo giá và QR), **Hoạt động của khách**, **Dung lượng ảnh** (link tới Dung lượng). Ở 390px mọi bảng/thanh có bản chữ và không tràn ngang | Click từng chỉ số → danh sách đã lọc |
| `/admin/requests` | Hộp thư yêu cầu | Tab NEW / Đã xử lý / Từ chối; card: khách, dự án, nội dung, ảnh thumbnail, thời gian | Mở triage |
| `/admin/intake`, `/admin/intake/:id` | Khách mới (P13) | Tab Mới / Đã chuyển / Đã từ chối; mỗi dòng: tên, SĐT, tên mẫu, trích đoạn, số ảnh. Chi tiết (sheet): bảng câu trả lời theo mẫu, ghi chú, ảnh khách gửi, gợi ý khách trùng SĐT/email. Empty state có link "Mở Cài đặt" → `?tab=portal` để bật form | "Tạo khách & dự án" (dialog điền sẵn tên dự án "{mẫu} — {tên khách}", loại, dịch vụ, hạn; hoặc chọn khách có sẵn), "Từ chối" (lý do nội bộ), "Xoá"; thao tác bị đua → toast + tải lại (409) |
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
| `/admin/settings/request-templates`, `…/new`, `…/:id` | Mẫu yêu cầu (P13) | Danh sách mẫu (cả ẩn) có sắp xếp; trình sửa: thông tin mẫu (tên, mô tả, loại thiết kế, dịch vụ, hiện/ẩn), tối đa 30 câu hỏi (8 loại, bắt buộc, gợi ý, lựa chọn), "Xem trước như khách thấy" theo thời gian thực | Tạo, sửa, xoá, sắp xếp; link quay lại → Cài đặt `?tab=portal` |
| `/f/:slug` (công khai, không đăng nhập) | Form khách mới (P13) | Giao diện Editorial như Portal, mobile-first: I. "Bạn cần thiết kế gì?" (chọn mẫu hoặc "Yêu cầu khác"), II. câu hỏi của mẫu / mô tả, III. thông tin liên hệ (tên, SĐT/Zalo, email) + tối đa 3 ảnh; ô honeypot ẩn khỏi người dùng. Link tắt/sai → "Link không còn hoạt động". Hết hạn token (`FORM_EXPIRED`) → tự tải lại token, giữ nguyên câu trả lời. Kho ≥ 80 % → ẩn câu ảnh, 507 → nút "Gửi không kèm ảnh". Gửi xong: thẻ "Đã gửi yêu cầu" nhận focus + "Gửi yêu cầu khác" | Gửi yêu cầu |
| `/admin/services` | Dịch vụ | Bảng giá | CRUD, ẩn/hiện |
| `/admin/settings` | Cài đặt (P17) | 7 tab, mở bằng `?tab=`: **Studio** (`studio`: hồ sơ), **Thanh toán** (`payment`: ngân hàng + QR thử 10.000 ₫ - chỉ hiện sau khi đã lưu tài khoản), **Báo giá** (`quotation`: tiền tố, hiệu lực, cọc, điều khoản mặc định), **Khách hàng & Portal** (`portal`: thời gian phản hồi dự kiến, thẻ Mẫu yêu cầu, link form khách mới), **Ảnh & Dung lượng** (`images`: watermark, tổng quan dung lượng), **Thông báo** (`notifications`: nhắc việc hằng ngày + trạng thái Discord), **Tài khoản** (`account`: đổi mật khẩu). `?tab=` thiếu/sai mở Studio; đổi tab ghi vào URL bằng `replace`. Mỗi phần có form và nút Lưu riêng; nút Lưu mờ cho tới khi phần đó có thay đổi | Lưu từng phần (toast riêng, vd "Đã lưu tài khoản ngân hàng"); phím mũi tên chỉ di chuyển focus giữa các tab, Enter/Space/click mới mở tab; đổi tab hay rời trang khi còn thay đổi chưa lưu hỏi "Bỏ thay đổi chưa lưu?" (Bỏ thay đổi / Tiếp tục sửa) + `beforeunload` |

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

### 2.4 Bảng thuật ngữ & quy ước giao diện admin (Plan 14)

Một bộ từ cho cả giao diện (`frontend/src/lib/glossary.ts`; `glossary.test.ts` quét mọi `.tsx`/`.ts` tìm cụm bị cấm):

| Dùng | Không dùng |
|---|---|
| **hạng mục** (deliverable) | "deliverable" trong chữ hiển thị, "Thêm deliverable", "Chưa có deliverable" |
| **công việc** (task) | "task" trong chữ hiển thị mới |
| **vòng sửa**, **vòng sửa miễn phí** | "round", "Vòng thêm:", "Giá/vòng vượt" |
| **Giá vòng sửa thêm** | "Giá vòng thêm" |
| **tin nhắn thanh toán** (nhắn, không phải "nhắc") | "tin nhắc" |
| Không hiện mã quy tắc cho người dùng | "(BR-…)" trong giao diện |

- **Toast** nằm dưới-phải (cao hơn nút dính đáy: `offset.bottom = 88`, mobile 96), tối đa 3 toast cùng lúc.
- **Nhãn trạng thái dự án** "Sẵn sàng bắt đầu" (BR-143); ô hạn ở header không để dấu gạch lơ lửng khi thiếu ngày.
- **Kanban**: không hiện nhãn ưu tiên `Trung bình` (chỉ hiện Khẩn cấp/Cao/Thấp); cột "Bị chặn" nằm trọn trong khung ở 1440 px; mobile có lớp bố cục riêng.
- **Hộp xác nhận** cho Gửi duyệt / Xác nhận đã duyệt / Bàn giao (BR-139, BR-140); hộp "Bắt đầu dự án" có ô "Hạn chung · {hạng mục}" cho mỗi hạng mục (BR-144); chuyển trạng thái dự án có cảnh báo hiện qua hộp "Cần xác nhận" kèm "Xem công việc" khi còn việc chưa xong.
- **Tách yêu cầu thành vòng sửa**: yêu cầu có ≥ 2 dòng thì mỗi dòng điền sẵn một "Mô tả mục n" (sửa được); thiếu mô tả báo lỗi ngay tại ô.
- **Brief**: tab Brief có chế độ xem Markdown (ví dụ `## Mục tiêu` thành tiêu đề); ghi chú task cũng dùng Markdown.
- **Ctrl+K**: nhóm Khách, Dự án, Báo giá, **Công việc**, **Yêu cầu** (02 §14.1).
- **Cài đặt theo tab (P17):** PUT `/api/admin/settings` luôn gửi toàn bộ trường, nhưng giao diện dựng body từ bản cài đặt mới nhất trên server rồi chỉ ghi đè các trường của phần đang lưu (kèm `rowVersion`), nên lưu một phần không làm mất chữ đang gõ dở hay giá trị của phần khác (`logoFileId` được gửi lại nguyên vẹn; `defaultTerms` rỗng = xoá điều khoản mặc định, `''` ở các trường tuỳ chọn khác gửi `null`). `409` → "Cài đặt vừa được thay đổi ở nơi khác - đã tải lại" và phần đó tải lại; lỗi `errors.*` của API hiện dưới đúng ô. Liên kết sâu: link "Cài đặt" ở hộp tạo link portal → `?tab=studio`, trình soạn báo giá thiếu ngân hàng → `?tab=payment`, trang Mẫu yêu cầu → `?tab=portal`, trang Dung lượng → `?tab=images`, hộp Khách mới rỗng → `?tab=portal`, mục checklist thiết lập (02 §11.1, BR-96). Ở 390 px thanh tab cuộn ngang (có fade) và tab đang chọn luôn nằm trong khung.
- **Sao chép link portal**: tin nhắn mẫu kèm link, ghi vào clipboard ngay trong cú nhấp chuột kể cả khi phải gọi POST để tạo link (`copyWhenReady`).
- Admin ở **390 px** không cuộn ngang trên Tổng quan, Công việc, Dịch vụ, chi tiết dự án.

## 3. Portal (khách)

### 3.1 Bố cục

Theme `data-theme="editorial"` trên `<html>` (nền kem, chữ Fraunces + Public Sans, không bo góc/bóng). Masthead: tên studio chữ serif, kẻ đôi, "Xin chào, {khách}" + nút Thoát (nút liên hệ gọi/email). Bottom nav kẻ mực (mobile): **Dự án** · **Báo giá** · **Yêu cầu**; Thoát và 3 link nav cao ≥ 44px.

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

`StatusBadge` dùng **tông** semantic (không còn màu theo hue); mỗi tông là một cặp token trong `index.css`.

| Tông | Trạng thái |
|---|---|
| neutral | DRAFT · SUPERSEDED (gạch ngang) · UNPAID · PENDING · INACTIVE · NOT_STARTED · TODO |
| active | QUOTED · IN_PROGRESS · SENT |
| attention | NEW · IN_REVIEW · REVIEW |
| success | COMPLETED · ACCEPTED · PAID · TRIAGED · CONFIRMED · ACTIVE · APPROVED · DELIVERED · DONE |
| warning | ON_HOLD · EXPIRED · DEPOSIT_PAID |
| danger | CANCELLED · REJECTED · DECLINED · BLOCKED |

Billing: FREE_FIX neutral · IN_QUOTA success · CHARGEABLE warning.

Màu luôn đi kèm chữ (không chỉ dựa vào màu — a11y). Độ tương phản chữ ≥ 4.5:1.

## 6. Design system (P16)

Nguồn sự thật là `frontend/src/index.css`; ADR-013 ghi lý do. Tóm tắt từ code đã giao:

**Kiến trúc token 3 lớp.** (1) Primitive thô trong `:root` (`--paper-*`, `--ink-*`, `--stone-*`, `--vermilion-*`, `--cream-*`, `--sepia-*`, `--oxblood-700`, `--moss-700`, `--ochre-700`, xanh/vàng/đỏ trạng thái): component không bao giờ dùng trực tiếp. (2) Semantic (`--background`, `--foreground`, `--card`, `--primary`, `--muted-foreground`, `--highlight`, `--highlight-soft`, `--highlight-text`, `--success`/`--warning`/`--danger` và bản `-soft`, `--border`, `--border-strong`, `--rule`, `--input`, `--ring`, `--overlay`, `--font-body`/`--font-heading`, `--r-sm..xl`, `--elev-card`/`--elev-pop`): admin ở `:root`, portal ghi đè trong `[data-theme="editorial"]`. (3) Phạm vi component (`@utility tone-inverse` ô mực, `tone-highlight` ô cam son) trỏ lại token semantic trong ô để phần con tự thích nghi. `@theme inline` ánh xạ sang Tailwind (`bg-background`, `text-muted-foreground`, `font-display`, `text-title`, `text-kicker`...); shadcn dùng chung một bộ.

**Hai theme.** Studio Bento (admin): nền giấy `#f5f1ea`, mực `#1c1a17`, cam son `#ff5a36` làm nền ô nhấn, bo 8/12/14/20px, bóng nhẹ. Editorial (portal): nền kem `#f3eee3`, sepia `#1b1916`, đỏ son đậm (`oxblood-700`) làm nhấn/ring, không bo góc, không bóng thẻ. Dark mode ngoài phạm vi; sau này chỉ thêm một khối `[data-theme="dark"]` ghi đè lớp semantic.

**Chữ.** Admin: Bricolage Grotesque (tiêu đề) + Be Vietnam Pro (thân). Portal: Fraunces (tiêu đề, serif) + Public Sans (thân). Tự host bằng `@fontsource*` ghim đúng `5.3.0` (`@fontsource-variable/bricolage-grotesque`, `@fontsource-variable/fraunces`, `@fontsource-variable/public-sans`, `@fontsource/be-vietnam-pro`), chỉ nạp subset `latin` + `vietnamese`, `font-display: swap`. Lý do tự host: không request bên thứ ba (riêng tư cho khách, không phụ thuộc CDN) và có đủ dấu tiếng Việt. Plus Jakarta Sans đã bỏ. Thang chữ: `display-2xl` 3.5rem · `display-lg` 2rem · `display-md` 1.5625rem · `title` 1.0625rem · `kicker` 0.6875rem (hoa, giãn 0.12em); tiêu đề line-height ≥ 1.1 để không cắt dấu.

**Tương phản.** `src/test/designTokens.test.ts` đọc `index.css` và kiểm cặp chữ/nền ≥ 4.5:1, ô nhập và focus ring ≥ 3:1, ở cả hai theme, trong ô mực và ô cam son, và `::selection`. Quy tắc: chữ trên cam son luôn là mực (chữ sáng trên cam son chỉ 2–3:1); cam son không làm màu chữ (dùng `--highlight-text`); viền nhạt chỉ trang trí. Đừng viết `--token:` trong comment của `index.css` (regex của test bắt nhầm).

**Ánh xạ màu cũ sang token.** `bg-white` → `bg-card`; `bg-slate-50/100` → `bg-surface-sunk`/`bg-muted`; `text-slate-900` → `text-foreground`; `text-slate-500/600` → `text-muted-foreground`; `border-slate-200` → `border`; tím/xanh dương trạng thái → tông `active`/`attention`; xanh lá → `success`; cam/vàng → `warning`; đỏ → `danger`/`destructive`; overlay `bg-black/80` → `bg-overlay`.

**Palette guard.** `src/test/paletteGuard.ts` (`rawPaletteHits`) cấm class bảng màu Tailwind (`bg-red-50`), `*-white`/`*-black`, hex/`rgb()`/`hsl()`/`oklch()`... và `style` màu literal. Ngoại lệ phải ghi `palette-ok: <lý do>` trong comment cùng dòng (ví dụ mã QR phải đen trên trắng). `palette.all.test.ts` quét `features/admin/**`, `components/**`, `features/portal/**`, `features/public/**`; các `palette.*.test.ts` theo vùng được giữ để báo lỗi gọn.

**Editorial kit** (`features/portal/kit/`) là cách dựng trang portal/công khai: dùng `EditorialSection`, `LeaderRow` (dòng dẫn chấm), `NumberedList`, `Letter`... thay vì tự dựng card; `usePortalTheme` bật/tắt `data-theme="editorial"` theo vòng đời layout.

**Cài đặt theo tab (P17).** `/admin/settings?tab=` có 7 tab (`settingsTabs.ts`): studio · payment · quotation · portal · images · notifications · account; thiếu/sai mở `studio`. Tab dùng chung token, không có màu riêng.

**Thêm dark mode.** Thêm khối `[data-theme="dark"]` chỉ ghi đè lớp semantic (và `tone-*` nếu cần), thêm cặp vào `designTokens.test.ts`; không sửa component.
