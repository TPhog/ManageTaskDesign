# 01 — Product Requirements Document (PRD)

## 1. Bối cảnh & vấn đề

Freelancer designer thường làm việc với nhiều khách qua Zalo/Messenger/email. Các vấn đề lặp lại:

1. Không nhớ khách đã sửa bao nhiêu vòng, vòng nào miễn phí, vòng nào phải tính tiền.
2. Yêu cầu ngoài phạm vi bị làm "free" vì không có bằng chứng phạm vi ban đầu.
3. Báo giá phát sinh rời rạc, khó theo dõi khách đã đồng ý cái gì, còn nợ bao nhiêu.
4. Khách liên tục hỏi "tới đâu rồi", "giá bao nhiêu", "chuyển khoản vào đâu".

## 2. Mục tiêu sản phẩm

| ID | Mục tiêu | Đo bằng |
|---|---|---|
| G1 | Designer quản lý toàn bộ khách/dự án/deliverable/task ở một nơi | Không cần sổ/Excel phụ |
| G2 | Đếm quota revision chính xác theo từng deliverable | Hệ thống tự gợi ý khi vượt quota |
| G3 | Mọi yêu cầu tính phí đều có báo giá phát sinh được lưu vết | 100% item CHARGEABLE gắn với quotation item |
| G4 | Báo giá đã gửi không bao giờ bị thay đổi âm thầm | Snapshot bất biến, sửa = version mới |
| G5 | Khách tự xem tiến độ, báo giá, quét QR chuyển khoản | Giảm câu hỏi lặp lại qua chat |
| G6 | Designer biết khoản nào đang chờ thu | Dashboard "Đang chờ thu" |

## 3. Người dùng & quyền

| Vai trò | Mô tả | Quyền |
|---|---|---|
| **Designer** | Chủ app, duy nhất 1 tài khoản | Toàn quyền tạo/sửa/xóa mềm mọi dữ liệu, quyết định mọi trạng thái |
| **Client** | Khách hàng của designer | Chỉ **xem** dữ liệu của chính mình và **tạo Client Request** |

Khách **không** có tài khoản/mật khẩu; truy cập bằng **magic link** do designer tạo (xem 07-security).

## 4. Phạm vi

### 4.1 Trong phạm vi Phase 1

Settings, Service Catalog, Client, Project, Brief (versioned), Deliverable, Client Request, Revision
(Round/Item, quota), Task (List + Kanban), Quotation (INITIAL/ADDITIONAL, version, snapshot, VietQR,
đánh dấu Accepted/Paid), Files (upload/preview/download), Client Portal, Activity Log, Dashboard.

### 4.2 Ngoài phạm vi

Thanh toán online, invoice, hóa đơn điện tử, khách Accept/Reject trong app, nhiều designer,
time tracking, pricing rule engine nhiều tầng, chat realtime.

### 4.3 Phase sau

- **Phase 2:** PDF báo giá (kèm QR), file versioning + watermark preview, thông báo email/Telegram khi có
  request mới, TOTP cho designer, nút copy/share link qua Zalo.
- **Phase 3:** Analytics (doanh thu theo khách, chi phí revision), comment trực tiếp trên ảnh.

## 5. Yêu cầu chức năng (Functional Requirements)

Ký hiệu ưu tiên: **M** = Must (Phase 1), **S** = Should (Phase 1 nếu kịp), **C** = Could (Phase 2+).

### 5.1 Settings (SET)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| SET-01 | Designer cấu hình hồ sơ hiển thị: tên/thương hiệu, email, SĐT, logo | M |
| SET-02 | Cấu hình tài khoản nhận tiền: ngân hàng (chọn từ danh sách BIN NAPAS), số TK, tên chủ TK | M |
| SET-03 | Điều khoản báo giá mặc định (text), số ngày hiệu lực báo giá mặc định (VD 7), % cọc mặc định (VD 50) | M |
| SET-04 | Tiền tố số báo giá (mặc định `BG`) | S |
| SET-05 | Đổi mật khẩu designer | M |

