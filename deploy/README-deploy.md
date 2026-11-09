# Deploy FreelanceFlow lên Ubuntu (không Docker)

| File | Vai trò |
|---|---|
| `deploy/freelanceflow.conf.example` | Mẫu file cấu hình **duy nhất** (cả 2 script cùng đọc). Copy thành `deploy/freelanceflow.conf` |
| `deploy/deploy-windows.ps1` | Chạy trên Windows: build SPA + publish API → đóng gói `.tar.gz` → scp → chạy script server qua SSH |
| `deploy/setup-ubuntu.sh` | Chạy trên server (root): cài đặt + cấu hình mọi thứ, deploy/rollback/backup/status/logs. Sau setup có lệnh `sudo freelanceflow ...` |

Bộ Docker cũ (`docker-compose.yml`, `deploy/Caddyfile`) vẫn giữ nguyên; đây là phương án thay thế.

## 1. Chuẩn bị

**Server**: Ubuntu **22.04 hoặc 24.04**, x86_64, RAM ≥ 1 GB (khuyến nghị 2 GB), ổ 50 GB, mới cài, có user SSH với quyền `sudo`.

**DNS**: tạo bản ghi **A** `app.tenban.com → IP server` (đợi vài phút cho DNS cập nhật). Không cần DNS nếu chỉ thử ở chế độ HTTP (mục 3).

**SSH bằng key** (trên Windows, PowerShell):

```powershell
ssh-keygen -t ed25519                       # nếu chưa có key (Enter để chọn mặc định)
type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh ubuntu@IP_SERVER "mkdir -p ~/.ssh && chmod 700 ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys"
ssh ubuntu@IP_SERVER "sudo -v && echo ok"   # phải in ra ok
```

**Máy Windows**: .NET SDK 9 trở lên, Node.js 20+ (có npm), OpenSSH Client (có sẵn trên Windows 10/11; nếu thiếu: Settings → Apps → Optional features → OpenSSH Client). `tar.exe` có sẵn từ Windows 10 1803.

## 2. Deploy lần đầu

```powershell
cd C:\duong\dan\ManageTaskDesign
copy deploy\freelanceflow.conf.example deploy\freelanceflow.conf
notepad deploy\freelanceflow.conf          # điền SSH_HOST, SSH_USER, DOMAIN, LETSENCRYPT_EMAIL
powershell -ExecutionPolicy Bypass -File deploy\deploy-windows.ps1 -FirstTime
```

Script sẽ build, upload, rồi chạy `sudo bash setup-ubuntu.sh setup` (sudo có thể hỏi mật khẩu). Setup làm theo thứ tự: `apt upgrade` → gói nền → timezone → swap (nếu RAM < 4 GB và chưa có swap) → ufw (chỉ SSH, 80, 443) → fail2ban (sshd) → unattended-upgrades → ASP.NET Core 9 (`ppa:dotnet/backports`; dự phòng `dotnet-install.sh`) → PostgreSQL 17 (repo PGDG, chỉ nghe localhost, role + DB + `unaccent`) → user `freelanceflow` + thư mục → env file → systemd (API + timer backup) → nginx + certbot → deploy bản đầu → kiểm `/health`.

Cuối cùng in **bảng tóm tắt**: URL, user designer và **mật khẩu designer sinh tự động - chỉ in một lần**, hãy lưu lại rồi đổi trong app. Mật khẩu DB tự sinh và chỉ nằm trong env file trên server.

Chạy lại `-FirstTime` bao nhiêu lần cũng được (idempotent): không sinh lại mật khẩu, không ghi đè mật khẩu DB.

## 3. Chế độ chỉ HTTP (thử khi DNS chưa sẵn sàng)

Để trống `LETSENCRYPT_EMAIL` → nginx chỉ chạy HTTP, không có chứng chỉ. Script không giả https: API vẫn chạy `Production` với `App__PublicBaseUrl=https://DOMAIN` (API bắt buộc https, nếu không thì không khởi động), nên:

- Mở `http://DOMAIN` hoặc `http://IP`: **đăng nhập và xem được**, nhưng mọi thao tác ghi bị **403**, vì cookie CSRF luôn có cờ `Secure` mà trình duyệt không nhận cookie `Secure` qua http. Mật khẩu đi qua mạng dạng rõ.
- Muốn thử **đầy đủ**: dùng tunnel `ssh -L 8080:127.0.0.1:80 ubuntu@IP_SERVER` rồi mở `http://localhost:8080`. Trình duyệt coi localhost là an toàn, kết nối được mã hóa bởi SSH, và chỉ ở chế độ này script mới cho phép thêm origin `http://localhost:8080`.
- Magic link gửi khách vẫn là `https://DOMAIN/p/...`, chưa mở được cho tới khi bật HTTPS.
- Bật HTTPS: trỏ DNS xong, điền `LETSENCRYPT_EMAIL`, chạy lại `-FirstTime`. Origin localhost sẽ tự bị gỡ.

