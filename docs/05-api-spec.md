# 05 — API Specification

Base URL: `/api`. JSON `camelCase`. Tiền: số nguyên VND. Ngày: `"YYYY-MM-DD"`. Thời điểm: ISO-8601 UTC.

## 1. Quy ước chung

### 1.1 Auth

| Nhóm | Prefix | Scheme | Cookie |
|---|---|---|---|
| Designer | `/api/admin/*` | `Designer` | `ff_admin` (HttpOnly, Secure, SameSite=Strict, 14 ngày trượt) |
| Portal | `/api/portal/*` | `Portal` | `ff_portal` (HttpOnly, Secure, SameSite=Lax, 30 ngày trượt) |
| Public | `/api/auth/*`, `/api/portal/session`, `/health` | — | — |

- Mọi request thay đổi dữ liệu (POST/PUT/PATCH/DELETE) phải kèm header `X-CSRF-Token` = giá trị
  cookie `ff_csrf` (double-submit; xem 07-security §3).

### 1.2 Lỗi — RFC 9457 ProblemDetails

```json
{ "type": "https://camcam/errors/INVALID_TRANSITION", "title": "Chuyển trạng thái không hợp lệ",
  "status": 409, "code": "INVALID_TRANSITION", "detail": "SENT → DRAFT", "errors": null }
```

| HTTP | code | Khi |
|---|---|---|
| 400 | `VALIDATION_FAILED` | Body không hợp lệ (`errors`: `{ field: [msg] }`) |
| 401 | `UNAUTHENTICATED` | Chưa đăng nhập / token portal hết hiệu lực |
| 403 | `FORBIDDEN` | Portal truy cập tài nguyên không thuộc khách |
| 404 | `NOT_FOUND` | Không tồn tại **hoặc** không thuộc khách (Portal luôn trả 404 thay vì 403 để không lộ tồn tại) |
| 409 | `INVALID_TRANSITION`, `CONCURRENCY_CONFLICT`, `QUOTATION_LOCKED`, `BRIEF_LOCKED`, `BILLING_LOCKED`, `HAS_ACTIVE_PROJECTS`, `DUPLICATE` | Vi phạm rule |
| 413 | `FILE_TOO_LARGE` | Upload vượt giới hạn |
| 415 | `UNSUPPORTED_FILE_TYPE` | Định dạng không cho phép |
| 422 | `BUSINESS_RULE` | Vi phạm rule khác (`detail` nêu BR-xx) |
| 428 | `SCOPE_GUARD` | BR-60: cần `overrideReason` |
| 429 | `RATE_LIMITED` | Vượt rate limit |

### 1.3 Phân trang & lọc

`GET ...?page=1&pageSize=20&sort=dueDate:asc&q=...` → `{ "items": [...], "page": 1, "pageSize": 20, "total": 134 }`.
`pageSize` tối đa 100.

### 1.4 Concurrency

Các resource có `rowVersion` (xmin, kiểu `uint`) trong response. Lệnh update/transition gửi lại
`rowVersion`; lệch → `409 CONCURRENCY_CONFLICT`.

## 2. Auth (designer)

| Method | Path | Body | Response |
|---|---|---|---|
| POST | `/api/auth/login` | `{ email, password }` | `204` + cookie; `401`; `429` khi lockout |
| POST | `/api/auth/logout` | — | `204` |
| GET | `/api/auth/me` | — | `{ email }` |
| POST | `/api/admin/account/password` | `{ currentPassword, newPassword }` | `204` (đổi security stamp) |

## 3. Admin API

### 3.1 Settings & Banks

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/settings` | |
| PUT | `/api/admin/settings` | `{ displayName, contactEmail, contactPhone, logoFileId, bankBin, bankAccountNo, bankAccountName, quotationPrefix, defaultValidityDays, defaultDepositPercent, defaultTerms }` |
| GET | `/api/admin/banks` | Danh sách ngân hàng active |

### 3.2 Services

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/services?includeInactive=false` | |
| POST | `/api/admin/services` | `{ name, description, basePrice, defaultIncludedRounds, extraRoundPrice }` |
| PUT | `/api/admin/services/{id}` | + `active` |

