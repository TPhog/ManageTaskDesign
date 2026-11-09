# 02 — Business Rules & State Machines

Tài liệu này là **nguồn sự thật** cho domain logic. Mỗi rule có ID `BR-xx` để tham chiếu trong code/test.

## 1. Nguyên tắc chung

| ID | Rule |
|---|---|
| BR-01 | Designer quyết định mọi trạng thái. Hệ thống chỉ **gợi ý** và **cảnh báo**, không tự chuyển trạng thái nghiệp vụ (ngoại lệ: SUPERSEDED, xem BR-34). |
| BR-02 | Tiền tệ duy nhất VND, lưu `bigint` (đồng). Không có số thập phân. |
| BR-03 | Làm tròn: mọi phép tính phần trăm làm tròn **half-up** về đồng (`Math.Round(x, MidpointRounding.AwayFromZero)`). |
| BR-04 | Tiền chỉ tồn tại trong `quotation_items` / `quotations`. Revision item **không** lưu tiền, chỉ liên kết `quotation_item_id`. |
| BR-05 | Mọi thay đổi trạng thái, tiền, quota, token portal đều ghi `activity_logs` trong **cùng transaction**. |
| BR-06 | Timestamps UTC. "Hôm nay", "quá hạn", "hết hạn" tính theo ngày lịch `Asia/Ho_Chi_Minh`. |

## 2. Client

| ID | Rule |
|---|---|
| BR-10 | Không archive được khách nếu có dự án ở `IN_PROGRESS` hoặc `ON_HOLD`. |
| BR-11 | Archive khách → tự động tắt portal (`portal_enabled = false`). |
| BR-12 | Tạo lại magic link → tăng `portal_token_version`; mọi session portal cũ lập tức vô hiệu. |

## 3. Project — State machine

```text
            ┌──────────────────────────────────────────────┐
            │                                              ▼
 DRAFT ──► QUOTED ──► IN_PROGRESS ◄──► ON_HOLD        CANCELLED
   │          │            │                ▲
   │          │            ▼                │ (reopen)
   │          │        COMPLETED ───────────┘ → IN_PROGRESS
   └──────────┴────────────────────────────────► CANCELLED
```

| Từ | Đến | Điều kiện / Ghi chú |
|---|---|---|
| DRAFT | QUOTED | Có ít nhất 1 báo giá INITIAL ở SENT (gợi ý, không bắt buộc) |
| DRAFT, QUOTED | IN_PROGRESS | Nếu chưa có INITIAL ACCEPTED → cảnh báo, yêu cầu xác nhận |
| IN_PROGRESS | ON_HOLD | Bắt buộc `hold_reason` |
| ON_HOLD | IN_PROGRESS | — |
| IN_PROGRESS | COMPLETED | Cảnh báo nếu còn deliverable chưa DELIVERED/CANCELLED hoặc còn báo giá ACCEPTED chưa PAID |
| COMPLETED | IN_PROGRESS | "Mở lại", bắt buộc lý do |
| bất kỳ (trừ COMPLETED) | CANCELLED | Bắt buộc lý do. CANCELLED là trạng thái cuối |

| ID | Rule |
|---|---|
| BR-20 | Chuyển trạng thái không hợp lệ → `409 INVALID_TRANSITION`. |
| BR-21 | Dự án CANCELLED/COMPLETED: Portal vẫn xem được, nhưng khách **không** gửi request mới được. |

## 4. Brief

| ID | Rule |
|---|---|
| BR-25 | Mỗi dự án có tối đa **1** brief version ở DRAFT tại một thời điểm. |
| BR-26 | `version` tăng tuần tự từ 1 trong dự án. |
| BR-27 | CONFIRMED → nội dung bất biến (DB trigger chặn UPDATE `content`). |
| BR-28 | Version mới được khởi tạo bằng bản sao nội dung của version mới nhất. |

## 5. Deliverable & Quota

### 5.1 Định nghĩa

