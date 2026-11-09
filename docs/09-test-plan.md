# 09 — Test Plan & Acceptance Criteria

## 1. Chiến lược

| Tầng | Công cụ | Phạm vi | Mục tiêu |
|---|---|---|---|
| Unit (BE) | xUnit + FluentAssertions | `Domain/*`, `VietQr`, tính tiền, gợi ý billing, state machine | ≥ 80% line coverage `Domain/` |
| Integration (BE) | `WebApplicationFactory` + Testcontainers PostgreSQL 17 | Endpoint thật + DB thật + trigger + constraint + auth | Mọi endpoint có ≥ 1 happy path + lỗi chính |
| Isolation (BE) | Như trên | **Mọi** endpoint Portal với 2 khách | 100% endpoint Portal |
| Unit (FE) | Vitest + Testing Library | format tiền/ngày, form validation, component trạng thái | |
| E2E | Playwright | 5 luồng chính (§3) trên docker-compose | Chạy trước mỗi release |
| Thủ công | — | QR với app ngân hàng thật, mobile Portal (iOS Safari, Android Chrome) | Trước go-live |

**TDD:** viết test trước cho Domain (quota, billing, quotation state/tiền, VietQR) — đây là chỗ sai là mất tiền.
Integration test dùng `IClock` giả để kiểm EXPIRED/quá hạn.

Fixture chuẩn (`TestData.Seed()`): designer, settings (VCB 970436 / 0123456789), services mẫu,
khách **A** (dự án A1 có deliverable Logo 3 vòng, Banner 2 vòng) và khách **B** (dự án B1).

## 2. Acceptance criteria theo nghiệp vụ

### AC-QUOTA — Quota revision (BR-40..46)

| ID | Given | When | Then |
|---|---|---|---|
| AC-Q1 | Logo `included_rounds=3`, chưa có round | Triage request: 1 round Logo với 5 item MINOR, billing null | 1 round `round_no=1`, 5 item IN_QUOTA, `usedRounds=1` |
| AC-Q2 | Logo đã dùng 3/3 | Tạo round 4 với 1 item MAJOR | Gợi ý CHARGEABLE, lý do nêu "3/3"; `usedRounds` vẫn 3 |
| AC-Q3 | Logo 1/3 | Round có 2 IN_QUOTA + 1 CHARGEABLE | `usedRounds = 2` (chỉ trừ 1) |
| AC-Q4 | Logo 3/3 | Item FREE_FIX ở round 4 | `usedRounds` vẫn 3 |
| AC-Q5 | Round cấp dự án | Thêm item billing IN_QUOTA | `422 BUSINESS_RULE` (BR-41) |
| AC-Q6 | Round 2 có 1 IN_QUOTA | Chuyển item đó CANCELLED | `usedRounds` giảm 1, activity có before/after |
| AC-Q7 | Item NEW_DELIVERABLE | Tạo | Gợi ý CHARGEABLE |
| AC-Q8 | Logo 3/3, round 4 có 3 item CHARGEABLE MINOR + 1 NEW_DELIVERABLE (service Banner) | `from-revisions` | DRAFT ADDITIONAL có 2 dòng: "Vòng sửa thứ 4 — Logo" 200.000 và Banner 500.000; cả 4 item có `quotation_item_id` |
| AC-Q9 | Logo 0 vòng miễn phí: round 1 có 2 MINOR; round cấp dự án có NEW_DELIVERABLE "Banner FB" (service Banner), "Thiệp mời", "Poster" (service Banner) và SCOPE_CHANGE "Bản tiếng Anh" | `from-revisions/preview` rồi `from-revisions` với giá nhập: Đổi font 350.000, Thiệp mời 750.000, Poster 900.000, Bản tiếng Anh 400.000 | Preview: ROUND 200.000 gợi ý, NEW_DELIVERABLE/SCOPE_CHANGE 0, không link gì. DRAFT: 350.000 · Thiết kế Banner 500.000 · Thiệp mời 750.000 · Bản tiếng Anh 400.000 · Thiết kế Banner 900.000 (tổng 2.900.000); item Banner/Poster có `serviceId` |
| AC-Q10 | Như AC-Q9 | Giá âm / > 100 tỷ / `linePrices` cho NEW_DELIVERABLE hoặc mục ngoài danh sách / 2 giá khác nhau cho 1 vòng / service không tồn tại | `400` đúng field (`newDeliverableServices` / `linePrices`); không item nào bị link |
| AC-Q11 | Dự án IN_PROGRESS; báo giá phát sinh SENT có "Sticker" (service có 2 bước mẫu) và "Menu A5" (giá nhập, không service) | Accept | 2 deliverable `IN_PROGRESS` tên theo mô tả item (Sticker: service + quota của service; Menu: 0 vòng); item có `createdDeliverableId` và `workStatus = DONE`; 2 task TODO theo bước mẫu cho Sticker; activity `deliverable.created` `via=quotation.accepted` |
| AC-Q12 | Như AC-Q11 nhưng dự án DRAFT (hoặc QUOTED / COMPLETED) | Accept | Deliverable `NOT_STARTED`, **không** task; DRAFT/QUOTED: `start-preview` đề xuất bước mẫu của Sticker |
| AC-Q13 | Báo giá phát sinh SENT có NEW_DELIVERABLE | Reject; hoặc accept 2 lần; hoặc đổi loại item đã thành deliverable; hoặc đổi sang/khỏi NEW_DELIVERABLE trên dòng SENT/ACCEPTED | Reject: không deliverable. Accept lần 2: `409`, không thêm deliverable. Đổi loại item đã thành deliverable: `422 BUSINESS_RULE`. Đổi loại trên dòng SENT/ACCEPTED: `409 BILLING_LOCKED` |
| AC-Q14 | Như AC-Q11 nhưng dự án CANCELLED | Accept | Báo giá ACCEPTED, **không** deliverable/task; activity `quotation.accepted` có `newDeliverablesSkipped = PROJECT_CANCELLED` + `skippedRevisionItemIds` |