### 5.2 Service Catalog (SVC)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| SVC-01 | CRUD dịch vụ: tên, mô tả, `base_price`, `default_included_rounds`, `extra_round_price`, active | M |
| SVC-02 | Dịch vụ inactive không hiển thị khi tạo deliverable/báo giá mới, nhưng dữ liệu cũ giữ nguyên | M |
| SVC-03 | Sửa giá dịch vụ **không** ảnh hưởng deliverable/báo giá đã tạo | M |

### 5.3 Client (CLI)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| CLI-01 | CRUD khách: tên, công ty, SĐT, email, kênh liên hệ (Zalo/FB...), ghi chú, trạng thái Active/Inactive | M |
| CLI-02 | Trang chi tiết khách: danh sách dự án, tổng giá trị đã chốt, đang chờ thu, timeline hoạt động | M |
| CLI-03 | Tạo/xem lại/thu hồi/tạo mới magic link Portal; bật/tắt Portal | M |
| CLI-04 | Tìm kiếm khách theo tên/công ty/SĐT | M |
| CLI-05 | Xóa mềm (archive) khách; không cho xóa nếu có báo giá ACCEPTED | M |

### 5.4 Project (PRJ)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| PRJ-01 | Tạo dự án cho 1 khách: tên, loại thiết kế, ngày bắt đầu, deadline, ghi chú nội bộ | M |
| PRJ-02 | Chuyển trạng thái theo state machine (02-business-rules §3) — thủ công bởi designer | M |
| PRJ-03 | Trang chi tiết có tab: Overview, Brief, Deliverables, Requests & Revisions, Tasks, Quotations, Files, Activity | M |
| PRJ-04 | Overview hiển thị: trạng thái, deadline, tiến độ deliverable, quota còn lại, tổng đã chốt, đang chờ thu | M |
| PRJ-05 | Danh sách dự án lọc theo trạng thái/khách, sắp xếp theo deadline | M |
| PRJ-06 | Khi chuyển ON_HOLD bắt buộc nhập lý do (khách thấy được nếu designer chọn công khai) | M |

### 5.5 Brief (BRF)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| BRF-01 | Brief có version; version DRAFT sửa được; designer bấm "Confirm" để khóa | M |
| BRF-02 | Brief CONFIRMED không sửa được; muốn đổi phải tạo version mới (copy từ bản mới nhất) | M |
| BRF-03 | Xem lịch sử version, so sánh 2 version (text diff) | S |
| BRF-04 | Portal chỉ hiển thị version CONFIRMED mới nhất | M |
| BRF-05 | Nội dung brief: rich text (Markdown) + file đính kèm tham khảo | M |

### 5.6 Deliverable (DLV)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| DLV-01 | Thêm deliverable vào dự án: tên, số lượng, dịch vụ (tùy chọn), `included_rounds` (mặc định từ dịch vụ, sửa được), `extra_round_price` (mặc định từ dịch vụ, sửa được), deadline | M |
| DLV-02 | Trạng thái: NOT_STARTED → IN_PROGRESS → IN_REVIEW → APPROVED → DELIVERED (+CANCELLED) | M |
| DLV-03 | Hiển thị `used_rounds / included_rounds`, cảnh báo khi còn 1 và khi hết | M |
| DLV-04 | Sắp xếp thứ tự hiển thị | C |

### 5.7 Client Request (REQ)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| REQ-01 | Khách gửi request từ Portal: chọn dự án, (tùy chọn) deliverable, nội dung, tối đa 10 file đính kèm | M |
| REQ-02 | Designer cũng có thể tự tạo request thay khách (nhận qua Zalo/email) và đánh dấu nguồn | M |
| REQ-03 | Hộp thư request: lọc NEW/TRIAGED/DECLINED, badge số NEW trên menu | M |
| REQ-04 | Triage: tách request thành 1..n Revision Round (mỗi round 1 deliverable hoặc cấp dự án) và các Item | M |
| REQ-05 | Decline kèm lý do; khách thấy trạng thái + lý do | M |
| REQ-06 | Khách xem danh sách request của mình và trạng thái | M |

