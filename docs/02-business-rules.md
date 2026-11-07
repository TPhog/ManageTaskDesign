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

### 5.4 Giá gợi ý khi tạo báo giá phát sinh (BR-46)

Từ tập item CHARGEABLE đã chọn (chưa có `quotation_item_id`):

1. Nhóm các item MINOR/MAJOR theo **round** → mỗi round = **1 dòng**:
   `"Vòng sửa thứ {round_no} — {deliverable.name}"`, qty 1, đơn giá = `deliverable.extra_round_price`.
2. Mỗi item NEW_DELIVERABLE = 1 dòng; nếu designer chọn service → đơn giá = `service.base_price`, ngược lại 0.
3. Mỗi item SCOPE_CHANGE = 1 dòng, đơn giá 0 (designer nhập).
4. Mỗi item được gán `quotation_item_id` của dòng tương ứng (nhiều item → 1 dòng được phép).

### 5.5 Liên kết qua version báo giá (BR-47)

- Tạo version mới copy các dòng, ghi `copied_from_item_id` = dòng ở version trước.
- Khi version mới được **send**: revision item đang trỏ dòng cũ được chuyển sang dòng mới có
  `copied_from_item_id` tương ứng (cùng transaction với BR-34). Dòng bị xóa ở version mới → item gỡ liên kết.
- Một revision item chỉ trỏ tới dòng của **một** version tại một thời điểm.
- "Item đã được báo giá chấp nhận" ⇔ `quotation_item_id` thuộc báo giá `status = ACCEPTED`.

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

## 7. Revision Item — Work status

```text
TODO ──► IN_PROGRESS ──► DONE
  │           │
  └───────────┴────────► CANCELLED
DONE ──► IN_PROGRESS (mở lại)
```

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
| BR-86 | Xóa file = xóa mềm (`deleted_at`); file vật lý bị dọn sau 30 ngày bởi job dọn dẹp (Phase 2; Phase 1 giữ nguyên). |
| BR-87 | File là `evidence_file_id` của báo giá ACCEPTED không xóa được. |

## 11. Dashboard metrics (BR-90)

| Chỉ số | Định nghĩa |
|---|---|
| Đang chờ thu | Σ `outstanding_amount` của báo giá ACCEPTED, dự án ≠ CANCELLED |
| Giá trị đã chốt tháng | Σ `total` báo giá ACCEPTED có `accepted_at` trong tháng hiện tại (VN) |
| Doanh thu phát sinh tháng | Như trên nhưng chỉ `kind = ADDITIONAL` |
| Báo giá chờ phản hồi | Số báo giá `status = SENT` và chưa hết hạn |
| Sắp hết hạn | SENT và `expiry_date` trong [today, today+3] |

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
| quotation.payment_status_changed | "Đã nhận tiền cọc cho BG-2026-0007" |
| file.shared | "Đã chia sẻ file: logo_v3.png" |