### AC-QUO — Báo giá (BR-30..39, 70..75)

| ID | Given | When | Then |
|---|---|---|---|
| AC-B1 | DRAFT: 2.000.000 + 500.000, giảm 100.000, VAT 0, cọc 50% | Lưu | subtotal 2.500.000, total 2.400.000, deposit 1.200.000 |
| AC-B2 | VAT 8% trên taxable 1.234.567 | Lưu | vat_amount 98.765 (half-up 98.765,36), total 1.333.332 |
| AC-B3 | DRAFT đầu tiên năm 2026, chưa có seq | Send | number `BG-2026-0001`, version 1, snapshot đủ trường, `issue_date` = hôm nay VN, `expiry_date` = +validity |
| AC-B4 | 2 lệnh send đồng thời của 2 báo giá khác | Send song song | Số khác nhau, liên tiếp, không trùng |
| AC-B5 | SENT | PUT sửa items | `409 QUOTATION_LOCKED`; UPDATE SQL trực tiếp cũng bị trigger chặn |
| AC-B6 | v1 SENT | new-version → sửa → send | v2 SENT cùng number; v1 SUPERSEDED; revision items chuyển link sang dòng v2 (BR-47) |
| AC-B7 | v1 SENT, v2 DRAFT | new-version lần nữa | `409` (1 DRAFT/chain) |
| AC-B8 | Đổi `base_price` service Logo 2.000.000 → 3.000.000 | Xem báo giá SENT cũ (admin + portal) | Vẫn 2.000.000 |
| AC-B9 | Đổi tài khoản ngân hàng trong settings | Xem QR báo giá SENT cũ | QR vẫn dùng tài khoản cũ (snapshot) |
| AC-B10 | SENT, `expiry_date` = hôm qua (clock giả) | GET | `effectiveStatus = EXPIRED`; DB `status = SENT` |
| AC-B11 | SENT | accept `{acceptedAt, note, evidenceFileId}` | ACCEPTED; activity `quotation.accepted` visible_to_client |
| AC-B12 | SENT | reject không có reason | `400` |
| AC-B13 | ACCEPTED, cọc 50%, UNPAID | payment-status DEPOSIT_PAID | `qrOptions` chỉ còn `remaining` = total − deposit |
| AC-B14 | ACCEPTED, cọc 0% | payment-status DEPOSIT_PAID | `422` (BR-71) |
| AC-B15 | SENT (chưa ACCEPTED) | payment-status PAID | `422` (BR-70) |
| AC-B16 | PAID | hoàn tác về DEPOSIT_PAID không có reason | `400`; có reason → OK + activity |
| AC-B17 | Settings chưa có ngân hàng | Send | `422` "Chưa cấu hình tài khoản nhận tiền" |
| AC-B18 | DRAFT ADDITIONAL có link revision | DELETE | Xóa OK; các revision item `quotation_item_id = null` |
| AC-B19 | SENT | DELETE | `409` |
| AC-B20 | Logo (service 2.000.000), Thiệp mời + Sticker (không service) | Tạo INITIAL với `deliverablePrices` Thiệp mời 450.000, Logo 1.800.000 | Dòng: Logo 1.800.000 · Thiệp mời 450.000 · Sticker 0; giá âm / > 100 tỷ / deliverable ngoài tập / `fromDeliverables=false` → `400 errors.deliverablePrices` |
| AC-B21 | DRAFT còn 1 dòng 0 ₫ "Menu A5" | Bấm "Gửi báo giá" | Hộp xác nhận nêu "1 dòng có đơn giá 0 ₫:" + "• Menu A5", nút "Vẫn gửi" (không chặn — BR-31) |