- `included_rounds`: số round miễn phí của deliverable (copy từ service khi tạo, sửa được).
- `extra_round_price`: giá gợi ý cho mỗi round vượt quota (copy từ service khi tạo, sửa được).
- **Round được tính quota** (`counted round`) = round thuộc deliverable đó có **≥ 1 item `billing = IN_QUOTA` và `work_status ≠ CANCELLED`**.
- `used_rounds` = số counted round của deliverable (**tính toán**, không lưu cột).
- `remaining_rounds` = `max(0, included_rounds - used_rounds)`.

### 5.2 Rules

| ID | Rule |
|---|---|
| BR-40 | Một round thuộc **tối đa 1** deliverable. Request chạm nhiều deliverable → designer tách thành nhiều round. |
| BR-41 | Round không có deliverable (cấp dự án) **không** được chứa item `IN_QUOTA` (chỉ FREE_FIX/CHARGEABLE). |
| BR-42 | Một round chỉ trừ **đúng 1** quota dù có bao nhiêu item IN_QUOTA. |
| BR-43 | Item `FREE_FIX` (lỗi do designer) và `CHARGEABLE` không trừ quota. |
| BR-44 | Giảm `included_rounds` xuống dưới `used_rounds` được phép nhưng phải cảnh báo; không tự đổi billing các item cũ. |
| BR-45 | Đổi billing của item cũ làm `used_rounds` thay đổi → ghi activity với before/after. |

### 5.3 Thuật toán gợi ý billing (khi tạo item)

```text
suggestBilling(item, round):
  if item.type in (SCOPE_CHANGE, NEW_DELIVERABLE): return CHARGEABLE
  if round.deliverable_id is null:                   return CHARGEABLE
  d = round.deliverable
  if round đã là counted round:                      return IN_QUOTA   // không tốn thêm quota
  if used_rounds(d) < d.included_rounds:             return IN_QUOTA
  return CHARGEABLE
```

UI hiển thị gợi ý + lý do ("Deliverable Logo đã dùng 3/3 vòng"). Designer có thể chọn khác (kể cả FREE_FIX).

### 5.4 Giá khi tạo báo giá phát sinh (BR-46)

Từ tập item CHARGEABLE đã chọn (chưa có `quotation_item_id`). UI mở dialog "Tạo báo giá phát sinh" (xem trước bằng
`from-revisions/preview`) để designer chọn dịch vụ / nhập đơn giá cho **từng dòng** trước khi tạo nháp.

Đơn giá mỗi dòng = **giá designer nhập** → nếu không nhập: `service.base_price` (chỉ dòng Hạng mục mới có chọn dịch vụ)
→ nếu không có: **giá gợi ý** bên dưới → 0 ₫ (UI nêu tên dòng 0 ₫, vẫn cho tạo — BR-31). Giá nhập: 0 … 100.000.000.000 ₫
(`QuotationValidation.MaxUnitPrice`, 100 tỷ, giữ `quantity × unitPrice` xa ngưỡng tràn `long`).

1. Nhóm các item MINOR/MAJOR theo **round** → mỗi round = **1 dòng**:
   `"Vòng sửa thứ {round_no} — {deliverable.name}"`, qty 1, gợi ý `deliverable.extra_round_price` (round cấp dự án: 0).
   Một round chỉ nhận **một** giá nhập.
2. Mỗi item NEW_DELIVERABLE = 1 dòng; chọn service → mô tả `service.name`, chi tiết = mô tả item, gợi ý `service.base_price`;
   không chọn → mô tả item, gợi ý 0. Service đã chọn lưu ở `revision_items.service_id` (dùng ở BR-48).
3. Mỗi item SCOPE_CHANGE = 1 dòng, gợi ý 0.
4. Mỗi item được gán `quotation_item_id` của dòng tương ứng (nhiều item → 1 dòng được phép).

### 5.5 Liên kết qua version báo giá (BR-47)

- Tạo version mới copy các dòng, ghi `copied_from_item_id` = dòng ở version trước.
- Khi version mới được **send**: revision item đang trỏ dòng cũ được chuyển sang dòng mới có
  `copied_from_item_id` tương ứng (cùng transaction với BR-34). Dòng bị xóa ở version mới → item gỡ liên kết.