### 5.8 Revision (REV)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| REV-01 | Round gắn với 1 deliverable (hoặc null = cấp dự án); `round_no` tăng tuần tự trong deliverable | M |
| REV-02 | Item: mô tả, type (MINOR/MAJOR/SCOPE_CHANGE/NEW_DELIVERABLE), billing (FREE_FIX/IN_QUOTA/CHARGEABLE), work status | M |
| REV-03 | Hệ thống **gợi ý** billing theo quota (02 §4); designer có quyền đổi | M |
| REV-04 | Từ các item CHARGEABLE chưa báo giá → "Tạo báo giá phát sinh" (DRAFT) với dòng giá gợi ý | M |
| REV-05 | Bắt đầu làm (IN_PROGRESS) item CHARGEABLE khi báo giá chưa ACCEPTED → cảnh báo, bắt buộc nhập lý do, ghi activity | M |
| REV-06 | Tạo Task từ Item (liên kết 2 chiều) | S |
| REV-07 | Portal hiển thị round/item (mô tả, trạng thái, miễn phí hay tính phí) — không hiển thị ghi chú nội bộ | M |

### 5.9 Task (TSK)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| TSK-01 | CRUD task: tiêu đề, mô tả, trạng thái, ưu tiên, deadline, deliverable (tùy chọn), revision item (tùy chọn) | M |
| TSK-02 | Checklist trong task | S |
| TSK-03 | View List (lọc/sort) và Kanban (kéo thả đổi trạng thái) | M |
| TSK-04 | View "Tất cả task" xuyên dự án, lọc quá hạn/hôm nay/tuần này | M |
| TSK-05 | Task chỉ nội bộ, **không** hiển thị trên Portal | M |
| TSK-06 | Calendar view | C |

### 5.10 Quotation (QUO)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| QUO-01 | Tạo báo giá INITIAL (từ deliverables của dự án) hoặc ADDITIONAL (từ revision item) | M |
| QUO-02 | Dòng báo giá: mô tả, số lượng, đơn giá, thành tiền; thêm/sửa/xóa/sắp xếp khi DRAFT | M |
| QUO-03 | Giảm giá (số tiền), VAT % (tùy chọn), % cọc, ngày hiệu lực, điều khoản | M |
| QUO-04 | "Gửi" (DRAFT → SENT): sinh số báo giá, chụp snapshot, khóa nội dung, hiện trên Portal | M |
| QUO-05 | Sửa báo giá SENT → tạo version mới (DRAFT, cùng số); khi version mới được gửi, version cũ → SUPERSEDED | M |
| QUO-06 | Designer đánh dấu ACCEPTED (kèm ngày, ghi chú, file bằng chứng tùy chọn) hoặc REJECTED (kèm lý do) | M |
| QUO-07 | EXPIRED hiển thị tự động khi quá hạn mà vẫn SENT; designer có thể "gia hạn" = tạo version mới | M |
| QUO-08 | Hiển thị VietQR: Cọc / Phần còn lại / Toàn bộ theo `payment_status` (06-vietqr-spec) | M |
| QUO-09 | Đánh dấu `payment_status`: UNPAID → DEPOSIT_PAID → PAID (và hoàn tác 1 bước, có log) | M |
| QUO-10 | Portal hiển thị báo giá SENT/ACCEPTED/REJECTED/EXPIRED (không DRAFT, không SUPERSEDED trừ khi xem lịch sử) | M |
| QUO-11 | Xuất PDF | C |
| QUO-12 | Nhân bản báo giá thành DRAFT mới | S |

### 5.11 Files (FIL)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| FIL-01 | Upload file vào dự án (kind: REFERENCE/PREVIEW/FINAL), tùy chọn gắn deliverable | M |
| FIL-02 | Cờ `visible_to_client`; mặc định PREVIEW = true, FINAL = false, REFERENCE = true | M |
| FIL-03 | Xem trước ảnh (jpg/png/webp/gif) và PDF trong trình duyệt | M |
| FIL-04 | Giới hạn: 20 MB/file, whitelist định dạng (07-security §5) | M |
| FIL-05 | Version file & watermark | C |