### AC-SCOPE — Scope guard (BR-60..62)

| ID | Given | When | Then |
|---|---|---|---|
| AC-S1 | Item CHARGEABLE chưa có báo giá | work-status IN_PROGRESS không reason | `428 SCOPE_GUARD` |
| AC-S2 | Như trên | Có `overrideReason` | OK; `scope_override_reason` lưu; activity `revision_item.scope_override` |
| AC-S3 | Item CHARGEABLE, báo giá SENT | work-status IN_PROGRESS | `428` |
| AC-S4 | Item CHARGEABLE, báo giá ACCEPTED | work-status IN_PROGRESS | OK, không cần reason |
| AC-S5 | Item đã link báo giá SENT | Đổi billing → IN_QUOTA | `409 BILLING_LOCKED` |
| AC-S6 | Item IN_QUOTA | work-status DONE | OK |

### AC-PRJ — Dự án & brief

| ID | Given | When | Then |
|---|---|---|---|
| AC-P1 | DRAFT | status → COMPLETED | `409 INVALID_TRANSITION` |
| AC-P2 | IN_PROGRESS | ON_HOLD không reason | `400` |
| AC-P3 | QUOTED, chưa có INITIAL ACCEPTED | → IN_PROGRESS, `confirmWarnings=false` | `422` + warnings; `true` → OK |
| AC-P4 | Brief v1 CONFIRMED | PUT content | `409 BRIEF_LOCKED`; SQL UPDATE trực tiếp bị trigger chặn |
| AC-P5 | Brief v1 CONFIRMED | Tạo version mới | v2 DRAFT, content = v1 |
| AC-P6 | Brief v2 DRAFT tồn tại | Tạo version mới | `409` |
| AC-P7 | Khách có dự án IN_PROGRESS | archive khách | `409 HAS_ACTIVE_PROJECTS` |

### AC-REQ — Request

| ID | Given | When | Then |
|---|---|---|---|
| AC-R1 | Khách A session | POST request cho A1 với 2 ảnh | 201, status NEW, `created_by=CLIENT`, `source=PORTAL`, 2 file `uploaded_by=CLIENT`; badge admin +1 |
| AC-R2 | A1 COMPLETED | Khách gửi request | `422` (BR-21) |
| AC-R3 | Request TRIAGED | Triage lại | `409` |
| AC-R4 | Decline với reason | Khách xem danh sách request | Thấy DECLINED + lý do |
| AC-R5 | Khách gửi request thứ 21 trong ngày | POST | `429` |
| AC-R6 | Upload `.html` / `.svg` / `.jpg` thực chất là exe | POST | `415` |
| AC-R7 | File 21 MB | POST | `413` |


### AC-STO — Lưu trữ ảnh (P10; BR-86, BR-88, BR-89)