## 4. Deploy thường ngày

```powershell
powershell -ExecutionPolicy Bypass -File deploy\deploy-windows.ps1
```

Server: giải nén vào `releases/<thời_gian>` → dump DB trước deploy → đổi symlink `current` (atomic) → restart → chờ `/health` (tối đa 120 giây) → **lỗi thì tự quay về bản trước**. Giữ 5 bản gần nhất. Tham số khác:

| Lệnh | Tác dụng |
|---|---|
| `-SkipBuild` | Dùng lại gói mới nhất trong `deploy\.build\` |
| `-SkipUpload` | Chỉ build + đóng gói, không SSH |
| `-Action status` | Service, release, health, hạn TLS, dung lượng ổ, backup |
| `-Action logs` | 200 dòng log API gần nhất (trên server: `sudo freelanceflow logs -f` để theo dõi) |
| `-Action rollback` | Quay về release trước |
| `-Action backup` | Backup ngay |

Build chạy trên **bản sao** `deploy\.build\frontend` nên không đụng `node_modules`/`dist` của `npm run dev` đang chạy; API publish vào `deploy\.build\dotnet`.

**Rollback và migration**: rollback chỉ đổi code, **không** đảo ngược migration. Nếu code cũ lỗi với schema mới, khôi phục dump `pre-deploy-*.dump` (mục 5).

## 5. Backup & khôi phục

Timer `freelanceflow-backup.timer` chạy hằng ngày lúc `BACKUP_TIME`, giữ `BACKUP_RETENTION_DAYS` ngày:

- `/var/backups/freelanceflow/db/db-<thời_gian>.dump`: `pg_dump -Fc`, cộng thêm `pre-deploy-<thời_gian>.dump` trước mỗi deploy.
- `/var/backups/freelanceflow/files/<thời_gian>/{files,keys}`: snapshot ảnh + key ring. Dùng hard-link nên ảnh không đổi giữa các ngày không tốn thêm dung lượng.

Khôi phục (trên server). Backup chỉ root đọc được (thư mục 700, file 600), nên làm trong shell root và đưa dump vào `pg_restore` qua stdin:

```bash
sudo -i
ls /var/backups/freelanceflow/db /var/backups/freelanceflow/files      # chọn bản cần khôi phục
systemctl stop freelanceflow
runuser -u postgres -- dropdb freelanceflow
runuser -u postgres -- createdb -O ff freelanceflow
runuser -u postgres -- pg_restore -d freelanceflow < /var/backups/freelanceflow/db/db-YYYYMMDD-HHMMSS.dump
rsync -a --delete /var/backups/freelanceflow/files/YYYYMMDD-HHMMSS/files/ /var/lib/freelanceflow/files/
rsync -a --delete /var/backups/freelanceflow/files/YYYYMMDD-HHMMSS/keys/  /var/lib/freelanceflow/keys/
chown -R freelanceflow:freelanceflow /var/lib/freelanceflow/files /var/lib/freelanceflow/keys
systemctl start freelanceflow && freelanceflow status
exit
```

(Đổi `freelanceflow`/`ff` nếu bạn đặt `DB_NAME`/`DB_USER` khác.) Backup nằm **cùng ổ** với dữ liệu: chống được lỗi thao tác hay migration hỏng, nhưng **không** chống được hỏng ổ/mất VPS. Nên chép thêm ra ngoài, ví dụ `rclone sync /var/backups/freelanceflow remote:freelanceflow-backup` bằng cron. Mỗi tháng nên thử khôi phục một lần.

## 6. Mọi thứ nằm ở đâu

| Gì | Ở đâu |
|---|---|
| Env file (bí mật: DB, webhook) | `/etc/freelanceflow/freelanceflow.env` (600, root:freelanceflow) |
| Bản sao config (đã xóa bí mật) | `/etc/freelanceflow/freelanceflow.conf` |
| Code | `/opt/freelanceflow/app/releases/*`, `current` → bản đang chạy (`app/` API, `web/` SPA) |
| Ảnh / key ring | `/var/lib/freelanceflow/files`, `/var/lib/freelanceflow/keys` |
| Log API | `journalctl -u freelanceflow` (journald giới hạn `JOURNAL_MAX_USE`) |
| Log nginx | `/var/log/nginx/access.log`, `error.log` (không ghi `/p/*`, `/f/*`, `/api/public/*`) |
| nginx site | `/etc/nginx/sites-available/freelanceflow` (script sinh ra; xem trước: `sudo freelanceflow render-nginx tls`) |
| systemd | `/etc/systemd/system/freelanceflow.service`, `freelanceflow-backup.{service,timer}` |
| Lệnh quản trị | `sudo freelanceflow <lệnh>` với lệnh: `setup`, `deploy`, `rollback`, `status`, `backup`, `logs` |

## 7. Đổi cấu hình về sau

Sửa `deploy\freelanceflow.conf` rồi chạy lại `deploy-windows.ps1 -FirstTime`. Hoặc trên server: `sudo nano /etc/freelanceflow/freelanceflow.conf && sudo freelanceflow setup`. **Không** sửa tay env file, nginx site hay unit vì lần setup sau sẽ ghi đè.

- `DISCORD_WEBHOOK_URL`: để trống = giữ giá trị đang có trên server; `none` = tắt.
- `DB_PASSWORD`: không bao giờ bị ghi đè. Muốn đổi: `sudo -u postgres psql -c "ALTER ROLE ff PASSWORD 'moi'"`, sửa `Password=` trong env file, rồi `sudo systemctl restart freelanceflow`.
- `DESIGNER_PASSWORD`: chỉ dùng khi chưa có tài khoản; sau đó đổi mật khẩu trong app.

## 8. Ngân sách ổ 50 GB

| Phần | Ước tính |
|---|---|
| Ubuntu + gói (.NET, PostgreSQL, nginx), apt cache | ~5–6 GB |
| Dữ liệu PostgreSQL | 0,5–2 GB |
| Ảnh (`STORAGE_QUOTA_BYTES`, mặc định 10 GB; chặn upload ở 90 %) | ≤ 10 GB |
| Snapshot ảnh 14 ngày (hard-link: chỉ tốn phần ảnh đã xóa/thay trong 14 ngày) | 0 – ~10 GB |
| Dump DB 14 ngày + pre-deploy | < 1 GB |
| 5 release | ~0,2 GB |
| journald | ≤ 1 GB |
| Swap | 2 GB |
| **Tổng xấu nhất** | **~32 GB**, còn dư ~18 GB |

Đặt quota 20 GB thì ảnh + snapshot đã gần 40 GB: quá sát. `setup` sẽ cảnh báo nếu quota lớn hơn 30 % dung lượng ổ.

## 9. Bảo mật

- Bí mật (mật khẩu DB, webhook Discord) **chỉ** nằm trong env file trên server. `deploy/freelanceflow.conf` đã có trong `.gitignore`, **không commit**. Sau setup có thể xóa mật khẩu/webhook khỏi file local. File config upload lên server bị xóa ngay sau khi chạy.
- API chỉ nghe `127.0.0.1:5000`. Đây là **bắt buộc**: API tin `X-Forwarded-For` của một hop (`KnownProxies` bị xóa trong `Program.cs`), nginx ghi đè header này bằng `$remote_addr` nên khách không giả IP được. ufw chỉ mở SSH/80/443; PostgreSQL chỉ nghe localhost.
- Service chạy bằng user `freelanceflow` (không đăng nhập được), hardening systemd (`ProtectSystem=strict`, chỉ ghi được thư mục ảnh và key ring, `NoNewPrivileges`, ...).
- Header bảo mật giống `deploy/Caddyfile` (HSTS chỉ khi có TLS, CSP, nosniff, X-Frame-Options, Permissions-Policy). `/p/*` và `/f/*` dùng `Referrer-Policy: no-referrer`, `/f/*` thêm `X-Robots-Tag: noindex`.
- Siết SSH (tùy chọn): `SSH_DISABLE_PASSWORD_AUTH=yes`, `SSH_DISABLE_ROOT_LOGIN=yes`. Script từ chối nếu thao tác đó có thể khóa bạn ngoài server.
- `appsettings.Development.json` bị loại khỏi gói build và bị xóa thêm lần nữa trên server.

## 10. Gỡ rối nhanh

| Triệu chứng | Kiểm tra |
|---|---|
| certbot lỗi | DNS A đã trỏ đúng IP chưa (`nslookup DOMAIN`), cổng 80 có mở ở firewall của nhà cung cấp VPS không |
| Deploy tự rollback | `-Action logs`: thường do sai connection string, thư mục ảnh/keys không ghi được, hoặc migration lỗi |
| 502 Bad Gateway | `sudo systemctl status freelanceflow`, `sudo freelanceflow logs` |
| Ghi dữ liệu bị 403 | Đang ở chế độ HTTP (mục 3), hoặc mở app bằng tên miền khác `DOMAIN` |
| Hết dung lượng | `-Action status` (dung lượng từng thư mục); giảm `BACKUP_RETENTION_DAYS` |