### 3.3 Clients

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/clients?q=&status=&page=` | Item: `{ id, name, company, phone, status, projectCount, acceptedTotal, outstanding, portalEnabled }` |
| POST | `/api/admin/clients` | `{ name, company, phone, email, contactChannel, notes }` |
| GET | `/api/admin/clients/{id}` | Chi tiết + thống kê |
| PUT | `/api/admin/clients/{id}` | + `status`, `rowVersion` |
| POST | `/api/admin/clients/{id}/archive` | `409 HAS_ACTIVE_PROJECTS` (BR-10) |
| POST | `/api/admin/clients/{id}/portal-link` | Tạo/tạo lại token → `{ url, createdAt }`. **URL chỉ trả về đúng lần này** |
| POST | `/api/admin/clients/{id}/portal/disable` | `portal_enabled = false` |
| POST | `/api/admin/clients/{id}/portal/enable` | Yêu cầu đã có token |
| GET | `/api/admin/clients/{id}/activity?page=` | |

> Vì chỉ lưu hash, designer muốn gửi lại link → bấm "Tạo link mới" (link cũ vô hiệu). UI cảnh báo điều này.

### 3.4 Projects

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/projects?status=&clientId=&q=&dueBefore=&page=&sort=` | |
| POST | `/api/admin/projects` | `{ clientId, name, designType, startDate, dueDate, internalNotes }` → tạo kèm brief v1 DRAFT rỗng |
| GET | `/api/admin/projects/{id}` | Overview: project + client + deliverables (với `usedRounds`, `remainingRounds`) + tổng tiền + counts |
| PUT | `/api/admin/projects/{id}` | `{ name, designType, startDate, dueDate, internalNotes, rowVersion }` |
| POST | `/api/admin/projects/{id}/status` | `{ to, reason?, reasonPublic?, confirmWarnings?: bool, rowVersion }` → `200` hoặc `409 INVALID_TRANSITION`; nếu có cảnh báo và `confirmWarnings=false` → `422 BUSINESS_RULE` kèm `warnings: [...]` |
| GET | `/api/admin/projects/{id}/activity?page=` | |

### 3.5 Briefs

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/projects/{id}/briefs` | Danh sách version (không content) |
| GET | `/api/admin/briefs/{briefId}` | |
| PUT | `/api/admin/briefs/{briefId}` | `{ content, rowVersion }` — `409 BRIEF_LOCKED` nếu CONFIRMED |
| POST | `/api/admin/briefs/{briefId}/confirm` | |
| POST | `/api/admin/projects/{id}/briefs` | Tạo version mới từ bản mới nhất; `409` nếu đã có DRAFT |

### 3.6 Deliverables

| Method | Path | Ghi chú |
|---|---|---|
| POST | `/api/admin/projects/{id}/deliverables` | `{ name, quantity, serviceId?, includedRounds?, extraRoundPrice?, dueDate }` (thiếu → lấy từ service; không service thì bắt buộc) |
| PUT | `/api/admin/deliverables/{id}` | `{ name, quantity, includedRounds, extraRoundPrice, dueDate, sortOrder }` — trả `warnings` nếu BR-44 |
| POST | `/api/admin/deliverables/{id}/status` | `{ to }` |

### 3.7 Requests

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/requests?status=NEW&projectId=&page=` | Item kèm `client`, `project`, `attachments[]` |
| GET | `/api/admin/requests/new-count` | `{ count }` cho badge |
| POST | `/api/admin/projects/{id}/requests` | Designer tạo hộ (multipart: `content`, `source`, `deliverableId?`, `files[]`) |
| GET | `/api/admin/requests/{id}` | |
| POST | `/api/admin/requests/{id}/triage` | xem 3.7.1 |
| POST | `/api/admin/requests/{id}/decline` | `{ reason }` |

#### 3.7.1 Triage body

```json
{
  "rounds": [
    {
      "deliverableId": "…",              // null = cấp dự án
      "receivedAt": "2026-11-07",
      "items": [
        { "description": "Đổi màu chữ sang xanh navy", "type": "MINOR", "billing": null },
        { "description": "Thêm tagline dưới logo",     "type": "MAJOR", "billing": "CHARGEABLE" }
      ]
    }
  ]
}
```