| ID | Given | When | Then |
|---|---|---|---|
| AC-STO1 | Designer, ảnh 3200×800 | Upload vào task | original 3000×750, preview 2560×640, thumb 480×120; variant trả `Cache-Control` đúng loại (preview/thumb immutable, original `max-age=600`), ETag là validator duy nhất (`ImageVariantPolicyTests`) |
| AC-STO2 | Ảnh phẳng (sọc 2 màu) / ảnh nhiễu | `ImageProcessor.Process` | ảnh phẳng: original lossless (`VP8L`) nhỏ hơn lossy, pixel giữ nguyên; ảnh nhiễu: lossy, không thử lossless (`ImageProcessorTests`) |
| AC-STO3 | Khách gửi request kèm ảnh | Upload | chỉ lưu `original` + `thumb`; `preview_is_original = true`; portal `preview` trả đúng byte của original khách gửi, `max-age=600`; portal `original` 404 (`ImageVariantPolicyTests`) |
| AC-STO4 | Ảnh X đã có trong dự án | Upload lại X (task khác / tiến độ / cùng lần tải) | 201, ảnh vẫn lưu, `duplicateOf` trỏ bản sớm nhất (hoặc `sameUpload`); dự án khác: `null`; danh sách: `null` (`ImageVariantPolicyTests`) |
| AC-STO5 | Task có 2 ảnh, 1 ảnh là chứng từ báo giá | Xóa task | 204; ảnh thường xóa mềm + object mất + quota giảm; ảnh chứng từ còn; `task.deleted` có `imagesDeleted 1, imagesKept 1`; upload song song với xóa task không để lại ảnh mồ côi (`TaskImageCleanupTests`) |
| AC-STO6 | Object của row đã xóa, original thừa, mồ côi 25 h & 5 phút, `.tmp-` 30 h, key lạ, 1 thumb mất | Sweep | xóa object row đã xóa, original thừa, mồ côi 25 h, `.tmp-`; giữ mồ côi 5 phút và key lạ; `missingObjects 1`; quota không đổi; activity `storage.reconciled` chỉ khi có phát hiện; một bước lỗi không dừng bước khác (`StorageSweepTests`) |
| AC-STO7 | Dự án hoàn thành 7 tháng trước (1 ảnh thường, 1 chứng từ, 1 ảnh khách), dự án hoàn thành 1 tháng | Xem trước N=6 rồi xác nhận | xem trước 1 ảnh + `digest`; gửi số/digest sai → 409; xác nhận → original mất, preview/thumb còn, quota giảm đúng `freedBytes`, `/original` trả preview, `originalDeletedAt` có giá trị, activity `image.originals_deleted` (`fileCount`, `fileIds`) (`OriginalCleanupTests`) |
| AC-STO8 | Nhiều dự án có ảnh | `GET /api/admin/storage/projects` | sắp `totalBytes` giảm dần; dự án COMPLETED có `completedAt`; số ảnh/bytes/original đúng (`OriginalCleanupTests`) |
| AC-STO9 | Designer trên UI | Upload ảnh trùng, xem "Theo dự án", xóa task, xem trước dọn bản gốc | toast cảnh báo trùng; dòng dự án "2 ảnh"; sau xóa task không còn ảnh; dự án vừa hoàn thành không nằm trong danh sách dọn (`e2e/storage-optimization.spec.ts`) |

### AC-DSH — Dashboard nâng cấp (P11)

| ID | Tiêu chí | Kiểm bằng |
|---|---|---|
| AC-DSH-01 | `dashboard/schedule` trả đủ 7 ngày từ hôm nay; quá hạn tách riêng, cũ nhất trước; loại task DONE và dự án CANCELLED; cắt 20/ngày; sắp theo ưu tiên | `DashboardScheduleTests` |
| AC-DSH-02 | Tick DONE tại chỗ làm task biến mất ngay (optimistic), lỗi thì hoàn lại, có "Hoàn tác" | `scheduleBlock.test.tsx`, e2e |
| AC-DSH-03 | Sức khỏe dự án: rủi ro, điểm, thứ tự, dự án COMPLETED còn nợ có mặt, đã thu đủ thì không; chấm điểm mọi dự án trong phạm vi rồi mới cắt 12 dòng (`hidden`) | `DashboardProjectHealthTests`, `ProjectHealthTests` |
| AC-DSH-04 | Đã thu theo kỳ = cọc theo `DepositPaidAt` + phần còn lại theo `PaidAt`; so sánh cùng số ngày kỳ trước; `period` sai → 400 | `DashboardMoneyInsightsTests`, `DashboardPeriodsTests` |
| AC-DSH-05 | `receivables.total` = `money.outstanding`; tuổi nợ và bucket đúng; copy nhắc thanh toán đúng nội dung | `DashboardMoneyInsightsTests`, `moneyInsightsBlock.test.tsx` |
| AC-DSH-06 | Feed hoạt động chỉ gồm sự kiện do khách + báo giá được chấp nhận; link do server dựng; số truy vấn cố định | `DashboardClientActivityTests` |
| AC-DSH-07 | Checklist: 6 mục, hoàn thành theo dữ liệu thật (STUDIO cần tên khác tên mẫu và có SĐT/email; SERVICE cần dịch vụ có giá được tạo/sửa sau seed), tắt idempotent, ẩn khi xong | `DashboardSetupTests`, `setupChecklist.test.tsx` |
| AC-DSH-08 | Digest gửi tối đa 1 lần/ngày VN (kể cả chạy đồng thời), đúng giờ cấu hình, tắt thì im lặng, không có gì để báo thì không gửi; lỗi trước commit thì ngày được thử lại | `DailyDigestTests`, `DailyDigestMessageTests` |
| AC-DSH-09 | Cấu hình digest lưu/validate 0–23, cảnh báo khi chưa có webhook | `DigestSettingsTests`, `digestSection.test.tsx` |
| AC-DSH-10 | Không tràn ngang ở 390px; bảng/thanh có bản đọc được bằng chữ | e2e `dashboard-upgrade.spec.ts` + kiểm tay |

