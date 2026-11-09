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
| PUT | `/api/admin/settings` | `{ displayName, contactEmail, contactPhone, logoFileId, bankBin, bankAccountNo, bankAccountName, quotationPrefix, defaultValidityDays, defaultDepositPercent, defaultTerms, portalResponseTime }`; `portalResponseTime` ≤ 60 ký tự, trống → "trong 24 giờ", quá dài → `400` (`errors.portalResponseTime`). GET trả thêm `portalResponseTime` |
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
| POST | `/api/admin/projects/{id}/status` | `{ to, reason?, reasonPublic?, confirmWarnings?: bool, rowVersion }` → `200` hoặc `409 INVALID_TRANSITION`; nếu có cảnh báo và `confirmWarnings=false` → `422 BUSINESS_RULE` kèm `warnings: [...]`. Với `to = COMPLETED`, `warnings` còn nêu công việc chưa xong và mục sửa chưa xong (BR-142) |
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
| POST | `/api/admin/deliverables/{id}/status` | `{ to, deliveryUrl?, clientMessage? }` - hai trường sau chỉ đọc khi `to = DELIVERED` (BR-140): `deliveryUrl` http/https ≤ 500, `clientMessage` ≤ 500; sai → `400` (`errors.deliveryUrl` / `errors.clientMessage`) |

### 3.7 Requests

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/requests?status=NEW&projectId=&page=` | Item kèm `client`, `project`, `attachments[]` |
| GET | `/api/admin/requests/new-count` | `{ count }` cho badge |
| POST | `/api/admin/projects/{id}/requests` | Designer tạo hộ (multipart: `content`, `source`, `deliverableId?`, `files[]`) |
| GET | `/api/admin/requests/{id}` | Request có thêm `templateId`, `templateName`, `answers: [{ key, label, type, display }]` (P13; rỗng nếu gửi tự do) |
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
| GET | `/api/admin/projects/{id}/revisions` | Rounds (theo deliverable) + items + liên kết báo giá; item có `serviceId`, `serviceName`, `createdDeliverableId`, `createdDeliverableName` |
| POST | `/api/admin/projects/{id}/rounds` | Tạo round không qua request: `{ deliverableId, receivedAt, items[] }` |
| POST | `/api/admin/rounds/{roundId}/items` | `{ description, type, billing? }` |
| PUT | `/api/admin/revision-items/{id}` | `{ description, type, billing, internalNotes, rowVersion }` — `409 BILLING_LOCKED` (BR-61; cũng khi đổi sang/khỏi NEW_DELIVERABLE trên dòng SENT/ACCEPTED, BR-48); `422 BUSINESS_RULE` nếu item đã thành deliverable |
| POST | `/api/admin/revision-items/{id}/work-status` | `{ to, overrideReason?, rowVersion }` — `428 SCOPE_GUARD` (BR-60) |
| POST | `/api/admin/revision-items/{id}/tasks` | Tạo task liên kết |

### 3.9 Tasks

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/tasks?projectId=&status=&due=overdue|today|week&page=` | |
| POST | `/api/admin/projects/{id}/tasks` | `{ title, description, priority, startDate, dueDate, deliverableId?, revisionItemId? }` - `dueDate < startDate` → `400` `errors.dueDate` (BR-141) |
| PUT | `/api/admin/tasks/{id}` | Cùng ràng buộc ngày như POST |
| POST | `/api/admin/tasks/{id}/move` | `{ status, sortOrder }` (Kanban) |
| DELETE | `/api/admin/tasks/{id}` | Xóa cứng (task không phải dữ liệu tài chính) + activity; ảnh của task bị xóa theo (BR-88), trừ ảnh chứng từ báo giá (giữ lại, đếm trong `task.deleted`) |
| POST/PUT/DELETE | `/api/admin/tasks/{id}/checklist[/{itemId}]` | |