`billing: null` → server áp dụng gợi ý (BR 5.3). Response: các round đã tạo kèm `suggestedBilling`,
`suggestionReason` cho từng item.

Endpoint hỗ trợ UI xem trước gợi ý trước khi lưu:
`POST /api/admin/revisions/suggest-billing` `{ deliverableId, items:[{type}] }` → `[{ billing, reason }]`.

### 3.8 Revisions

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/projects/{id}/revisions` | Rounds (theo deliverable) + items + liên kết báo giá |
| POST | `/api/admin/projects/{id}/rounds` | Tạo round không qua request: `{ deliverableId, receivedAt, items[] }` |
| POST | `/api/admin/rounds/{roundId}/items` | `{ description, type, billing? }` |
| PUT | `/api/admin/revision-items/{id}` | `{ description, type, billing, internalNotes, rowVersion }` — `409 BILLING_LOCKED` (BR-61) |
| POST | `/api/admin/revision-items/{id}/work-status` | `{ to, overrideReason?, rowVersion }` — `428 SCOPE_GUARD` (BR-60) |
| POST | `/api/admin/revision-items/{id}/tasks` | Tạo task liên kết |

### 3.9 Tasks

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/tasks?projectId=&status=&due=overdue|today|week&page=` | |
| POST | `/api/admin/projects/{id}/tasks` | `{ title, description, priority, startDate, dueDate, deliverableId?, revisionItemId? }` |
| PUT | `/api/admin/tasks/{id}` | |
| POST | `/api/admin/tasks/{id}/move` | `{ status, sortOrder }` (Kanban) |
| DELETE | `/api/admin/tasks/{id}` | Xóa cứng (task không phải dữ liệu tài chính) + activity |
| POST/PUT/DELETE | `/api/admin/tasks/{id}/checklist[/{itemId}]` | |