### AC-CF — Portal vòng 2 (P12; BR-76, BR-77, BR-97)

| ID | Given | When | Then | Test |
|---|---|---|---|---|
| AC-CF1 | Báo giá SENT của khách A | Khách bấm "Đồng ý báo giá" 2 lần | 200 cả hai; `status` vẫn SENT; 1 activity `quotation.client_accepted`; 1 Discord | `PortalQuotationStatementTests`, `QuotationClientStatementTests` |
| AC-CF2 | Báo giá ACCEPTED / hết hạn | Khách bấm "Đồng ý" | 409 `INVALID_TRANSITION` / `QUOTATION_EXPIRED` | như trên |
| AC-CF3 | Báo giá SENT cọc 50% | Khách báo đã chuyển `deposit` | 200, `clientPaymentNotice.pending = true`, `payment_status` vẫn UNPAID; `remaining` → 409 `PAYMENT_NOT_APPLICABLE`; `purpose` sai → 400 | như trên |
| AC-CF4 | Khách đã đồng ý + báo cọc | Designer mở dự án | 2 action đầu: `CONFIRM_CLIENT_ACCEPTANCE`, `CONFIRM_CLIENT_PAYMENT`; không còn `RECORD_ACCEPTANCE` | `ProjectFlowTests` |
| AC-CF5 | Sản phẩm IN_REVIEW | Khách duyệt | `reviewState = CLIENT_APPROVED`, `awaitingClient` giảm 1 | `PortalReviewStateTests`, `ClientReviewStateTests` |
| AC-CF6 | Sản phẩm IN_REVIEW có request NEW gắn nó (hoặc mục sửa TODO) | Khách duyệt | 409 `CHANGE_REQUEST_OPEN`; `reviewState = CHANGES_IN_PROGRESS` tới khi request bị từ chối hoặc mục sửa DONE | `PortalReviewStateTests` |
| AC-CF7 | Designer đặt "Thời gian phản hồi dự kiến" | Khách gửi yêu cầu | Màn tóm tắt hiện "Nhà thiết kế thường phản hồi {giá trị}"; trống → "trong 24 giờ"; > 60 ký tự → 400 | `PortalResponseTimeSettingsTests`, `portal-request-form.test.tsx` |
| AC-CF8 | Mobile 390px | Mở trang dự án/báo giá | Không cuộn ngang, không nút nổi che nội dung, nút chính ≥ 44px, lightbox phủ toàn màn hình | `portal-lightbox.test.tsx`, `portal-payment-block.test.tsx`, e2e `portal-round2.spec.ts` |
| AC-CF9 | Trigger khóa báo giá SENT | UPDATE bốn cột `client_*` / UPDATE `total` | cột `client_*` đổi được; `total`/`snapshot` bị `quotation_locked` | `PortalClientStatementLockTests`, `QuotationLockMigrationTests` |

### AC-TPL — Mẫu yêu cầu (BR-130..132)

| ID | Tiêu chí | Test |
|---|---|---|
| AC-TPL1 | Migration seed đúng 6 mẫu (Banner quảng cáo, Poster, Menu cửa hàng, Banner sự kiện / chương trình, Bài đăng mạng xã hội, Logo) với kích thước 1080×1080, 1200×628, 1080×1920, A4, A3, 60×160 cm | `RequestTemplateSchemaTests`, `RequestTemplateFieldTests`, `RequestTemplatesAdminTests` |
| AC-TPL2 | Designer tạo/sửa/sắp xếp/xoá mẫu, gắn dịch vụ; sửa chồng → 409 | `RequestTemplatesAdminTests`, e2e `request-templates.spec.ts` |
| AC-TPL3 | Portal chỉ thấy mẫu đang dùng, không thấy `serviceId`/`designType`/`active` | `PortalTemplateRequestTests`, `PortalIsolationTests` |
| AC-TPL4 | Yêu cầu theo mẫu lưu văn bản render + câu trả lời; hộp Yêu cầu hiện bảng câu trả lời; Discord có "Mẫu" | `PortalTemplateRequestTests`, e2e `portal-template-request.spec.ts` |
| AC-TPL5 | Câu trả lời sai/thiếu báo theo từng câu (server + client cùng thông điệp); không trả lời gì → `errors.answers`; câu ảnh bắt buộc là tuỳ chọn khi kho từ chối ảnh | `TemplateAnswersTests`, `requestTemplates.test.ts`, `PortalTemplateRequestTests` |
| AC-TPL6 | Xoá mẫu không đổi yêu cầu cũ | `RequestTemplateSchemaTests` |

