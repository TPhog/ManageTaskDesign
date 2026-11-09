# 11 — Architecture Decision Records

Format: Bối cảnh → Quyết định → Hệ quả. Trạng thái tất cả: **Accepted** (2026-11-07).

## ADR-001 — Không xử lý thanh toán; dùng VietQR + đánh dấu thủ công

- **Bối cảnh:** App cá nhân; tích hợp cổng thanh toán tốn phí, cần pháp nhân, webhook, đối soát.
- **Quyết định:** Chỉ sinh VietQR từ báo giá; designer tự đánh dấu UNPAID/DEPOSIT_PAID/PAID. Không có bảng invoice/payment.
- **Hệ quả:** Đơn giản, không phí giao dịch. Không tự xác nhận tiền về; không thanh toán nhiều đợt tùy ý
  (chỉ cọc + phần còn lại). Muốn thêm milestone → ADR mới.

## ADR-002 — Single-user, không multi-tenant

- **Bối cảnh:** Chỉ một designer dùng.
- **Quyết định:** Không có `workspace_id/owner_id`, chỉ một bản ghi `designer_accounts`.
- **Hệ quả:** Bớt một lớp lọc mọi query. Chuyển sang SaaS sau này cần migration thêm `owner_id` + global query filter.

## ADR-003 — Quota tính theo round, gắn deliverable

- **Bối cảnh:** Thỏa thuận thực tế: "logo sửa 3 lần, banner 2 lần". Đếm theo item làm khách khó hiểu.
- **Quyết định:** Round thuộc ≤ 1 deliverable; round có ≥ 1 item IN_QUOTA (chưa hủy) trừ đúng 1 quota;
  FREE_FIX/CHARGEABLE không trừ. `used_rounds` tính toán, không lưu.
- **Hệ quả:** Request chạm nhiều deliverable phải tách round khi triage (UI hỗ trợ).

## ADR-004 — Khách không Accept/Reject trong app

- **Trạng thái:** thay một phần bởi ADR-012 (2026-11): khách có nút "Đồng ý báo giá" và "Tôi đã chuyển khoản" chỉ để **ghi nhận**; việc chấp nhận/từ chối và đổi trạng thái vẫn của designer, khách vẫn không có nút Từ chối.

- **Bối cảnh:** Designer muốn kiểm soát hoàn toàn; khách quen xác nhận qua Zalo.
- **Quyết định:** Portal chỉ đọc + gửi request. Designer đánh dấu ACCEPTED kèm ghi chú/bằng chứng (ảnh chụp tin nhắn).
- **Hệ quả:** Bằng chứng đồng ý nằm ngoài hệ thống (ảnh chụp). Có thể thêm nút "Tôi đồng ý" ở Phase sau mà không đổi schema lớn.

## ADR-005 — Magic link thay vì tài khoản khách

- **Bối cảnh:** Khách không muốn đăng ký; link gửi qua Zalo.
- **Quyết định:** Token 256-bit/khách, lưu hash, đổi sang cookie session, thu hồi bằng token version.
- **Hệ quả:** Ai có link là xem được dữ liệu của khách đó → designer cần thu hồi khi nghi lộ. Không có quyền theo từng người trong công ty khách.

## ADR-006 — Snapshot báo giá + khóa ở DB

- **Bối cảnh:** Financial integrity: báo giá đã gửi không được đổi âm thầm.
- **Quyết định:** `snapshot jsonb` khi send (gồm cả tài khoản ngân hàng); trigger chặn sửa các cột tiền/nội dung và items; sửa = version mới.
- **Hệ quả:** Hiển thị luôn từ snapshot; schema snapshot có `schemaVersion`.

## ADR-007 — Một API project, chia thư mục theo feature; không MediatR/Repository

- **Bối cảnh:** Spec gốc đề xuất Modular Monolith nhiều module; quy mô 1 user.
- **Quyết định:** Một project `FreelanceFlow.Api` với `Domain/`, `Features/`, `Data/`, `Infrastructure/`. DbContext dùng trực tiếp.
- **Hệ quả:** Ít boilerplate. Ranh giới giữ bằng quy ước thư mục + domain method; nếu cần tách module sau này vẫn khả thi.

## ADR-008 — Cookie auth (2 scheme) thay vì JWT

- **Bối cảnh:** SPA first-party cùng domain.
- **Quyết định:** Cookie HttpOnly cho Designer (SameSite=Strict) và Portal (SameSite=Lax), CSRF double-submit.
- **Hệ quả:** Không lưu token ở localStorage; frontend và API phải cùng origin (Caddy reverse proxy).

## ADR-009 — Trạng thái theo thời gian tính khi đọc

- **Bối cảnh:** Phase 1 không có background job.
- **Quyết định:** EXPIRED, quá hạn, sắp hết hạn được tính từ `IClock.TodayVn` khi query.
- **Hệ quả:** Không có thông báo chủ động khi hết hạn (Phase 2 với job).

## ADR-010 — Tiền `bigint` VND, làm tròn half-up

- **Quyết định:** Mọi số tiền là số nguyên đồng; phần trăm làm tròn `AwayFromZero`; tính ở backend.
- **Hệ quả:** Không lỗi số thực; không hỗ trợ ngoại tệ.

## ADR-011 — Gọi tài liệu là "Báo giá", không phải "Hóa đơn"

- **Bối cảnh:** "Hóa đơn" có nghĩa pháp lý (hóa đơn điện tử).
- **Quyết định:** Chỉ dùng "Báo giá"/"Đề nghị thanh toán".
- **Hệ quả:** App không thay thế hóa đơn điện tử nếu designer có nghĩa vụ xuất hóa đơn.

## ADR-012 — Khách ghi nhận "Đồng ý" và "Đã chuyển khoản" trên Portal

- **Trạng thái:** thay một phần ADR-004.
- **Bối cảnh:** QA CF-03: khách không biết đồng ý báo giá hay báo đã chuyển tiền ở đâu, nên quay lại nhắn Zalo; designer không có chỗ trong hệ thống để thấy điều đó.
- **Quyết định:** hai nút trên Portal chỉ ghi `client_accepted_at` / `client_payment_notice_*` + activity khách thấy được + Discord. Designer xác nhận bằng hành động có sẵn qua "Bước tiếp theo" (`CONFIRM_CLIENT_ACCEPTANCE` → chấp nhận như BR-31, `CONFIRM_CLIENT_PAYMENT` → đổi `payment_status` như BR-70..72). Hai nút idempotent; không đổi `status`/`payment_status`; app không xử lý hay xác minh tiền (BR-01). Trigger `fn_quotation_locked` whitelist đúng bốn cột mới.
- **Hệ quả:** bằng chứng khách đồng ý có trong hệ thống (thời điểm + ghi chú); designer vẫn phải kiểm tra tài khoản ngân hàng trước khi xác nhận; không có tự động đổi trạng thái nên không thể "chốt nhầm" do khách bấm.
