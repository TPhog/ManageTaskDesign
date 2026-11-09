# 07 — Security & Access Control

## 1. Mô hình đe dọa (tóm tắt)

| Tài sản | Mối đe dọa | Biện pháp chính |
|---|---|---|
| Tài khoản designer | Brute-force, đánh cắp session | Hash mật khẩu, lockout, rate limit, cookie HttpOnly/Strict |
| Dữ liệu khách A | Khách B (hoặc người có link) xem được | Scope mọi truy vấn theo `client_id` từ session; 404 cho tài nguyên ngoài scope |
| Magic link | Bị lộ (chuyển tiếp tin nhắn, log) | Token 256-bit, chỉ lưu hash, thu hồi được, đổi token khỏi URL ngay sau khi mở |
| Báo giá đã gửi | Bị sửa âm thầm | Domain + DB trigger khóa, activity append-only |
| File upload | Mã độc, XSS qua SVG/HTML, path traversal | Whitelist, kiểm magic bytes, lưu tên ngẫu nhiên, `nosniff`, không phục vụ trực tiếp từ disk |
| Server | Lộ secret, mất dữ liệu | Secret qua env, HTTPS, backup off-site |

## 2. Xác thực designer

- Một tài khoản duy nhất, seed lần đầu từ env `Seed__DesignerEmail/Password` (≥ 12 ký tự).
- Hash: `PasswordHasher<DesignerAccount>` (PBKDF2-HMAC-SHA512, 100k+ vòng — mặc định của ASP.NET Core).
- Lockout: 5 lần sai liên tiếp → khóa 15 phút (`failed_logins`, `lockout_until`).
- Rate limit `/api/auth/login`: 5 request/phút/IP (ASP.NET Core `RateLimiter`, fixed window).
- Cookie `ff_admin`: HttpOnly, Secure, SameSite=**Strict**, Path=`/`, hết hạn trượt 14 ngày.
  Claim chứa `security_stamp`; mỗi request đối chiếu (cache 5 phút) → đổi mật khẩu = đăng xuất mọi nơi.
- Phase 2: TOTP (2FA).

## 3. CSRF

- Cookie auth ⇒ cần chống CSRF cho mọi lệnh thay đổi dữ liệu.
- Double-submit: server set cookie `ff_csrf` (không HttpOnly, SameSite=Strict, random 128-bit) khi login/tạo
  session portal; SPA đọc và gửi header `X-CSRF-Token`; middleware so sánh constant-time.
- Kèm kiểm tra `Origin`/`Referer` = `App__PublicBaseUrl` cho POST/PUT/PATCH/DELETE.
- Không có endpoint thay đổi dữ liệu bằng GET.
- **Ngoại lệ có chủ đích:** tiền tố `/api/public/*` (form khách mới, §4.5) không kiểm CSRF - ẩn danh, không có phiên để giả mạo, và trang `/f/*` dùng `Referrer-Policy: no-referrer` nên trình duyệt gửi `Origin: null`. Mọi endpoint dưới `/api/public` **phải** giữ ẩn danh và không đọc `HttpContext.User` (có chú thích GUARD trong `CsrfMiddleware`).

## 4. Client Portal — magic link

### 4.1 Tạo token

```text
token      = base64url(RandomNumberGenerator.GetBytes(32))      // 43 ký tự, 256 bit
hash       = SHA-256(token)                                     // lưu clients.portal_token_hash
version    = clients.portal_token_version + 1
URL        = {PublicBaseUrl}/p/{token}
```

- Token **chỉ hiển thị một lần** cho designer (copy để gửi qua Zalo). DB chỉ có hash.
- "Tạo link mới" → hash mới + version mới ⇒ link cũ và mọi session cũ vô hiệu (BR-12).

### 4.2 Đổi token lấy session

1. Khách mở `/p/{token}` → SPA gọi `POST /api/portal/session { token }`.
2. Server: tính hash → tìm client với `portal_enabled = true`, `archived_at is null` → so sánh
   `CryptographicOperations.FixedTimeEquals`.