### AC-INT — Form khách mới (BR-133..138)

| ID | Tiêu chí | Test |
|---|---|---|
| AC-INT1 | Form tắt mặc định; bật tạo link `/f/{slug}`; tắt → 404; bật lại giữ link; slug không vào activity; `/api/portal/me` có `designer.intakeUrl` khi bật, `null` khi tắt | `IntakeLinkSettingsTests`, `PortalIntakeLinkTests` |
| AC-INT2 | "Tạo link mới" → link cũ 404, biểu mẫu của link cũ → `FORM_EXPIRED` | `IntakeLinkSettingsTests`, `PublicIntakeAbuseTests` |
| AC-INT3 | GET công khai chỉ có liên hệ studio + mẫu đang dùng; `no-store`, `noindex`; trang `/f/*` `no-referrer` | `PublicIntakeTests`, e2e `public-intake.spec.ts` |
| AC-INT4 | Gửi hợp lệ → `204` rỗng; hồ sơ NEW + ảnh `INTAKE_SUBMISSION` không dự án; Discord chỉ tên khách + tên mẫu | `PublicIntakeTests` |
| AC-INT5 | Honeypot / điền quá nhanh → `204` không lưu; token sai/hết hạn/của link cũ/đã dùng → `FORM_EXPIRED`; token được trả lại khi hồ sơ không được lưu | `PublicIntakeAbuseTests` |
| AC-INT6 | `POST` 5/giờ/IP (`429`), 5 hồ sơ/ngày/IP và trần 30/ngày studio (`429 RATE_LIMITED`; IPv6 theo /64), body > `MaxUploadMb × 3 + 1` MB (`413`), kho ≥ 80 % từ chối ảnh (`507`) nhưng nhận chữ; cookie designer bị bỏ qua, không cần CSRF | `PublicIntakeAbuseTests`, `PublicIntakeDailyCapTests`, `PublicIntakeTests` |
| AC-INT7 | Hộp Khách mới: danh sách, badge, chi tiết (câu trả lời, ảnh, khách trùng SĐT/email) | `IntakeInboxTests`, e2e `intake.spec.ts` |
| AC-INT8 | Từ chối giữ lý do nội bộ và chặn chuyển đổi; xoá hồ sơ xoá mềm ảnh; xoá đua với chuyển đổi → `409` không mất dữ liệu | `IntakeInboxTests`, e2e `intake.spec.ts` |
| AC-INT9 | Một lần bấm tạo khách + dự án DRAFT + brief v1 + hạng mục từ dịch vụ + gắn ảnh; khách có sẵn không bị nhân đôi; lần 2 / bản cũ → 409 | `IntakeConvertTests`, e2e `intake.spec.ts` |
| AC-INT10 | API từ chối khởi động ngoài Development/Testing khi thiếu `DataProtection:KeysPath`; hai host dùng chung thư mục key đọc được token của nhau | `DataProtectionKeysTests` |

### AC-UX14 — Designer UX vòng 2 (P14; BR-139..145)