- Một revision item chỉ trỏ tới dòng của **một** version tại một thời điểm.
- "Item đã được báo giá chấp nhận" ⇔ `quotation_item_id` thuộc báo giá `status = ACCEPTED`.

### 5.6 Hạng mục mới thành deliverable khi chấp nhận (BR-48)

- Khi designer **đánh dấu một báo giá đã chấp nhận**: mỗi item NEW_DELIVERABLE (chưa CANCELLED) đang trỏ dòng của báo giá
  đó và chưa có `created_deliverable_id` → tạo **1 deliverable** trong cùng transaction: tên = **mô tả của revision item**
  (không có thì tên service; ≤ 200 ký tự, cắt thêm "…"), số lượng = số lượng dòng, `service_id` = `revision_items.service_id`,
  `included_rounds` / `extra_round_price` copy từ service (không service: 0 / 0). Ghi `revision_items.created_deliverable_id`;
  item chuyển work status sang **DONE** (activity `revision_item.work_status_changed`, `via = quotation.accepted`; chuyển
  này nằm ngoài sơ đồ BR-60 vì do lệnh accept thực hiện); activity `deliverable.created` (`via = quotation.accepted`).
- Kết quả theo trạng thái dự án:

| Dự án | Deliverable | Task |
|---|---|---|
| `IN_PROGRESS`, `ON_HOLD` | `IN_PROGRESS` (lệnh accept của designer đóng vai "Bắt đầu") | 1 task `TODO` cho mỗi bước mẫu của service (không service: không task) |
| `DRAFT`, `QUOTED` | `NOT_STARTED` | không; "Bắt đầu dự án" đề xuất bước mẫu như mọi deliverable |
| `COMPLETED` | `NOT_STARTED` | không (designer mở lại dự án) |
| `CANCELLED` | **không tạo gì**; activity `quotation.accepted` ghi `newDeliverablesSkipped = "PROJECT_CANCELLED"` + `skippedRevisionItemIds` | — |

- Báo giá nháp / đã gửi / bị từ chối: **không** tạo gì. Mỗi item chỉ tạo deliverable **1 lần** (unique index).
  Item đã thành deliverable không đổi loại được (`422 BUSINESS_RULE`).
- Item đang trỏ dòng báo giá **SENT/ACCEPTED** không được đổi loại sang/khỏi NEW_DELIVERABLE: `409 BILLING_LOCKED`
  (cùng cơ chế BR-61, vì accept hành động theo loại item).

### 5.7 Trạng thái duyệt trên Portal — một giọng (BR-97)

> Plan 12 dự định số **BR-48**, nhưng BR-48 đã thuộc §5.6 (P09), nên quy tắc này là **BR-97**.

| ID | Rule |
|---|---|
| BR-97 | Khách chỉ thấy **một** trạng thái duyệt cho mỗi sản phẩm (`reviewState`, server tính bằng `ClientReviewStates.Of`): `CHANGES_IN_PROGRESS` khi sản phẩm đang `IN_PROGRESS`/`IN_REVIEW` **và** có việc sửa đang mở — một request `NEW` gắn với sản phẩm, hoặc một mục sửa `TODO`/`IN_PROGRESS` thuộc bất kỳ vòng nào của sản phẩm; `CLIENT_APPROVED` khi `IN_REVIEW` + `client_decision = APPROVED`; `AWAITING_CLIENT` khi `IN_REVIEW` còn lại; `NONE` cho mọi trạng thái khác. Việc đang sửa thắng mọi trạng thái khác. Khách chỉ duyệt được ở `AWAITING_CLIENT`; đang sửa thì `409 CHANGE_REQUEST_OPEN` (điều kiện nằm trong cùng câu `UPDATE ... WHERE` của lệnh duyệt nên request tạo đồng thời không lọt được); duyệt lặp lại khi đã `CLIENT_APPROVED` trả `200` không ghi gì thêm. Số "chờ bạn duyệt" = số sản phẩm `AWAITING_CLIENT` (`deliverableSummary.awaitingClient`). Phía designer, luồng dùng cùng quy tắc: `CONFIRM_APPROVAL` chỉ xuất hiện khi `CLIENT_APPROVED`, và cảnh báo `REVIEW_WITH_OPEN_REVISIONS` khi sản phẩm `IN_REVIEW` còn mục sửa chưa xong. |