3. Thành công → cookie `ff_portal` (HttpOnly, Secure, SameSite=**Lax**, 30 ngày trượt) chứa
   `client_id` + `portal_token_version`. SPA `history.replaceState` sang `/portal` để token rời khỏi thanh địa chỉ.
4. Mỗi request portal: đọc client theo `client_id` (cache ngắn 60s), kiểm `portal_enabled`, `archived_at`,
   `portal_token_version` khớp → nếu không: 401, xóa cookie.
5. Sai token → `401` thông điệp chung "Liên kết không hợp lệ hoặc đã hết hạn", rate limit 10/phút/IP.

### 4.2.1 Endpoint công khai `GET /api/portal/studio`

- `AllowAnonymous` có chủ đích: trang đăng xuất / link hết hạn / link sai cần hiện cách liên hệ studio.
- Chỉ trả **3 trường**: `displayName`, `contactPhone`, `contactEmail` — cùng dữ liệu đã in trên mọi báo giá;
  tuyệt đối không thêm dữ liệu khách, ngân hàng hay cài đặt khác (có test khóa danh sách trường).
- Rate limit cố định 60 request/phút/IP (`RateLimit:PortalStudioPerMinute`, nới ở Development) → `429`.

### 4.3 Header bảo vệ link

- Trang `/p/*`: `Referrer-Policy: no-referrer`, không tải tài nguyên bên thứ ba (tránh rò token qua Referer).
- Reverse proxy **không log** path `/p/*` đầy đủ (Caddy: `log` filter hoặc ghi `uri` đã che).
- Serilog: không log body `POST /api/portal/session`.
- Trang `/f/*` (form khách mới): `Referrer-Policy: no-referrer`, `X-Robots-Tag: noindex`; Caddy `log_skip` cho cả `/f/*` và `/api/public/*` (path chứa slug) - `deploy/Caddyfile`; Vite dev server đặt `Referrer-Policy` cho `/p/*` và `/f/*`. Slug ở môi trường Development có thể xuất hiện trong log request của ASP.NET (production chỉ log Warning trở lên).

### 4.4 Phân quyền dữ liệu (bắt buộc)

- Mọi handler Portal nhận `PortalPrincipal { ClientId }` qua DI; **không bao giờ** nhận `clientId` từ request.
- Mọi query bắt đầu từ `db.Projects.Where(p => p.ClientId == principal.ClientId && p.ArchivedAt == null)` rồi
  mới đi xuống (deliverables, quotations, files, requests…). Lấy theo `id` → luôn join ngược tới `project.client_id`.
- Tài nguyên không thuộc khách → **404** (không 403).
- Lọc thêm: quotations `status NOT IN ('DRAFT','SUPERSEDED')` (trừ endpoint lịch sử version của chain đã có bản hiển thị);
  files `visible_to_client OR uploaded_by = 'CLIENT'` và `deleted_at IS NULL`; activity `visible_to_client`.
