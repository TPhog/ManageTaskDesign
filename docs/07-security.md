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

### 4.4 Phân quyền dữ liệu (bắt buộc)

- Mọi handler Portal nhận `PortalPrincipal { ClientId }` qua DI; **không bao giờ** nhận `clientId` từ request.
- Mọi query bắt đầu từ `db.Projects.Where(p => p.ClientId == principal.ClientId && p.ArchivedAt == null)` rồi
  mới đi xuống (deliverables, quotations, files, requests…). Lấy theo `id` → luôn join ngược tới `project.client_id`.
- Tài nguyên không thuộc khách → **404** (không 403).
- Lọc thêm: quotations `status NOT IN ('DRAFT','SUPERSEDED')` (trừ endpoint lịch sử version của chain đã có bản hiển thị);
  files `visible_to_client OR uploaded_by = 'CLIENT'` và `deleted_at IS NULL`; activity `visible_to_client`.
- DTO Portal là **class riêng** (không tái dùng DTO admin) để không vô tình lộ `internalNotes`, `notes`, `status_reason` khi không public.
- Test integration bắt buộc cho **mọi** endpoint Portal: khách B gọi với id tài nguyên của khách A → 404 (09-test-plan §4).

## 5. Upload file

| Kiểm tra | Giá trị |
|---|---|
| Kích thước | ≤ 20 MB/file (Kestrel `MaxRequestBodySize` 220 MB cho 10 file); proxy cùng giới hạn |
| Số file | ≤ 10/request (portal), ≤ 20 (admin) |
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
- [ ] Không thấy token portal trong log proxy/app.
- [ ] Backup chạy và restore thử thành công.
- [ ] `dotnet list package --vulnerable` và `npm audit` không có lỗi High/Critical.