## 6. Client Request

```text
NEW ──► TRIAGED
  └───► DECLINED
```

| ID | Rule |
|---|---|
| BR-50 | Chỉ request NEW mới được triage/decline. |
| BR-51 | Triage tạo 1..n round trong **cùng transaction**; mỗi round lưu `source_request_id`. |
| BR-52 | DECLINED bắt buộc `decline_reason` (khách thấy). |
| BR-53 | Khách gửi request chỉ cho dự án của mình và dự án ở DRAFT/QUOTED/IN_PROGRESS/ON_HOLD. |
| BR-54 | Rate limit: tối đa 20 request/khách/ngày. |

Màn "Đã gửi yêu cầu" trên Portal hiện "Nhà thiết kế thường phản hồi {`settings.portal_response_time`}" — designer đặt ở Cài đặt › Khách hàng & Portal; tối đa 60 ký tự, bỏ trống = mặc định "trong 24 giờ" (`GET /api/portal/me` → `designer.responseTime`). Ảnh đính kèm request từ Portal tối đa **5** ảnh/lần gửi.

## 7. Revision Item — Work status

```text
TODO ──► IN_PROGRESS ──► DONE
  │           │
  └───────────┴────────► CANCELLED
DONE ──► IN_PROGRESS (mở lại)
```

Ngoài sơ đồ: khi chấp nhận báo giá, item NEW_DELIVERABLE vừa thành deliverable được chuyển thẳng sang `DONE` (BR-48).

| ID | Rule |
|---|---|
| BR-60 | **Scope guard:** chuyển item `CHARGEABLE` sang IN_PROGRESS/DONE khi item chưa gắn quotation item thuộc báo giá ACCEPTED → API trả `428 SCOPE_GUARD` trừ khi request có `override_reason` (≥ 5 ký tự). Override ghi activity `revision_item.scope_override`. |
| BR-61 | Item đã gắn quotation item thuộc báo giá SENT/ACCEPTED không được đổi `billing` (phải tạo version báo giá mới, bỏ liên kết trước). |
| BR-62 | Khi báo giá bị REJECTED: các item liên kết **không** tự đổi; UI gợi ý designer chuyển CANCELLED hoặc đổi billing. |

## 8. Quotation

### 8.1 State machine

```text
DRAFT ──send──► SENT ──markAccepted──► ACCEPTED
                 │ ├──markRejected──► REJECTED
                 │ └──(new version sent)──► SUPERSEDED
                 └── EXPIRED (tính khi đọc, không lưu)
```

- `effective_status` = `EXPIRED` nếu `status = SENT` và `today(VN) > expiry_date`; ngược lại = `status`.
- Từ EXPIRED (thực chất là SENT) designer vẫn có thể mark ACCEPTED (khách đồng ý muộn) — cảnh báo.

### 8.2 Rules

| ID | Rule |
|---|---|
| BR-30 | Chỉ DRAFT được sửa header/items. |
| BR-31 | `send`: yêu cầu ≥ 1 item, `total ≥ 0`, `expiry_date ≥ issue_date`; gán `number` (nếu version 1), `issue_date = today`, tính tiền, ghi `snapshot` (JSON đầy đủ: header, items, settings ngân hàng, terms, designer profile), `sent_at`. |
| BR-32 | Số báo giá: `{prefix}-{YYYY}-{seq:0000}`, seq tăng theo năm, lấy từ bảng `quotation_sequences` với `SELECT ... FOR UPDATE`. Ví dụ `BG-2026-0007`. Số không bao giờ tái sử dụng. |
| BR-33 | Tạo version mới từ SENT/EXPIRED/REJECTED: copy thành DRAFT, cùng `number`, `version + 1`, `previous_version_id`. Mỗi `number` tối đa 1 DRAFT. |
| BR-34 | Khi version N+1 được `send` → version đang SENT của cùng number chuyển SUPERSEDED (tự động, cùng transaction). Không thể tạo version mới khi đã có version ACCEPTED. |
| BR-35 | Ràng buộc DB: mỗi `number` tối đa 1 bản ở `SENT` hoặc `ACCEPTED`. |
| BR-36 | `markAccepted`: chỉ từ SENT; lưu `accepted_at` (ngày designer nhập, mặc định hôm nay), `accepted_note`, `evidence_file_id` (tùy chọn). |
| BR-37 | `markRejected`: chỉ từ SENT; bắt buộc `rejected_reason`. |
| BR-38 | DRAFT được xóa cứng (chưa có giá trị pháp lý). SENT trở đi không xóa được. |
| BR-39 | Snapshot là nguồn hiển thị cho mọi báo giá ≠ DRAFT (Portal và Admin). Cột tiền trên bảng chỉ để query/aggregate và phải khớp snapshot. |