### 3.10 Quotations

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/quotations?projectId=&status=&effectiveStatus=&page=` | Item có `effectiveStatus`, `outstanding` |
| GET | `/api/admin/quotations/{id}` | DRAFT: dữ liệu bảng; khác DRAFT: `snapshot` + trạng thái + `qrOptions` + `clientAcceptedAt` + `clientPaymentNotice` (P12, BR-76/77); kèm `newDeliverables: [{ revisionItemId, quotationItemId, lineDescription, serviceId, serviceName, createdDeliverableId, createdDeliverableName }]` |
| POST | `/api/admin/projects/{id}/quotations` | INITIAL: `{ kind:"INITIAL", title, fromDeliverables: true, deliverableIds?, deliverablePrices?: [{ deliverableId, unitPrice }] }` → item theo deliverable (`quantity × (giá nhập → service.base_price → 0)`); `400 errors.deliverablePrices` (âm / > 100 tỷ / deliverable ngoài tập / `fromDeliverables=false`) |
| POST | `/api/admin/projects/{id}/quotations/from-revisions/preview` | `{ revisionItemIds }` → `{ lines: [{ kind: ROUND/NEW_DELIVERABLE/SCOPE_CHANGE, revisionItemIds, description, details, suggestedUnitPrice }] }`; không ghi DB |
| POST | `/api/admin/projects/{id}/quotations/from-revisions` | ADDITIONAL: `{ title, revisionItemIds, newDeliverableServices?: [{ revisionItemId, serviceId?, unitPrice? }], linePrices?: [{ revisionItemId, unitPrice }] }` (BR-46); `400 errors.newDeliverableServices` / `errors.linePrices` (giá âm / > 100 tỷ, `linePrices` cho NEW_DELIVERABLE hoặc item ngoài danh sách, 2 giá khác nhau cho 1 vòng, service không tồn tại) |
| PUT | `/api/admin/quotations/{id}` | DRAFT: `{ title, validityDays, discountAmount, vatPercent, depositPercent, terms, notesToClient, internalNotes, items:[{id?, description, details, quantity, unitPrice, sourceType, sourceId}], rowVersion }` — thay toàn bộ items; server tính tiền |
| DELETE | `/api/admin/quotations/{id}` | Chỉ DRAFT; gỡ liên kết `revision_items.quotation_item_id` |
| POST | `/api/admin/quotations/{id}/send` | BR-31/34 |
| POST | `/api/admin/quotations/{id}/new-version` | BR-33 → DRAFT mới |
| POST | `/api/admin/quotations/{id}/duplicate` | Nhân bản thành chain mới (DRAFT, number null) |
| POST | `/api/admin/quotations/{id}/accept` | `{ acceptedAt, note?, evidenceFileId?, rowVersion }` — BR-48: tạo deliverable từ item NEW_DELIVERABLE của báo giá (cùng transaction; theo trạng thái dự án; CANCELLED không tạo, activity ghi `newDeliverablesSkipped`) |
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
- `from-revisions` ghi `revision_items.service_id` cho item NEW_DELIVERABLE; `accept` ghi `revision_items.created_deliverable_id` (BR-48).

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

Các endpoint dashboard bổ sung (P11) đều là `GET` thuần đọc, cookie `Designer`, projection `AsNoTracking`; chỉ `setup/dismiss` ghi dữ liệu.

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/dashboard/schedule` | Lịch 7 ngày (BR-91) |
| GET | `/api/admin/dashboard/project-health` | Sức khỏe dự án (BR-92) |
| GET | `/api/admin/dashboard/money?period=month\|quarter\|year` | Tiền đã thu và công nợ (BR-93); giá trị khác → `400` ProblemDetails `errors.period` |
| GET | `/api/admin/dashboard/client-activity?limit=10` | Hoạt động của khách; `limit` kẹp 1..30 |
| GET | `/api/admin/dashboard/setup` | Checklist thiết lập (BR-96) |
| POST | `/api/admin/dashboard/setup/dismiss` | Ẩn checklist; idempotent; `204`; ghi activity `settings.setup_dismissed` lần đầu; cần CSRF |

`GET /api/admin/dashboard/schedule` →

```json
{
  "today": "2026-11-09",
  "undatedOpenTasks": 4,
  "overdue": { "total": 12, "items": [ { "id": "…", "title": "Chốt màu", "status": "TODO", "priority": "HIGH", "dueDate": "2026-11-07", "sortOrder": 1024.0, "projectId": "…", "projectName": "Logo Cà phê", "clientName": "Gió" } ] },
  "days": [ { "date": "2026-11-09", "tasks": [ /* như overdue.items, tối đa 20 */ ], "hiddenTasks": 0,
              "deadlines": [ { "kind": "PROJECT", "id": "…", "projectId": "…", "name": "Logo Cà phê", "projectName": "Logo Cà phê", "clientName": "Gió", "date": "2026-11-09" } ] } ]
}
```