- **Link bàn giao (Plan 14, BR-140):** chỉ nhận URL tuyệt đối `http`/`https` (chặn `javascript:`, `data:`, `file:`… ở API; không khoảng trắng; ≤ 500 ký tự), hiển thị trên Portal qua `linkify` và trên admin qua `LinkifiedText` (`safeHref`): chỉ http(s) thành thẻ `<a>`, `target="_blank"`, `rel="noopener noreferrer"`, `referrerPolicy="no-referrer"` (admin). URL không được ghi vào log ứng dụng (chỉ nằm trong `activity_logs.data.deliveryUrl` và `client_message`). Brief Markdown ở trang admin render bằng `react-markdown` với `skipHtml` và `urlTransform = safeHref` (không HTML thô, không `javascript:`); Portal hiển thị brief dưới dạng văn bản thuần.
- DTO Portal là **class riêng** (không tái dùng DTO admin) để không vô tình lộ `internalNotes`, `notes`, `status_reason` khi không public.
- **Liên kết ngoài do người dùng nhập (Plan 15, BR-148):** task và hạng mục lưu tối đa 10 liên kết; API chỉ nhận URL tuyệt đối `http`/`https` (chặn `javascript:`, `data:`, `file:`…, userinfo, khoảng trắng, > 2000 ký tự), lưu dạng `AbsoluteUri`, DB check `ck_tasks_links` / `ck_deliverables_links`. Giao diện luôn render `<a target="_blank" rel="noopener noreferrer">` qua `safeHref`; Portal đã có `Referrer-Policy: no-referrer` (§4.3). URL **không** vào log ứng dụng hay `activity_logs` (chỉ số lượng). Link của task không bao giờ ra Portal (TSK-05); link hạng mục chỉ ra khi designer bật `visibleToClient`, qua DTO Portal riêng (`PortalLinkDto { title, url }`, không lộ cờ). **Không có endpoint ẩn danh mới và không có route Portal mới** - `GET /api/portal/projects/{id}` chỉ có thêm trường `links` trong từng hạng mục.
- Test integration bắt buộc cho **mọi** endpoint Portal: khách B gọi với id tài nguyên của khách A → 404 (09-test-plan §4).
- **Lệnh ghi của khách (Plan 12):** `POST /api/portal/quotations/{id}/accept` và `.../payment-notice`, `POST /api/portal/deliverables/{id}/approve` đều cần CSRF (§3), scope theo `PortalPrincipal.ClientId` (ngoài phạm vi → `404`), idempotent, và **chỉ ghi các cột `client_*` / `client_decision`** - không đổi `status`, `payment_status` hay tiền (BR-01, BR-76/77). Ghi chú của khách ≤ 500 ký tự được lưu nhưng **không** đi vào log hay `activity_logs.data` (chỉ `hasNote`); Discord cắt còn 200 ký tự. Trigger `fn_quotation_locked` là fail-closed: chỉ whitelist đúng 4 cột `client_*` mới (04 §2), mọi cột nội dung khác vẫn bị khóa.

### 4.5 Form công khai cho khách mới `/f/{slug}` (Plan 13)

- Endpoint: `GET`/`POST /api/public/intake/{slug}` - `AllowAnonymous` có chủ đích. Handler **không** đọc `HttpContext.User` và không inject `PortalPrincipal`: cookie `ff_admin`/`ff_portal` đi kèm bị bỏ qua (test `Designer_cookie_is_ignored…`).
- Slug 128-bit ngẫu nhiên (`RandomNumberGenerator`), không phải bí mật tuyệt đối (designer đăng công khai) nên lưu nguyên văn; "Tạo link mới" thay slug. Slug sai / form tắt → cùng một `404`. Activity không ghi slug.
- Giới hạn (BR-137):
  - rate limit theo IP (IPv6 theo /64, `ClientIpKey`): `GET` 10/phút (`RateLimit:PublicIntakePerMinute`, Development 120) và `POST` 5/giờ cửa sổ trượt (`RateLimit:PublicIntakeSubmitPerHour`, Development 60) → `429`;
  - trần **30 hồ sơ lưu/ngày VN cho cả studio** (đếm lại dưới `pg_advisory_xact_lock` trong transaction ghi) và **5 hồ sơ lưu/ngày VN/IP** (`PublicIntake:MaxSubmissionsPerIpPerDay`, đếm trong bộ nhớ) → `429 RATE_LIMITED`;
  - body ≤ `App:MaxUploadMb × 3 + 1` MB (`ImageLimits.PublicIntakeBodyBytes`, mặc định 61 MB) → `413`; ≤ 3 ảnh, kiểm magic bytes như §5 → `415`;
  - kho ≥ **80 %** → từ chối ảnh `507 STORAGE_FULL`, vẫn nhận phần chữ; câu ảnh bắt buộc thành tuỳ chọn (BR-131, BR-135).