### 5.12 Client Portal (POR)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| POR-01 | Mở magic link → vào trang tổng quan các dự án của khách | M |
| POR-02 | Chi tiết dự án: trạng thái, deadline, deliverable + quota còn lại, brief đã confirm, file được chia sẻ, timeline công khai | M |
| POR-03 | Danh sách & chi tiết báo giá (SENT/ACCEPTED/REJECTED/EXPIRED), QR chuyển khoản, tải ảnh QR | M |
| POR-04 | Gửi request mới + xem danh sách request đã gửi | M |
| POR-05 | Responsive, ưu tiên mobile (khách mở từ Zalo) | M |
| POR-06 | Không hiển thị: task, ghi chú nội bộ, báo giá DRAFT, catalog, dữ liệu khách khác | M |

### 5.13 Activity Log (ACT)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| ACT-01 | Ghi log mọi thao tác thay đổi trạng thái, tiền, quota, portal token, xóa mềm | M |
| ACT-02 | Mỗi log: actor, action, entity, thời điểm UTC, before/after (khi phù hợp), `visible_to_client`, `client_message` | M |
| ACT-03 | Log không sửa/xóa được qua app | M |
| ACT-04 | Timeline theo dự án và theo khách | M |

### 5.14 Dashboard (DSH)

| ID | Yêu cầu | Ưu tiên |
|---|---|---|
| DSH-01 | Cần xử lý: request NEW, task quá hạn, task đến hạn hôm nay, báo giá SENT chờ phản hồi, báo giá hết hạn trong 3 ngày | M |
| DSH-02 | Dự án: đang làm, ON_HOLD, deadline trong 7 ngày | M |
| DSH-03 | Tiền: Đang chờ thu; Giá trị đã chốt tháng này; Doanh thu phát sinh tháng này (02 §7) | M |
| DSH-04 | Biểu đồ giá trị đã chốt 6 tháng gần nhất | S |

## 6. Yêu cầu phi chức năng (NFR)

| ID | Yêu cầu |
|---|---|
| NFR-01 | **Tính toàn vẹn tài chính:** báo giá đã SENT bất biến; tiền lưu `bigint` VND; mọi phép tính ở backend |
| NFR-02 | **Audit:** mọi thay đổi trạng thái/tiền có activity log, không sửa được |
| NFR-03 | **Cô lập dữ liệu khách:** mọi truy vấn Portal lọc theo `client_id` từ session; test tự động bắt buộc |
| NFR-04 | **Hiệu năng:** p95 API < 300 ms với ≤ 500 khách, ≤ 5.000 dự án trên VPS 2 vCPU/2 GB |
| NFR-05 | **Thời gian:** lưu UTC (`timestamptz`), hiển thị `Asia/Ho_Chi_Minh`; ngày (date) không có giờ dùng kiểu `date` |
| NFR-06 | **Sẵn sàng/Backup:** backup PostgreSQL hằng ngày (pg_dump) + thư mục file; giữ 14 bản; có script restore |
| NFR-07 | **Bảo mật:** HTTPS bắt buộc; cookie HttpOnly/Secure/SameSite; rate limit login & portal; xem 07 |
| NFR-08 | **Ngôn ngữ:** UI tiếng Việt; code, DB, API tiếng Anh |
| NFR-09 | **Trình duyệt:** Chrome/Edge/Safari/Firefox 2 phiên bản gần nhất; Portal tối ưu mobile ≥ 360px |
| NFR-10 | **Khả năng bảo trì:** coverage domain logic ≥ 80%; integration test với PostgreSQL thật (Testcontainers) |
| NFR-11 | **Xóa:** chỉ xóa mềm cho Client/Project/Quotation/File; Activity log không bao giờ xóa |
| NFR-12 | **Dữ liệu cá nhân:** không lưu IP/user-agent của khách ngoài log truy cập web server (xoay vòng 30 ngày) |

## 7. Giả định & ràng buộc

- Một designer, một ngôn ngữ (vi), một tiền tệ (VND).
- Triển khai tự host trên 1 VPS bằng Docker Compose.
- Khách đồng ý báo giá qua kênh ngoài (Zalo/email); designer ghi nhận trong app.
- Designer tự đối soát chuyển khoản trên app ngân hàng của mình.