### 3.10 Quotations

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/quotations?projectId=&status=&effectiveStatus=&page=` | Item có `effectiveStatus`, `outstanding` |
| GET | `/api/admin/quotations/{id}` | DRAFT: dữ liệu bảng; khác DRAFT: `snapshot` + trạng thái + `qrOptions` |
| POST | `/api/admin/projects/{id}/quotations` | INITIAL: `{ kind:"INITIAL", title, fromDeliverables: true }` → item theo deliverable (`quantity × service.base_price`) |
| POST | `/api/admin/projects/{id}/quotations/from-revisions` | ADDITIONAL: `{ title, revisionItemIds: [...] }` (BR-46) |
| PUT | `/api/admin/quotations/{id}` | DRAFT: `{ title, validityDays, discountAmount, vatPercent, depositPercent, terms, notesToClient, internalNotes, items:[{id?, description, details, quantity, unitPrice, sourceType, sourceId}], rowVersion }` — thay toàn bộ items; server tính tiền |
| DELETE | `/api/admin/quotations/{id}` | Chỉ DRAFT; gỡ liên kết `revision_items.quotation_item_id` |
| POST | `/api/admin/quotations/{id}/send` | BR-31/34 |
| POST | `/api/admin/quotations/{id}/new-version` | BR-33 → DRAFT mới |
| POST | `/api/admin/quotations/{id}/duplicate` | Nhân bản thành chain mới (DRAFT, number null) |
| POST | `/api/admin/quotations/{id}/accept` | `{ acceptedAt, note?, evidenceFileId?, rowVersion }` |
| POST | `/api/admin/quotations/{id}/reject` | `{ rejectedAt, reason, rowVersion }` |
| POST | `/api/admin/quotations/{id}/payment-status` | `{ to, date, reason?, rowVersion }` (reason bắt buộc khi hoàn tác) |
| GET | `/api/admin/quotations/{id}/qr/{purpose}` | `purpose ∈ deposit|remaining|full` → JSON `{ payload, amount, memo }` |
| GET | `/api/admin/quotations/{id}/qr/{purpose}.png` | PNG 512×512 |

`qrOptions` trong response:

```json
"qrOptions": [
  { "purpose": "deposit", "label": "Thanh toán cọc 50%", "amount": 1200000, "memo": "BG20260007 COC" },
  { "purpose": "full",    "label": "Thanh toán toàn bộ", "amount": 2400000, "memo": "BG20260007 TT" }
]
```

Liên kết revision ↔ báo giá qua các version (BR-47):
- `from-revisions` gán `revision_items.quotation_item_id` = dòng tương ứng của DRAFT.
- `new-version` copy dòng với `copied_from_item_id` = dòng cũ; liên kết vẫn trỏ dòng cũ.
- `send` version mới: mọi revision item trỏ dòng cũ được **chuyển** sang dòng có `copied_from_item_id`
  tương ứng; dòng cũ bị xóa khỏi version mới → item được gỡ liên kết (trở lại "CHARGEABLE chưa báo giá").

### 3.11 Files

| Method | Path | Ghi chú |
|---|---|---|
| POST | `/api/admin/projects/{id}/files` | multipart: `files[]`, `kind`, `deliverableId?`, `visibleToClient?` |
| GET | `/api/admin/projects/{id}/files?kind=&deliverableId=` | |
| PATCH | `/api/admin/files/{id}` | `{ visibleToClient, deliverableId, kind }` |
| DELETE | `/api/admin/files/{id}` | Xóa mềm; `409` nếu là evidence (BR-87) |
| GET | `/api/admin/files/{id}/content?download=0|1` | Stream; `Content-Disposition` inline/attachment |

### 3.12 Dashboard

`GET /api/admin/dashboard` →

```json
{
  "actionRequired": { "newRequests": 3, "overdueTasks": 2, "dueTodayTasks": 4, "awaitingQuotations": 2, "expiringSoonQuotations": 1 },
  "projects": { "inProgress": 6, "onHold": 1, "dueIn7Days": [ { "id": "…", "name": "…", "clientName": "…", "dueDate": "2026-11-10" } ] },
  "money": { "outstanding": 5400000, "acceptedThisMonth": 12500000, "additionalThisMonth": 1300000 },
  "acceptedByMonth": [ { "month": "2026-06", "total": 8000000 } ]
}
```

### 3.13 Endpoint bổ sung (Search, Flow, Images, Storage, Watermark, Notes)

Tất cả dưới `/api/admin/*`, cookie `Designer`; POST/PUT/PATCH/DELETE cần CSRF header.

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/search?q=` | Tìm nhanh header: `{ clients, projects, quotations }`, tối đa 5/nhóm; q 2–100 ký tự, không phân biệt hoa/thường/dấu; SĐT bỏ qua khoảng trắng, `.`, `-` |
| GET | `/api/admin/projects/{id}/flow` | Các bước luồng dự án (ProjectFlow) và bước hiện tại |
| GET | `/api/admin/flow/actions` | Danh sách việc cần làm tiếp theo trên mọi dự án |
| GET | `/api/admin/projects/{id}/start-preview` | Cảnh báo trước khi bắt đầu (vd chưa có báo giá gốc được chấp nhận) |
| POST | `/api/admin/projects/{id}/start` | DRAFT/QUOTED → IN_PROGRESS; cảnh báo không chặn |
| POST | `/api/admin/deliverables/{id}/submit-review` · `confirm-approval` | Gửi khách duyệt / designer xác nhận duyệt thay khách |
| POST | `/api/admin/projects/{projectId}/progress-updates` | Cập nhật tiến độ hiển thị cho khách |
| GET/POST | `/api/admin/tasks/{id}/notes` | Ghi chú task |
| PUT/DELETE | `/api/admin/tasks/{id}/notes/{noteId}` | |
| POST/GET | `/api/admin/tasks/{taskId}/images` | Upload (multipart) / liệt kê ảnh của task; chặn upload khi dung lượng ≥ 90% |
| GET | `/api/admin/projects/{projectId}/images` | Ảnh theo dự án |
| PATCH/DELETE | `/api/admin/images/{id}` | Sửa metadata / xóa |
| POST | `/api/admin/images/bulk-delete` | Xóa nhiều ảnh |
| GET | `/api/admin/images/{id}/{variant}` | Stream ảnh theo biến thể |
| GET | `/api/admin/storage/usage` | Dung lượng đã dùng / hạn mức |
| GET | `/api/admin/storage/images` | Liệt kê ảnh theo dung lượng để dọn |
| GET/PUT | `/api/admin/settings/watermark` | Cấu hình watermark ảnh (`enabled`, `text`, `opacityPercent`); policy `DesignerOnly` |

## 4. Portal API

Mọi endpoint lấy `clientId` **từ session**, không nhận `clientId` từ client.

| Method | Path | Ghi chú |
|---|---|---|
| POST | `/api/portal/session` | `{ token }` → `204` + cookie `ff_portal`; token sai/portal tắt → `401` (thông báo chung). Rate limit 10/phút/IP |
| POST | `/api/portal/logout` | |
| GET | `/api/portal/me` | `{ clientName, designer: { displayName, logoUrl, contactPhone, contactEmail } }` |
| GET | `/api/portal/projects` | `[{ id, name, designType, status, dueDate, deliverableSummary: { total, delivered }, hasOpenQuotation }]` |
| GET | `/api/portal/projects/{id}` | Project công khai: status (+ lý do nếu public), dates, deliverables (`status`, `includedRounds`, `usedRounds`, `remainingRounds`), brief CONFIRMED mới nhất |
| GET | `/api/portal/projects/{id}/revisions` | Rounds + items: `description, type, billing, workStatus` (không `internalNotes`) |
| GET | `/api/portal/projects/{id}/files` | Chỉ `visible_to_client = true`, không xóa |
| GET | `/api/portal/files/{id}/content` | Kiểm tra thuộc khách + visible (hoặc khách tự upload) |
| GET | `/api/portal/projects/{id}/timeline?page=` | Activity `visible_to_client = true`: `{ occurredAt, message }` |
| GET | `/api/portal/quotations?projectId=` | Status ≠ DRAFT, ≠ SUPERSEDED |
| GET | `/api/portal/quotations/{id}` | `snapshot` + `effectiveStatus` + `paymentStatus` + `qrOptions` + `versions: [{version, status}]` |
| GET | `/api/portal/quotations/{id}/versions/{version}` | Xem bản SUPERSEDED (lịch sử) |
| GET | `/api/portal/quotations/{id}/qr/{purpose}.png` | Tải ảnh QR |
| GET | `/api/portal/studio` | **Public** (`AllowAnonymous`, không cần session): `{ displayName, contactPhone, contactEmail }` — cho trang hết hạn/link lỗi. Rate limit 60/phút/IP (`RateLimit:PortalStudioPerMinute`) |
| GET | `/api/portal/projects/{id}/images` | Ảnh dự án được chia sẻ cho khách (scope theo `clientId`) |
| GET | `/api/portal/images/{id}/{variant}` | Stream ảnh; ngoài scope → 404 |
| POST | `/api/portal/deliverables/{id}/approve` | Khách duyệt hạng mục đang chờ duyệt; ngoài scope → 404 |
| GET | `/api/portal/requests?projectId=` | `[{ id, createdAt, content, status, declineReason, attachments }]` |
| POST | `/api/portal/projects/{id}/requests` | multipart `content`, `deliverableId?`, `files[]` (≤ 10, ≤ 20MB/file) — BR-53/54 |

Portal **không** có endpoint nào trả: tasks, internal_notes, services, settings ngân hàng ngoài snapshot,
dữ liệu khách khác.

## 5. Ví dụ DTO

### 5.1 `GET /api/admin/projects/{id}` (rút gọn)

```json
{
  "id": "0193…", "name": "Nhận diện ABC Coffee", "designType": "BRANDING", "status": "IN_PROGRESS",
  "startDate": "2026-11-01", "dueDate": "2026-11-30", "rowVersion": 18234,
  "client": { "id": "…", "name": "Nguyễn Văn A", "company": "ABC Coffee" },
  "deliverables": [
    { "id": "…", "name": "Logo", "quantity": 1, "status": "IN_REVIEW", "includedRounds": 3,
      "usedRounds": 3, "remainingRounds": 0, "extraRoundPrice": 200000, "dueDate": "2026-11-15" }
  ],
  "money": { "acceptedTotal": 2500000, "outstanding": 1250000, "pendingQuotationTotal": 200000 },
  "counts": { "newRequests": 1, "openTasks": 5, "overdueTasks": 1 }
}
```