### 8.3 Công thức tiền

```text
item.amount      = qty × unit_price                       (qty: integer ≥ 1, unit_price ≥ 0)
subtotal         = Σ item.amount
discount_amount  = 0 ≤ discount_amount ≤ subtotal         (designer nhập số tiền)
taxable          = subtotal − discount_amount
vat_amount       = round_half_up(taxable × vat_percent / 100)   (vat_percent ∈ [0, 20], mặc định 0)
total            = taxable + vat_amount
deposit_amount   = round_half_up(total × deposit_percent / 100) (deposit_percent ∈ [0, 100])
```

Ví dụ: 2 item 2.000.000 + 500.000, giảm 100.000, VAT 0, cọc 50% → subtotal 2.500.000, total 2.400.000, cọc 1.200.000.

### 8.4 Payment status (đánh dấu thủ công)

```text
UNPAID ──► DEPOSIT_PAID ──► PAID
  └──────────────────────────► PAID
(hoàn tác: PAID → DEPOSIT_PAID/UNPAID, DEPOSIT_PAID → UNPAID — bắt buộc lý do)
```

| ID | Rule |
|---|---|
| BR-70 | Chỉ báo giá ACCEPTED được đổi `payment_status`. |
| BR-71 | DEPOSIT_PAID chỉ khi `deposit_percent > 0`. |
| BR-72 | Lưu `deposit_paid_at`, `paid_at` (ngày designer nhập) + activity log. |
| BR-73 | `outstanding_amount` (tính): UNPAID → `total`; DEPOSIT_PAID → `total − deposit_amount`; PAID → 0. |

### 8.5 QR hiển thị theo trạng thái (BR-75)

| effective_status / payment_status | QR được hiển thị |
|---|---|
| DRAFT, SUPERSEDED, REJECTED | Không có QR |
| SENT, EXPIRED + deposit_percent > 0 | **Cọc** (`deposit_amount`), **Toàn bộ** (`total`) |
| SENT, EXPIRED + deposit_percent = 0 | **Toàn bộ** |
| ACCEPTED + UNPAID | Như SENT |
| ACCEPTED + DEPOSIT_PAID | **Phần còn lại** (`total − deposit_amount`) |
| ACCEPTED + PAID | Không QR, hiển thị "Đã thanh toán" |

Nội dung chuyển khoản: `{number_no_dash} {COC|CONLAI|TT}` — ví dụ `BG20260007 COC` (06-vietqr-spec §4).

### 8.6 Lời khách trên Portal (BR-76, BR-77)

Nguyên tắc BR-01: Portal chỉ **ghi nhận** lời khách; designer xác nhận bằng hành động có sẵn. Không có thay đổi trạng thái tự động.