- Chống bot (BR-138): honeypot `website` + thời gian điền tối thiểu `PublicIntake:MinFillSeconds` (3 s) đo bằng `formToken` = Data Protection `Protect("{slug}|{issuedAtUnixMs}|{id}")`, purpose `FreelanceFlow.PublicIntake.FormToken.v1`, hiệu lực 24 h, **dùng một lần** cho một hồ sơ được lưu (id đã dùng nằm trong bộ nhớ, trả lại khi hồ sơ không được lưu). Bot nhận `204` giống người thật và không có dòng nào được ghi.
- **Data Protection keys phải được lưu bền:** `DataProtection:KeysPath` (production `/data/keys`, volume `keys`; Development `./data/keys`). API **từ chối khởi động** ngoài Development/Testing nếu thiếu cấu hình này. Nếu key ring mất, mọi cookie đăng nhập/portal và biểu mẫu đang mở mất hiệu lực (khách nhận `FORM_EXPIRED` và biểu mẫu tự tải lại token).
- Không echo: POST thành công `204` rỗng; lỗi validation chỉ có thông điệp theo trường. GET chỉ trả `displayName`, `contactPhone`, `contactEmail` của studio + mẫu đang bật (không `serviceId`, `designType`, ngân hàng, dữ liệu khách); `Cache-Control: no-store`.
- Không log body, tên, SĐT, email, slug, `formToken`, địa chỉ IP (chỉ dùng làm khoá trong bộ nhớ). Activity `intake.submitted` chỉ có `templateId`, `imageCount`. Discord chỉ nhận **tên khách + tên mẫu** (không SĐT/email/câu trả lời/ghi chú).
- Cổng quản trị: hộp Khách mới và mẫu yêu cầu chỉ dành cho designer (`/api/admin/*`).

## 5. Upload file

| Kiểm tra | Giá trị |
|---|---|
| Kích thước | ≤ 20 MB/file (Kestrel `MaxRequestBodySize` 220 MB cho 10 file); proxy cùng giới hạn |
| Số file | ≤ 5/request (portal, Plan 12), ≤ 3 ảnh (form khách mới, §4.5, BR-135), ≤ 20 (admin) |
| Định dạng cho khách upload | `jpg, jpeg, png, webp, gif, pdf` |
| Định dạng cho designer upload | trên + `zip, ai, psd, fig, eps, svg, mp4` |
| Kiểm tra | Phần mở rộng **và** magic bytes khớp; MIME từ server, không tin header client |
| Lưu trữ | `storage_key = yyyy/MM/{uuid}` (không dùng tên gốc); thư mục ngoài web root |
| Phục vụ | Qua API sau kiểm quyền; header `X-Content-Type-Options: nosniff`; `Content-Disposition: attachment` cho mọi loại **trừ** ảnh raster & PDF (inline) |
| Cache ảnh | Portal: `private, max-age=600` (ẩn/xóa/đăng xuất phải có hiệu lực sớm trên máy dùng chung). Admin preview/thumb: `private, max-age=31536000, immutable` (object bất biến). Admin original: `private, max-age=600` (có thể chuyển sang preview sau khi dọn bản gốc). ETag là validator duy nhất, không `Last-Modified`; 404 không cache |
| Biến thể ảnh | Designer: original ≤ 3000 px (chỉ designer), preview ≤ 2560 px có watermark, thumb 480 px. Khách không bao giờ nhận object `original` của designer (portal chặn `original`; DB `ck_files_preview_alias` chỉ cho alias preview→original ở ảnh `uploaded_by = CLIENT`) |
| Dọn kho | Job hằng ngày chỉ xóa key đúng dạng ảnh của app, không bao giờ xóa key lạ; object không có row chỉ xóa sau 24 h; chỉ ghi `activity_logs` `storage.reconciled` (kèm số đếm) khi có phát hiện/thay đổi. Dọn bản gốc là thao tác thủ công của designer, khớp bản xem trước bằng digest (xem BR-89) |
| SVG | Chỉ designer upload; luôn `attachment` (không render inline → tránh XSS) |
| Tên file | `original_name` được sanitize khi đưa vào `Content-Disposition` (RFC 5987 `filename*`) |
| Quota | Cảnh báo khi tổng dung lượng > 80% `App__StorageQuotaGb` |