| ID | Kịch bản | Kết quả | Test |
|---|---|---|---|
| AC-UX14-01 | Bấm "Gửi duyệt" khi còn task mở | Hộp xác nhận liệt kê task mở; "Vẫn gửi duyệt" vẫn thực hiện được | `deliverableActionDialogs.test.tsx`, e2e `designer-ux-round2.spec.ts` |
| AC-UX14-02 | "Bàn giao" kèm link `https://drive…` + lời nhắn | Timeline portal có lời nhắn và link bấm được (`rel=noopener`) | `DeliveryLinkTests`, e2e `designer-ux-round2.spec.ts` |
| AC-UX14-03 | "Bàn giao" với `javascript:alert(1)` | Lỗi inline, không gọi API (FE); `400 VALIDATION_FAILED` `errors.deliveryUrl` (API) | `deliverableActionDialogs.test.tsx`, `DeliveryLinkTests`, e2e `designer-ux-round2.spec.ts`, `task-notes.spec.ts` |
| AC-UX14-04 | Task hạn trước ngày bắt đầu | `400 errors.dueDate`; drawer báo lỗi trên ô hạn | `TaskDatesTests`, `TaskTests`, `taskSchema.test.ts` |
| AC-UX14-05 | Dự án đã hoàn thành còn task mở | Không gợi ý "chuyển về Đang làm"; có info `COMPLETED_WITH_OPEN_ITEMS` "còn n công việc…" | `ProjectFlowTests`, `ProjectCompletionWarningsTests` |
| AC-UX14-06 | Báo giá được chấp nhận, dự án còn DRAFT | Badge "Sẵn sàng bắt đầu", nút chính "Bắt đầu dự án" | `flowUtils.test.ts`, e2e `designer-ux-round2.spec.ts` |
| AC-UX14-07 | Dự án 0 hạng mục | Không có "Tạo báo giá" | `ProjectFlowTests`, e2e `project-flow.spec.ts` |
| AC-UX14-08 | Chưa nhận cọc | Có nút "Đánh dấu đã nhận cọc" → báo giá `DEPOSIT_PAID` | `ProjectFlowTests`, e2e `designer-ux-round2.spec.ts` |
| AC-UX14-09 | Tải ảnh trong task | Mặc định "Khách xem được" tắt | `taskImages.test.tsx` |
| AC-UX14-10 | Bắt đầu dự án với hạn chung | Mọi task mẫu của hạng mục có hạn đó (`startDate` trống) | `ProjectFlowApiTests`, `startProject.test.ts`, e2e `designer-ux-round2.spec.ts` |
| AC-UX14-11 | Tách yêu cầu 4 dòng | 4 mục sửa điền sẵn | kiểm tay (`RequestSheet`/`RoundEditor`) |
| AC-UX14-12 | Brief `## Mục tiêu` | Kết xuất heading ở trang Brief admin (Portal hiện văn bản thuần) | `briefTab.test.tsx` |
| AC-UX14-13 | Ô tiền | Hiện `2.000.000` khi đang gõ; dán `2,500,000` = 2.500.000 | `moneyInput.test.ts`, `moneyInputComponent.test.tsx` |
| AC-UX14-14 | Viewport 390px | Không tràn ngang ở Tổng quan, Công việc, Dịch vụ, chi tiết dự án | e2e `designer-ux-round2.spec.ts` |
| AC-UX14-15 | Ctrl+K "phac thao" | Có nhóm Công việc (và Yêu cầu theo nội dung) | `SearchTests`, `searchUtils.test.ts`, e2e `designer-ux-round2.spec.ts` |
| AC-UX14-16 | Kanban 1440px | Cột "Bị chặn" nằm trọn trong khung; thẻ không hiện nhãn "Trung bình" | `taskMeta.test.tsx`, e2e `designer-ux-round2.spec.ts` |

## 3. Luồng E2E (Playwright)

1. **Onboarding:** đăng nhập → cấu hình ngân hàng → tạo dịch vụ → tạo khách → tạo link portal.
2. **Dự án mới:** tạo dự án + 2 deliverable từ service → viết & confirm brief → báo giá INITIAL → gửi →
   đánh dấu ACCEPTED → DEPOSIT_PAID → dự án IN_PROGRESS.
3. **Revision vượt quota:** khách (context trình duyệt riêng, mở magic link) gửi 4 request cho Logo →
   designer triage từng cái → request thứ 4 gợi ý tính phí → tạo báo giá phát sinh → gửi →
   khách thấy báo giá + QR đúng số tiền.