| ID | Rule |
|---|---|
| BR-76 | **"Đồng ý báo giá".** Khi báo giá `SENT` và chưa hết hạn, khách bấm → chỉ ghi `client_accepted_at` (+ activity `quotation.client_accepted`, hiện cho khách, + Discord sau commit). `status` vẫn `SENT` cho tới khi designer bấm chấp nhận (BR-31). Idempotent: bấm lại khi đã ghi trả `200` và không ghi activity/Discord thứ hai (kiểm trước điều kiện hết hạn). Báo giá không còn `SENT` → `409 INVALID_TRANSITION`; `SENT` nhưng quá `expiry_date` và chưa đồng ý → `409 QUOTATION_EXPIRED`. Designer thấy bước `CONFIRM_CLIENT_ACCEPTANCE` ("Khách đã đồng ý báo giá … — xác nhận") thay cho `RECORD_ACCEPTANCE`; bước này biến mất khi báo giá chuyển `ACCEPTED`. |
| BR-77 | **"Tôi đã chuyển khoản".** Khách chọn một khoản trong các lựa chọn QR của BR-75 (`deposit` / `full` / `remaining`) và ghi chú tuỳ chọn ≤ 500 ký tự → ghi `client_payment_notice_purpose/at/note` (+ activity `quotation.client_payment_notice` kèm `purpose`, `amount`, `hasNote` — không log nội dung ghi chú, + Discord). Khoản không nằm trong lựa chọn QR hiện tại → `409 PAYMENT_NOT_APPLICABLE`; `purpose` sai/thiếu hoặc ghi chú quá dài → `400`. Gửi lại đúng khoản đang chờ là idempotent; khoản khác thay thế báo cũ. "Chờ xác nhận" (`pending`) = báo giá `SENT`/`ACCEPTED` và `payment_status` chưa tới mức khách báo (`deposit`: `UNPAID`; `full`: khác `PAID`; `remaining`: `DEPOSIT_PAID`). `payment_status` do designer đổi như BR-70..72 (bước `CONFIRM_CLIENT_PAYMENT` dẫn tới đó); các cột `client_*` **không bị xoá** khi designer xác nhận, chỉ `pending` chuyển `false`. |

Cả hai hành động chỉ ghi các cột `client_*` của báo giá nên được whitelist trong trigger `fn_quotation_locked` (xem 04 §2); `updated_at` cũng được cập nhật để `xmin`/concurrency vẫn đúng.

## 9. Task

| ID | Rule |
|---|---|
| BR-80 | Trạng thái tự do giữa TODO/IN_PROGRESS/REVIEW/DONE/BLOCKED (Kanban). |
| BR-81 | Quá hạn: `due_date < today(VN)` và `status ≠ DONE`. |
| BR-82 | Task của dự án CANCELLED không xuất hiện trong "Tất cả task" mặc định. |

## 10. Files

| ID | Rule |
|---|---|
| BR-85 | File gắn với dự án (bắt buộc), deliverable/request/quotation (tùy chọn). |
| BR-86 | Xóa file = xóa mềm (`deleted_at`) rồi xóa object ngay sau commit. Object còn sót (xóa lỗi, row đã xóa, biến thể thừa, object không có row > 24 h, file ghi dở > 24 h) được job đối soát hằng ngày (`StorageSweeper`) dọn; job chỉ ghi `storage.reconciled` khi có phát hiện/thay đổi, và mỗi bước độc lập (một bước lỗi không dừng các bước khác). Key không đúng dạng ảnh không bao giờ bị xóa. Quota luôn tính từ DB. |
| BR-87 | File là `evidence_file_id` của báo giá ACCEPTED không xóa được. |
| BR-88 | Xóa task xóa luôn ảnh của task (xóa mềm cùng transaction, object xóa sau commit); ảnh là `evidence_file_id` của báo giá (BR-87) được giữ lại, `task.deleted` ghi `imagesDeleted`/`imagesKept`. Upload ảnh vào task khóa dòng task (share lock) nên không chạy chồng với xóa task. |
| BR-89 | "Xóa bản gốc của dự án đã hoàn thành cũ hơn N tháng" (N 1–120) chỉ chạy khi designer bấm: xem trước (số ảnh/MB + `digest` của tập id) → xác nhận (không khôi phục được) gửi lại `expectedImageCount`, `expectedFreedBytes`, `expectedDigest`; khác bản xem trước → `409 CONCURRENCY_CONFLICT`. Giữ preview + thumb; khóa các dòng `FOR UPDATE` và kiểm lại BR-87; bỏ qua ảnh chứng từ và ảnh khách lưu một bản; ngày hoàn thành = lần `project.status_changed` cuối. Ghi `image.originals_deleted` mỗi dự án (`fileCount`, ≤ 200 `fileIds`). Không job nào tự chạy (BR-01). |