Phase 2: quét virus (ClamAV) cho file khách upload.

## 6. HTTP security headers (áp dụng toàn site)

```text
Strict-Transport-Security: max-age=31536000; includeSubDomains
Content-Security-Policy: default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline';
                         script-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'
X-Content-Type-Options: nosniff
Referrer-Policy: strict-origin-when-cross-origin      (/p/* : no-referrer)
Permissions-Policy: camera=(), microphone=(), geolocation=()
```

## 7. Bí mật & cấu hình

- Secret chỉ ở `.env` trên server (quyền 600), không commit. Repo có `.env.example`.
- Data Protection keys trong volume `/data/keys`, có trong backup.
- PostgreSQL không mở port ra Internet (chỉ network nội bộ Docker).

### 7.1 Dashboard và digest (P11)

- Mọi endpoint `/api/admin/dashboard/*` và `/api/admin/settings/digest` nằm sau cookie `Designer`; không có endpoint nào ở Portal. Các `GET` thuần đọc; `POST dashboard/setup/dismiss` và `PUT settings/digest` cần CSRF header.
- Tin digest không chứa webhook URL, token portal, thông tin tài khoản ngân hàng hay nội dung brief; chỉ tiêu đề task, tên dự án, số báo giá, hạn và tổng công nợ.
- `GET /api/admin/settings/digest` chỉ trả `channelConfigured` (boolean), không bao giờ trả webhook URL; webhook không được log.
- `client-activity` dựng `href` ở server từ id nội bộ, frontend không ghép đường dẫn từ dữ liệu khách nhập.

## 8. Dữ liệu cá nhân

- Chỉ thu thập: tên, công ty, SĐT, email, kênh liên hệ của khách — do designer nhập.
- Không lưu IP/user-agent trong DB. Log reverse proxy giữ 30 ngày.
- Khách yêu cầu xóa dữ liệu: designer archive khách + (Phase 2) công cụ ẩn danh hóa (thay thông tin liên hệ bằng
  `[đã xóa]`, giữ báo giá/activity vì giá trị đối chiếu tài chính).
- Đối chiếu nghĩa vụ theo Luật Bảo vệ dữ liệu cá nhân hiện hành trước khi vận hành thương mại.

## 9. Checklist bảo mật trước go-live

- [ ] HTTPS + HSTS hoạt động, HTTP redirect.
- [ ] Đã xóa `Seed__DesignerPassword` khỏi `.env` sau lần chạy đầu.
- [ ] Test isolation Portal pass 100%.
- [ ] Rate limit login & portal session hoạt động (thử thủ công).
- [ ] Upload file `.html`, `.svg` (từ portal), file đổi đuôi → bị từ chối.
- [ ] `/f/*` có `Referrer-Policy: no-referrer` + `X-Robots-Tag: noindex`; Caddy `log_skip` cho `/f/*` và `/api/public/*`.
- [ ] `DataProtection__KeysPath` trỏ vào volume bền (`/data/keys`); đã kiểm tra cookie còn hiệu lực sau khi restart API.
- [ ] `RateLimit__PublicIntakeSubmitPerHour` và `PublicIntake__MaxSubmissionsPerIpPerDay` đặt cho production (mặc định 5 và 5); `RateLimit__PublicIntakePerMinute` mặc định 10.
- [ ] Form khách mới tắt nếu không dùng.
- [ ] Không thấy token portal trong log proxy/app.
- [ ] Backup chạy và restore thử thành công.
- [ ] `dotnet list package --vulnerable` và `npm audit` không có lỗi High/Critical.