`overdue.items` tối đa 10, cũ nhất trước; `days` luôn đủ 7 phần tử; `deadlines[].kind` là `PROJECT` hoặc `DELIVERABLE`.

`GET /api/admin/dashboard/project-health` chấm điểm mọi dự án trong phạm vi BR-92, sắp theo rủi ro rồi cắt tối đa 12 dòng; `total` là số dự án trong phạm vi, `hidden = total − số dòng trả về`:

```json
{
  "total": 15, "hidden": 3,
  "items": [ { "projectId": "…", "projectName": "…", "clientId": "…", "clientName": "…", "status": "IN_PROGRESS", "stage": "EXECUTE",
               "dueDate": "2026-11-05", "daysToDeadline": -4, "progressPercent": 40, "quotaUsed": 2, "quotaTotal": 4,
               "risks": ["LATE", "TASKS_OVERDUE"], "riskScore": 6 } ]
}
```

`GET /api/admin/dashboard/money?period=month` →

```json
{
  "period": "month", "periodStart": "2026-11-01", "periodEnd": "2026-11-30",
  "current": { "start": "2026-11-01", "endExclusive": "2026-11-10" },
  "previous": { "start": "2026-10-01", "endExclusive": "2026-10-10" },
  "collected": { "current": 6000000, "previous": 4000000, "deltaPercent": 50.0, "currentDeposits": 4000000, "currentRemainders": 2000000 },
  "accepted": { "current": 12500000, "previous": 9000000, "deltaPercent": 38.9 },
  "receivables": { "total": 5400000, "hiddenItems": 0,
    "items": [ { "quotationId": "…", "number": "BG-2026-0007", "version": 1, "kind": "INITIAL", "clientId": "…", "clientName": "…", "projectId": "…", "projectName": "…",
                 "paymentStatus": "DEPOSIT_PAID", "total": 8000000, "depositPercent": 50, "depositAmount": 4000000, "outstanding": 4000000,
                 "agingFrom": "2026-10-20", "agingDays": 20, "agingBucket": "8-30" } ],
    "byClient": [ { "clientId": "…", "clientName": "…", "outstanding": 4000000, "count": 1 } ] }
}
```

`deltaPercent` là `null` khi kỳ trước bằng 0. `receivables.items` tối đa 20, `byClient` tối đa 10; `receivables.total` bằng `money.outstanding` của `GET /api/admin/dashboard`.

`GET /api/admin/dashboard/client-activity` chỉ gồm `REQUEST_CREATED`, `DELIVERABLE_APPROVED` (do khách thực hiện) và `QUOTATION_ACCEPTED`; `href` do server dựng:

```json
{ "items": [ { "id": 981, "occurredAt": "2026-11-09T02:10:00+00:00", "kind": "REQUEST_CREATED", "text": "…", "clientName": "…", "projectName": "…", "href": "/admin/requests/…" } ] }
```

Endpoint này dùng một số truy vấn cố định, nhỏ (feed, dự án, khách), không phụ thuộc số dòng trả về; chỉ mục `ix_activity_occurred` phục vụ sắp xếp.

`GET /api/admin/dashboard/setup` →

```json
{ "dismissed": false, "complete": false, "doneCount": 2, "total": 6,
  "items": [ { "key": "STUDIO", "label": "Điền thông tin studio", "hint": "…", "done": true, "href": "/admin/settings" } ] }
```

`items[].key` ∈ `STUDIO, BANK, SERVICE, TASK_TEMPLATE, WATERMARK, CLIENT` (xem BR-96).

### 3.13 Endpoint bổ sung (Search, Flow, Images, Storage, Watermark, Notes)