**Biến thể ảnh (P10).** Chỉ áp cho ảnh mới. original ≤ 3000 px (q80), preview ≤ 2560 px (q78, watermark), thumb 480 px (q75). Ảnh phẳng (≤ 256 màu trên mẫu 64×64) thử thêm WebP lossless cho original và giữ bản nhỏ hơn. Ảnh khách gửi lưu **một bản** (preview là bí danh của original, `preview_is_original`) cộng thumb. Upload trùng SHA-256 trong cùng dự án vẫn lưu và trả cờ `duplicateOf`.

## 11. Dashboard metrics (BR-90)

| Chỉ số | Định nghĩa |
|---|---|
| Đang chờ thu | Σ `outstanding_amount` của báo giá ACCEPTED, dự án ≠ CANCELLED |
| Giá trị đã chốt tháng | Σ `total` báo giá ACCEPTED có `accepted_at` trong tháng hiện tại (VN) |
| Doanh thu phát sinh tháng | Như trên nhưng chỉ `kind = ADDITIONAL` |
| Báo giá chờ phản hồi | Số báo giá `status = SENT` và chưa hết hạn |
| Sắp hết hạn | SENT và `expiry_date` trong [today, today+3] |

### 11.1 Mở rộng dashboard (P11: BR-91..94, BR-96)

BR-95 đã dùng cho mục 12 (hiển thị công khai trên Portal) nên P11 dùng BR-91..94 và BR-96; không có BR-95 trong nhóm này.