4. **Version báo giá:** báo giá SENT → tạo version mới, sửa giá → gửi → portal hiển thị v2, v1 ở lịch sử.
5. **Thu hồi link:** designer tạo link mới → session khách cũ bị 401 → link mới vào được.
6. **Dashboard nâng cấp (P11, `dashboard-upgrade.spec.ts`):** tick task quá hạn từ lịch 7 ngày rồi Hoàn tác; mở TaskDrawer từ lịch; đổi kỳ tiền đã thu và copy nhắc thanh toán; widget dung lượng dẫn tới trang Dung lượng; ẩn checklist (bỏ qua nếu DB dev đã xong/đã ẩn); lưu giờ digest; không tràn ngang ở 390px.
7. **Báo giá phát sinh có giá (P09, `quotation-pricing.spec.ts`):** định giá báo giá phát sinh qua dialog → chấp nhận → hạng mục mới trong Deliverables (AC-Q9, AC-Q11); giá deliverable không dịch vụ + Chọn từ dịch vụ + xác nhận 0 ₫ (AC-B20, AC-B21).
8. **Dung lượng ảnh (P10, `storage-optimization.spec.ts`):** upload ảnh trùng → cảnh báo; Cài đặt › Dung lượng › Theo dự án; xóa task → ảnh của task mất; xem trước xóa bản gốc của dự án đã hoàn thành (AC-STO9).
9. **Portal vòng 2 (P12):** khách đồng ý báo giá + báo đã chuyển cọc, designer thấy hai bước xác nhận và ghi chú của khách (AC-CF1, CF3, CF4; `portal-quotation-request.spec.ts` › "client agrees to a sent quotation …"); một giọng trạng thái duyệt qua Duyệt và Yêu cầu sửa kèm ảnh, thời gian phản hồi, không nút nổi (AC-CF5..8; `portal-round2.spec.ts`). Chữ trên các spec `portal*.spec.ts` theo bảng thuật ngữ Portal (08 §3.4).
10. **Mẫu yêu cầu & khách mới (P13):** khách mới điền Poster qua `/f/{slug}` (có kịch bản `FORM_EXPIRED` không mất chữ đã gõ) rồi cảm ơn (`public-intake.spec.ts`); designer chuyển hồ sơ thành khách + dự án + brief hoặc từ chối/xoá (`intake.spec.ts`); khách portal gửi Poster theo mẫu và designer thấy bảng câu trả lời (`portal-template-request.spec.ts`); designer tạo/sửa/xoá mẫu và quản lý link (`request-templates.spec.ts`). Spec công khai dùng 1 lượt/IP/ngày trong trần 5 (BR-137) - chạy > 5 lần/ngày trên cùng máy dev sẽ nhận `429`.
11. **Designer UX vòng 2 (P14, `designer-ux-round2.spec.ts`):** dự án đã báo giá hiện "Sẵn sàng bắt đầu" → bắt đầu với hạn chung → "Xem công việc"/"Đánh dấu đã nhận cọc" → gửi duyệt khi còn việc ("Vẫn gửi duyệt") → xác nhận đã duyệt → bàn giao với link sai rồi link đúng + lời nhắn, khách thấy trên timeline; Kanban 1440 px, không tràn ngang ở 390 px, Ctrl+K tìm công việc không dấu. Các spec `project-flow`, `project-full-flow`, `deliverables` bấm qua hộp xác nhận mới.

## 4. Test cô lập Portal (bắt buộc, tự động sinh)

Với **mỗi** endpoint Portal có tham số id (project, quotation, file, request, version…):

```text
Given khách A có tài nguyên X; khách B đã đăng nhập portal
When  B gọi endpoint với id của X
Then  404, body không chứa bất kỳ trường nào của X
```

Thêm:
- Portal list endpoints của B không trả phần tử nào thuộc A.
- B không xem được báo giá DRAFT của chính B; không xem được file `visible_to_client=false`.
- Portal response không chứa khóa JSON `internalNotes`, `notes`, `scopeOverrideReason` (test snapshot schema).
- Portal bị tắt / khách archived / token version cũ → 401.
- Request portal không có header CSRF → 403.
- Route mới `GET /api/portal/request-templates` (P13) có trong `RouteTable` - dữ liệu chung của studio, không có tài nguyên theo khách. `/api/public/*` nằm ngoài `/api/portal` và được kiểm riêng (AC-INT3..6).
- Route Portal ghi mới của P12 có trong bảng isolation (`RouteTable`): `POST /api/portal/quotations/{id}/accept` và `.../payment-notice` (khách B với id báo giá của A → 404, không đổi cột `client_*` của A); `POST /api/portal/deliverables/{id}/approve` cũng vậy.

Triển khai: một test `[Theory]` lấy danh sách route từ `EndpointDataSource`, lọc prefix `/api/portal`,
và **fail nếu có route mới chưa được khai báo trong bảng isolation-case** — buộc mọi endpoint mới có test.

## 5. Test bất biến DB (integration)

- UPDATE `quotations.total` khi status ≠ DRAFT → exception.
- INSERT `quotation_items` vào báo giá SENT → exception.
- UPDATE/DELETE `activity_logs` → exception.
- UPDATE `project_briefs` CONFIRMED → exception.
- 2 báo giá cùng `number` ở SENT → unique violation.

## 6. Kiểm thử thủ công trước go-live

- [ ] Quét QR (cọc/toàn bộ/còn lại) bằng 3 app ngân hàng, chuyển thử khoản nhỏ.
- [ ] Portal trên iPhone Safari & Android Chrome: mở link từ Zalo (in-app browser của Zalo), upload ảnh từ camera.
- [ ] Checklist bảo mật 07 §9.
- [ ] Backup → restore trên máy khác → dữ liệu và file đầy đủ.