Tất cả dưới `/api/admin/*`, cookie `Designer`; POST/PUT/PATCH/DELETE cần CSRF header.

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/search?q=` | Tìm nhanh header: `{ clients, projects, quotations, tasks, requests }`, tối đa 5/nhóm (02 §14.1: `tasks[] { id, projectId, title, projectName, status }`, `requests[] { id, projectId, projectName, clientName, excerpt, status }`); q 2–100 ký tự, không phân biệt hoa/thường/dấu; SĐT bỏ qua khoảng trắng, `.`, `-` |
| GET | `/api/admin/projects/{id}/flow` | Các bước luồng dự án (ProjectFlow) và bước hiện tại. Action kinds P12: `CONFIRM_CLIENT_ACCEPTANCE` (báo giá `SENT` đã có `client_accepted_at`; khi đó không còn `RECORD_ACCEPTANCE`), `CONFIRM_CLIENT_PAYMENT` (có `clientPaymentNotice.pending`); hai kind này đứng đầu danh sách. Warning P12: `REVIEW_WITH_OPEN_REVISIONS`. `CONFIRM_APPROVAL` chỉ khi `reviewState = CLIENT_APPROVED` (BR-97). Action kinds P14: `MARK_QUOTED`, `RECORD_DEPOSIT`, `VIEW_TASKS`; warning `COMPLETED_WITH_OPEN_ITEMS` (info); không có `CREATE_QUOTATION` khi dự án chưa có hạng mục (BR-143); `COPY_PAYMENT_REMINDER` có nhãn "Sao chép tin nhắn thanh toán" |
| GET | `/api/admin/flow/actions` | Danh sách việc cần làm tiếp theo trên mọi dự án |
| GET | `/api/admin/projects/{id}/start-preview` | Cảnh báo trước khi bắt đầu (vd chưa có báo giá gốc được chấp nhận) + từng hạng mục `{ deliverableId, name, serviceName, titles[], suggestedDueDate }` (BR-144) |
| POST | `/api/admin/projects/{id}/start` | `{ rowVersion, tasks?: [{ deliverableId, titles[], dueDate? }] }` - DRAFT/QUOTED → IN_PROGRESS; cảnh báo không chặn; `dueDate` là hạn chung cho mọi công việc tạo từ hạng mục đó (BR-144) → `{ projectId, status, startedDeliverables, createdTasks, warnings[] }` |
| POST | `/api/admin/deliverables/{id}/submit-review` · `confirm-approval` | Gửi khách duyệt (`{ clientMessage? ≤ 500 }`, lời nhắn hiện cho khách) / designer xác nhận duyệt thay khách |
| POST | `/api/admin/projects/{projectId}/progress-updates` | Cập nhật tiến độ hiển thị cho khách |
| GET/POST | `/api/admin/tasks/{id}/notes` | Ghi chú task |
| PUT/DELETE | `/api/admin/tasks/{id}/notes/{noteId}` | |
| POST/GET | `/api/admin/tasks/{taskId}/images` | Upload (multipart) / liệt kê ảnh của task; chặn upload khi dung lượng ≥ 90%; response upload có `duplicateOf` (`{ id, ownerType, ownerId, taskTitle, createdAt, sameUpload }` khi trùng SHA-256 trong dự án — vẫn lưu; `null` nếu không trùng); upload share-lock dòng task |
| GET | `/api/admin/projects/{projectId}/images` | Ảnh theo dự án |
| PATCH/DELETE | `/api/admin/images/{id}` | Sửa metadata / xóa |
| POST | `/api/admin/images/bulk-delete` | Xóa nhiều ảnh |
| GET | `/api/admin/images/{id}/{variant}` | Stream ảnh theo biến thể; ETag theo object thật sự gửi là validator duy nhất (không `Last-Modified`). `Cache-Control`: preview/thumb `private, max-age=31536000, immutable`; original `private, max-age=600` (original đã dọn → trả preview; preview của ảnh khách → original) |
| GET | `/api/admin/storage/usage` | Dung lượng đã dùng / hạn mức |
| GET | `/api/admin/storage/images` | Liệt kê ảnh theo dung lượng để dọn; item có `originalDeletedAt` |
| GET | `/api/admin/storage/projects` | Dung lượng theo dự án (sắp `totalBytes` giảm dần): `projectId, projectName, clientName, projectStatus, completedAt, imageCount, totalBytes, originalBytes, originalsDeleted` |
| GET | `/api/admin/storage/original-cleanup?olderThanMonths=` | Xem trước xóa bản gốc (BR-89): `olderThanMonths, cutoff, projectCount, imageCount, freedBytes, digest, projects[]`; N 1–120 |
| POST | `/api/admin/storage/original-cleanup` | `{ olderThanMonths, expectedImageCount, expectedFreedBytes, expectedDigest }` → `{ projectCount, imageCount, freedBytes }`; `409 CONCURRENCY_CONFLICT` nếu số ảnh/MB/digest khác bản xem trước hoặc có dòng không còn đủ điều kiện |
| GET/PUT | `/api/admin/settings/digest` | Nhắc việc hằng ngày (BR-94): GET trả `{ enabled, hourVn, channelConfigured }`; PUT nhận `{ enabled, hourVn }`, `hourVn` ngoài 0..23 → `400` (`errors.hourVn`); `channelConfigured` chỉ cho biết đã có Discord webhook hay chưa, không trả URL; ghi activity `settings.digest_updated`; policy `DesignerOnly` |
| GET/PUT | `/api/admin/settings/watermark` | Cấu hình watermark ảnh (`enabled`, `text`, `opacityPercent`); policy `DesignerOnly` |

### 3.14 Mẫu yêu cầu & khách mới (Plan 13)

Tất cả dưới `/api/admin/*`, cookie `Designer`, CSRF cho ghi.

**Mẫu yêu cầu** (BR-130)

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/request-templates` | Mọi mẫu (cả ẩn), theo `sortOrder`: `AdminRequestTemplate { id, name, description, designType, serviceId, serviceName, active, sortOrder, fields: TemplateField[], rowVersion, createdAt, updatedAt }` |
| GET | `/api/admin/request-templates/{id}` | |
| POST | `/api/admin/request-templates` | `{ name, description?, designType, serviceId?, active?, fields }` → `201`; > 50 mẫu / > 30 câu / mã câu trùng-sai dạng / lựa chọn thiếu → `400` theo trường |
| PUT | `/api/admin/request-templates/{id}` | như POST + `rowVersion`; lệch → `409 CONCURRENCY_CONFLICT` |
| DELETE | `/api/admin/request-templates/{id}` | Xoá cứng; yêu cầu/hồ sơ cũ giữ snapshot |
| POST | `/api/admin/request-templates/reorder` | `{ ids: Guid[] }` - thứ tự mới |

```ts
type TemplateField = {
  key: string                       // ^[a-z0-9_]{1,40}$, duy nhất trong mẫu
  label: string                     // ≤ 120
  type: 'SHORT_TEXT' | 'LONG_TEXT' | 'SINGLE_CHOICE' | 'MULTI_CHOICE' | 'SIZE' | 'DATE' | 'NUMBER' | 'IMAGES'
  required: boolean
  help: string | null               // ≤ 300
  options: string[]                 // SINGLE/MULTI_CHOICE: 1-20; SIZE: 0-20 kích thước có sẵn
}
```

**Form khách mới** (BR-133)

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/settings/intake` | `{ enabled, slug, url }` (`url` = `{App:PublicBaseUrl}/f/{slug}`; null khi chưa từng bật) |
| PUT | `/api/admin/settings/intake` | `{ enabled }` - lần bật đầu tạo slug; activity `settings.intake_updated` (không ghi slug) |
| POST | `/api/admin/settings/intake/regenerate` | Slug mới, link cũ và biểu mẫu đang mở vô hiệu; activity `settings.intake_link_regenerated` |

**Hộp "Khách mới"** (BR-134..137)

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/admin/intake?status=NEW&page=&pageSize=` | `status` ∈ `NEW` (mặc định) / `CONVERTED` / `REJECTED`; item `{ id, createdAt, status, contactName, contactPhone, contactEmail, templateName, excerpt, imageCount }` |
| GET | `/api/admin/intake/new-count` | `{ count }` cho badge |
| GET | `/api/admin/intake/{id}` | `IntakeDetail`: item + `answers`, `note`, `body`, `images[] { id, width, height, sizeBytes, thumbUrl, previewUrl, originalUrl }`, gợi ý (`suggestedDesignType`, `suggestedServiceId/Name`, `suggestedDueDate`), `matchingClients[]` (trùng 9 số cuối SĐT hoặc email), `rejectReason`, `convertedClientId/ProjectId`, `rowVersion` |
| POST | `/api/admin/intake/{id}/convert` | `{ existingClientId? \| clientName, projectName, designType, dueDate?, serviceId?, deliverableName?, rowVersion }` → `200 { clientId, projectId, deliverableId }`. `400` theo trường; `404` khách có sẵn không còn ACTIVE; `409` hồ sơ không còn NEW / `CONCURRENCY_CONFLICT` |
| POST | `/api/admin/intake/{id}/reject` | `{ reason?(≤500), rowVersion }` → `200 IntakeDetail`; `409` như trên |
| DELETE | `/api/admin/intake/{id}` | `204`; ảnh xoá mềm; hồ sơ CONVERTED hoặc vừa bị chuyển đổi đồng thời → `409` |

## 4. Portal API

Mọi endpoint lấy `clientId` **từ session**, không nhận `clientId` từ client.

| Method | Path | Ghi chú |
|---|---|---|
| POST | `/api/portal/session` | `{ token }` → `204` + cookie `ff_portal`; token sai/portal tắt → `401` (thông báo chung). Rate limit 10/phút/IP |
| POST | `/api/portal/logout` | |
| GET | `/api/portal/me` | `{ clientName, designer: { displayName, logoUrl, contactPhone, contactEmail, responseTime, intakeUrl } }` — `responseTime` từ `settings.portal_response_time`; `intakeUrl` là link form khách mới khi đang bật, ngược lại `null` (BR-133) |
| GET | `/api/portal/projects` | `[{ id, name, designType, status, dueDate, deliverableSummary: { total, delivered, approved, inReview, awaitingClient }, hasOpenQuotation, openQuotationId }]` — `awaitingClient` = sản phẩm `AWAITING_CLIENT` (BR-97) |
| GET | `/api/portal/projects/{id}` | Project công khai: status (+ lý do nếu public), dates, deliverables (`status`, `includedRounds`, `usedRounds`, `remainingRounds`, `progressPercent`, `clientDecision`, `reviewState` ∈ `NONE`/`AWAITING_CLIENT`/`CLIENT_APPROVED`/`CHANGES_IN_PROGRESS`, `hasOpenChangeRequest`), brief CONFIRMED mới nhất |
| GET | `/api/portal/projects/{id}/revisions` | Rounds + items: `description, type, billing, workStatus` (không `internalNotes`) |
| GET | `/api/portal/projects/{id}/files` | Chỉ `visible_to_client = true`, không xóa |
| GET | `/api/portal/files/{id}/content` | Kiểm tra thuộc khách + visible (hoặc khách tự upload) |
| GET | `/api/portal/projects/{id}/timeline?page=` | Activity `visible_to_client = true`: `{ occurredAt, message }` |
| GET | `/api/portal/quotations?projectId=` | Status ≠ DRAFT, ≠ SUPERSEDED |
| GET | `/api/portal/quotations/{id}` | `snapshot` + `effectiveStatus` + `paymentStatus` + `qrOptions` + `versions: [{version, status}]` + `clientAcceptedAt` + `clientPaymentNotice: { purpose, amount, at, note, pending }` hoặc `null` (list item cũng có hai trường này) |
| GET | `/api/portal/quotations/{id}/versions/{version}` | Xem bản SUPERSEDED (lịch sử) |
| GET | `/api/portal/quotations/{id}/qr/{purpose}.png` | Tải ảnh QR |
| POST | `/api/portal/quotations/{id}/accept` | **"Đồng ý báo giá"** (BR-76). Không body. `200` = chi tiết báo giá (như GET) với `clientAcceptedAt` đã có; idempotent. `409 INVALID_TRANSITION` (không còn `SENT`), `409 QUOTATION_EXPIRED`, `409 CONCURRENCY_CONFLICT`; ngoài phạm vi khách → `404`. Ghi `quotation.client_accepted`, Discord sau commit; **không** đổi `status` |
| POST | `/api/portal/quotations/{id}/payment-notice` | **"Tôi đã chuyển khoản"** (BR-77). `{ purpose: "deposit" / "full" / "remaining", note?: string(≤500) }` → `200` chi tiết báo giá với `clientPaymentNotice`; idempotent cho đúng khoản đang chờ. `400` (`errors.purpose` / `errors.note`), `409 PAYMENT_NOT_APPLICABLE`, `409 CONCURRENCY_CONFLICT`, `404` ngoài phạm vi. Ghi `quotation.client_payment_notice`; **không** đổi `payment_status` |
| GET | `/api/portal/studio` | **Public** (`AllowAnonymous`, không cần session): `{ displayName, contactPhone, contactEmail }` — cho trang hết hạn/link lỗi. Rate limit 60/phút/IP (`RateLimit:PortalStudioPerMinute`) |
| GET | `/api/portal/projects/{id}/images` | Ảnh dự án được chia sẻ cho khách (scope theo `clientId`) |
| GET | `/api/portal/images/{id}/{variant}` | Stream ảnh `preview`/`thumb`; ảnh khách gửi lưu một bản nên `preview` là chính ảnh khách gửi; ngoài scope → 404. `Cache-Control: private, max-age=600` (ẩn/xóa/đăng xuất phải có hiệu lực sớm trên máy dùng chung); ETag là validator duy nhất |
| POST | `/api/portal/deliverables/{id}/approve` | Khách duyệt hạng mục `AWAITING_CLIENT`; ngoài scope → `404`; đang có việc sửa mở → `409 CHANGE_REQUEST_OPEN` (BR-97); đã duyệt → `200` idempotent; không còn chờ duyệt → `409 INVALID_TRANSITION` |
| GET | `/api/portal/request-templates` | Mẫu **đang dùng**, theo thứ tự: `[{ id, name, description, fields }]` - không `serviceId`/`designType`/`active` (dữ liệu chung của studio, không theo khách) |
| GET | `/api/portal/requests?projectId=` | `[{ id, createdAt, content, status, declineReason, attachments, templateName }]` |
| POST | `/api/portal/projects/{id}/requests` | multipart `content`, `deliverableId?`, `files[]` (≤ 5, ≤ 20MB/file), **`templateId?` + `answers?` (JSON `{ [key]: giá trị }`)** - BR-53/54, BR-131/132. Có `templateId`: `content` là ghi chú thêm (≤ 2000) và server render văn bản (≤ 5000); không trả lời gì → `400 errors.answers` |

### 4.1 Public API (ẩn danh, Plan 13)

Không cookie, không CSRF (ngoại lệ duy nhất: tiền tố `/api/public/*`, 07-security §3), `AllowAnonymous`. Rate limit: `GET` 10/phút/IP (`RateLimit:PublicIntakePerMinute`), `POST` 5/giờ/IP (`RateLimit:PublicIntakeSubmitPerHour`, thay chính sách GET), IPv6 theo /64 → `429`.

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/public/intake/{slug}` | `{ studio: { displayName, contactPhone, contactEmail }, templates: [{ id, name, description, fields }], maxImages, maxUploadMb, formToken }`. `maxImages` = 3, hoặc `0` khi kho ≥ 80 % (chỉ nhận chữ). Header `Cache-Control: no-store`, `X-Robots-Tag: noindex`. Slug sai / form tắt → cùng một `404` |
| POST | `/api/public/intake/{slug}` | multipart: `contactName`, `contactPhone`, `contactEmail?`, `templateId?`, `answers?` (JSON), `note?`, `files[]` (≤ 3 JPEG/PNG/WebP), `formToken`, `website` (honeypot, để trống). Thành công `204` rỗng (không echo). Lỗi: `400` validation theo trường (`contactName`, `contactPhone`, `contactEmail`, `templateId`, `note`, `files`, `answers`, `answers.<mã>`) - token vẫn dùng được; `400 FORM_EXPIRED`; `404`; `413` body > `App:MaxUploadMb × 3 + 1` MB; `415` sai định dạng ảnh; `429 RATE_LIMITED` (trần 30/ngày studio hoặc 5/ngày/IP) hoặc `429` của rate limiter; `507 STORAGE_FULL` khi gửi ảnh lúc kho ≥ 80 % (gửi lại không kèm ảnh). Bot (honeypot / quá nhanh) cũng nhận `204` nhưng không lưu |

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