| ID | Quy tắc |
|---|---|
| BR-91 | **Lịch 7 ngày.** Cửa sổ là hôm nay → hôm nay+6 (ngày VN). Chỉ task chưa `DONE` của dự án chưa `CANCELLED` (BR-81/82). Task có hạn trước hôm nay vào nhóm **Quá hạn** riêng, nổi bật, cũ nhất trước, hiện tối đa 10 kèm tổng. Mỗi ngày hiện tối đa 20 task (sắp theo ưu tiên rồi `sort_order`), phần dư trả về `hiddenTasks`. Deadline dự án đang mở (`DRAFT/QUOTED/IN_PROGRESS/ON_HOLD`) và deliverable chưa bàn giao hiện cùng ngày với task. Task không có hạn không nằm trong lịch, chỉ được đếm (`undatedOpenTasks`). Tick `DONE` tại chỗ dùng `POST /api/admin/tasks/{id}/move` có sẵn và có "Hoàn tác". |
| BR-92 | **Sức khỏe dự án.** Phạm vi: `IN_PROGRESS`, `ON_HOLD`, `QUOTED`, cộng `COMPLETED` còn báo giá `ACCEPTED` chưa `PAID` (DF-27). Mọi dự án trong phạm vi đều được chấm điểm trước khi xếp hạng (dự án rủi ro không hạn không bị loại), rồi sắp theo điểm giảm dần, hạn tăng dần (không hạn xếp sau), tên; trả tối đa 12 dòng kèm `total` và `hidden`. Mã rủi ro và trọng số: `LATE` 4, `DEPOSIT_UNPAID` 3, `BALANCE_DUE` 3, `TASKS_OVERDUE` 2, `OPEN_REQUESTS` 2, `QUOTA_EXCEEDED` 1; điểm = tổng trọng số các mã có mặt. `LATE` = hạn dự án < hôm nay và chưa `COMPLETED`; các mã còn lại lấy từ cùng `FlowFacts`/`FlowState` với trang dự án. Tiến độ = Σ task `DONE` / Σ task của deliverable chưa `CANCELLED` (làm tròn half-up; `null` khi chưa có task). |
| BR-93 | **Tiền thu theo kỳ và tuổi nợ.** `Đã thu` = phần cọc theo `deposit_paid_at` + (`total` − cọc) theo `paid_at` của báo giá `ACCEPTED`. Kỳ `month/quarter/year` tính "đến hôm nay" và so với cùng số ngày đã trôi của kỳ trước (`period` khác ba giá trị trên → 400). Công nợ dùng cùng bộ lọc BR-73 với `money.outstanding` nên `receivables.total = money.outstanding`. Tuổi nợ tính từ `deposit_paid_at` (nếu đã cọc) hoặc `accepted_at`; bucket `0-7`, `8-30`, `31+`. Danh sách công nợ hiện tối đa 20 dòng (`hiddenItems`) và 10 khách. |
| BR-94 | **Digest hằng ngày.** Gửi tối đa một lần mỗi ngày VN, vào hoặc sau giờ cấu hình (`digest_hour_vn` 0-23, mặc định 8; `digest_enabled` mặc định bật). Nội dung: task quá hạn, hôm nay, ngày mai (mỗi nhóm tối đa 5 dòng), deadline dự án và báo giá `SENT` hết hạn trong 3 ngày, kèm tổng công nợ làm ngữ cảnh. Chỉ công nợ thì không kích hoạt tin. Ngày được claim nguyên tử bằng `UPDATE dashboard_state ... WHERE digest_last_run_date IS NULL OR < today` (idempotent giữa nhiều tiến trình), kể cả khi không có gì để báo. Claim, đọc dữ liệu và enqueue nằm trong một transaction; commit sau khi enqueue. Nếu có lỗi trước commit thì claim được hoàn lại và lần tick sau thử lại. Đánh đổi chấp nhận: nếu commit lỗi sau khi enqueue thì tin đã nằm trong hàng đợi và ngày được thử lại, nên hiếm khi có thể gửi trùng một tin. Digest không đổi trạng thái nghiệp vụ (BR-01). |
| BR-96 | **Checklist thiết lập lần đầu.** 6 mục: `STUDIO` (tên studio khác tên mẫu "FreelanceFlow Studio" **và** có SĐT hoặc email), `BANK` (đủ BIN, số tài khoản, tên chủ tài khoản), `SERVICE` "Xem lại bảng giá dịch vụ" (có dịch vụ đang bán, giá > 0, được tạo hoặc sửa sau thời điểm seed), `TASK_TEMPLATE` (có dịch vụ đang bán với task mẫu), `WATERMARK` (đã lưu cấu hình watermark ít nhất một lần, ghi nhận qua activity `settings.watermark_updated`), `CLIENT` (có khách chưa archive). Hoàn thành tính từ dữ liệu thật; chỉ trạng thái "đã ẩn" được lưu (`dashboard_state.setup_dismissed_at`). Thẻ ẩn khi cả 6 mục xong hoặc đã ẩn; ẩn là idempotent. |

## 12. Hiển thị công khai trên Portal (BR-95)

Activity được ghi `visible_to_client = true` cho các action sau (kèm `client_message` tiếng Việt):

| Action | client_message mẫu |
|---|---|
| project.status_changed | "Dự án chuyển sang: Đang thực hiện" |
| project.on_hold (nếu designer chọn công khai) | "Dự án tạm dừng: {lý do}" |
| deliverable.status_changed | "Logo: Chờ duyệt" |
| brief.confirmed | "Brief phiên bản 2 đã được xác nhận" |
| request.triaged / request.declined | "Yêu cầu của bạn đã được tiếp nhận" / "Yêu cầu bị từ chối: {lý do}" |
| quotation.sent / accepted / rejected | "Báo giá BG-2026-0007 (v2) đã được gửi" |
| quotation.client_accepted | "Bạn đã đồng ý báo giá BG-2026-0007 — chờ nhà thiết kế xác nhận" |
| quotation.client_payment_notice | "Bạn báo đã chuyển 1.000.000 ₫ (tiền cọc) cho báo giá BG-2026-0007 — chờ nhà thiết kế kiểm tra" |
| deliverable.client_approved | "Khách đã duyệt Logo" |
| quotation.payment_status_changed | "Đã nhận tiền cọc cho BG-2026-0007" |
| file.shared | "Đã chia sẻ file: logo_v3.png" |
