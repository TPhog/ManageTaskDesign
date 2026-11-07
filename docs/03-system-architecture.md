# 03 — System Architecture

## 1. Tổng quan

```text
                    ┌──────────────────────── VPS (Docker Compose) ───────────────────────┐
 Designer (browser) │  ┌────────────┐     ┌──────────────────────┐     ┌──────────────┐  │
 ───── HTTPS ──────►│  │   Caddy /  │────►│  api (ASP.NET Core   │────►│ PostgreSQL 17│  │
 Client (mobile)    │  │   Nginx    │     │  .NET 10, Kestrel)   │     └──────────────┘  │
 ───── HTTPS ──────►│  │ TLS + SPA  │     │                      │────► /data/files     │
                    │  └────────────┘     └──────────────────────┘      (volume)        │
                    │   serves web/dist      /api/admin/*  /api/portal/*                 │
                    └─────────────────────────────────────────────────────────────────────┘
```

- **Một** SPA React phục vụ cả Admin (`/admin/*`) và Portal (`/p/*`, `/portal/*`).
- **Một** API ASP.NET Core, hai nhóm endpoint tách biệt: `/api/admin/*` (designer) và `/api/portal/*` (khách).
- Không Redis, không message queue, không background worker ở Phase 1. Các trạng thái theo thời gian
  (EXPIRED, quá hạn) **tính khi đọc**.

## 2. Tech stack

| Layer | Công nghệ | Ghi chú |
|---|---|---|
| Frontend | React 19 + TypeScript + Vite | SPA |
| UI | Tailwind CSS 4 + shadcn/ui | Component copy vào repo |
| Routing | React Router | `/admin/*`, `/p/:token`, `/portal/*` |
| Data fetching | TanStack Query | Cache + invalidation theo query key |
| Form | react-hook-form + zod | Schema validate dùng chung cho form |
| Kanban | @dnd-kit | Kéo thả task |
| Markdown | react-markdown (render) + textarea/MDX editor đơn giản | Brief |
| QR render | `qrcode` (npm) hoặc nhận PNG từ API | Payload sinh ở backend |
| Backend | ASP.NET Core .NET 10 — Minimal APIs | Một project API |
| ORM | EF Core 10 + Npgsql | Migration bằng EF |
| Validation | FluentValidation | Tại endpoint |
| Auth | Cookie authentication (2 scheme: `Designer`, `Portal`) | Không JWT |
| Password hash | `PasswordHasher<T>` từ Microsoft.AspNetCore.Identity (không dùng full Identity) | |
| QR (server) | QRCoder | Sinh PNG cho tải về |
| Logging | Serilog → console (JSON) | Docker logs |
| DB | PostgreSQL 17 | `timestamptz`, `jsonb`, partial index |
| Reverse proxy | Caddy (tự cấp TLS Let's Encrypt) — hoặc Nginx + certbot | |
| Test BE | xUnit + FluentAssertions + Testcontainers.PostgreSql | |
| Test FE | Vitest + Testing Library; Playwright cho E2E | |

## 3. Cấu trúc repository

```text
camcam/
├── CLAUDE.md
├── docker-compose.yml
├── docker-compose.dev.yml
├── .env.example
├── docs/                         ← bộ tài liệu này
├── backend/
│   ├── FreelanceFlow.sln
│   ├── src/FreelanceFlow.Api/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Domain/               ← entity + enum + domain logic thuần (không phụ thuộc EF/HTTP)
│   │   │   ├── Clients/  Projects/  Briefs/  Deliverables/  Requests/
│   │   │   ├── Revisions/  Tasks/  Quotations/  Files/  Activity/  Settings/
│   │   │   └── Common/           ← Money helpers, DomainException, Clock
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/   ← IEntityTypeConfiguration<T>
│   │   │   └── Migrations/
│   │   ├── Features/             ← mỗi feature: Endpoints.cs + DTOs + Validators + Handlers
│   │   │   ├── Auth/  Settings/  Services/  Clients/  Projects/  Briefs/  Deliverables/
│   │   │   ├── Requests/  Revisions/  Tasks/  Quotations/  Files/  Activity/  Dashboard/
│   │   │   └── Portal/           ← tất cả endpoint của khách
│   │   ├── Infrastructure/
│   │   │   ├── Storage/          ← IFileStorage + LocalFileStorage
│   │   │   ├── VietQr/           ← VietQrPayloadBuilder, Crc16Ccitt, BankDirectory
│   │   │   └── Security/         ← PortalTokenService, auth handlers, rate limit
│   │   └── Common/               ← ProblemDetails mapping, pagination, ICurrentActor
│   └── tests/
│       ├── FreelanceFlow.UnitTests/         ← domain + VietQR + money
│       └── FreelanceFlow.IntegrationTests/  ← WebApplicationFactory + Testcontainers
└── frontend/
    ├── package.json
    ├── src/
    │   ├── main.tsx  router.tsx
    │   ├── lib/                  ← api client, query keys, format (tiền, ngày), zod schemas
    │   ├── components/ui/        ← shadcn
    │   ├── components/           ← shared: MoneyText, StatusBadge, QuotaMeter, FileUploader, QrCard
    │   ├── features/admin/       ← dashboard, clients, projects, requests, tasks, quotations, services, settings
    │   └── features/portal/      ← home, project, quotation, request-new
    └── e2e/                      ← Playwright
```

### Quy ước phân lớp

- `Domain/` chứa entity với **method nghiệp vụ** (VD `Quotation.Send(clock, seq, settings)`,
  `Quotation.MarkAccepted(...)`, `Project.ChangeStatus(...)`) và ném `DomainException(code)` khi vi phạm.
  Không gọi DB, không biết HTTP.
- `Features/` điều phối: load entity qua `AppDbContext`, gọi method domain, ghi activity, `SaveChanges`
  trong 1 transaction. Không viết logic nghiệp vụ ở endpoint.
- Không dùng repository pattern / MediatR / CQRS — DbContext dùng trực tiếp trong handler (single-user app).
- Truy vấn đọc (list, dashboard) dùng projection `Select` → DTO, `AsNoTracking`.

## 4. Luồng chính

### 4.1 Khách gửi request → báo giá phát sinh → QR

```text
Client(Portal)        API                         DB                     Designer(Admin)
   │ POST /portal/requests (multipart)              │                          │
   │──────────────────────►│ validate, client_id từ session                   │
   │                       │ insert client_request(NEW) + files + activity ──►│
   │                       │                                                  │ thấy badge NEW
   │                       │◄────────── POST /admin/requests/{id}/triage ─────│
   │                       │ tạo round(s)+items, gợi ý billing (BR-46), status=TRIAGED
   │                       │◄────────── POST /admin/quotations/from-revision ─│ chọn item CHARGEABLE
   │                       │ tạo quotation ADDITIONAL DRAFT + items (giá gợi ý)│
   │                       │◄────────── POST /admin/quotations/{id}/send ─────│
   │                       │ number, snapshot, SENT, activity(visible)        │
   │ GET /portal/quotations/{id}  → snapshot + qr options                     │
   │ khách quét QR, chuyển khoản, nhắn Zalo "đã đồng ý + đã CK"               │
   │                       │◄── POST /admin/quotations/{id}/accept ───────────│
   │                       │◄── POST /admin/quotations/{id}/payment-status ───│ DEPOSIT_PAID
```

### 4.2 Transaction & concurrency

- Mỗi command = 1 `DbContext` transaction (`SaveChangesAsync` bao cả activity log).
- Optimistic concurrency bằng cột hệ thống `xmin` của PostgreSQL (`UseXminAsConcurrencyToken` /
  `IsRowVersion`) cho `quotations`, `projects`, `revision_items`, `client_requests`.
  Client gửi `version` (xmin) trong body khi update; mismatch → `409 CONCURRENCY_CONFLICT`.
- Sinh số báo giá: `SELECT ... FROM quotation_sequences WHERE year = @y FOR UPDATE` trong transaction `send`.

### 4.3 Đồng hồ & thời gian

- Interface `IClock { DateTimeOffset UtcNow; DateOnly TodayVn; }` — inject để test.
- DB: `timestamptz` cho thời điểm, `date` cho ngày (deadline, issue/expiry, accepted_at, paid_at).

## 5. Cấu hình (environment)

| Biến | Ví dụ | Mô tả |
|---|---|---|
| `ConnectionStrings__Default` | `Host=db;Database=freelanceflow;Username=ff;Password=...` | |
| `App__PublicBaseUrl` | `https://studio.example.com` | Dùng để dựng magic link |
| `App__FilesRoot` | `/data/files` | Thư mục lưu file |
| `App__MaxUploadMb` | `20` | |
| `Seed__DesignerEmail` | `me@example.com` | Chỉ dùng lần đầu khi chưa có tài khoản |
| `Seed__DesignerPassword` | (secret) | Chỉ dùng lần đầu; xóa khỏi env sau khi seed |
| `ASPNETCORE_ENVIRONMENT` | `Production` | |

Data Protection keys (ký cookie) lưu vào volume `/data/keys` để cookie sống qua restart.

## 6. Deploy

`docker-compose.yml` (rút gọn):

```yaml
services:
  db:
    image: postgres:17
    environment: [POSTGRES_DB=freelanceflow, POSTGRES_USER=ff, POSTGRES_PASSWORD=${DB_PASSWORD}]
    volumes: [pgdata:/var/lib/postgresql/data]
    healthcheck: { test: ["CMD-SHELL", "pg_isready -U ff"], interval: 10s }
  api:
    build: ./backend
    env_file: .env
    volumes: [files:/data/files, keys:/data/keys]
    depends_on: { db: { condition: service_healthy } }
  web:
    image: caddy:2
    ports: ["80:80", "443:443"]
    volumes: [./deploy/Caddyfile:/etc/caddy/Caddyfile, ./frontend/dist:/srv, caddy_data:/data]
volumes: { pgdata: {}, files: {}, keys: {}, caddy_data: {} }
```

Caddyfile: `handle /api/* { reverse_proxy api:8080 }`, còn lại `try_files {path} /index.html` + `file_server`.

- API chạy migration khi khởi động (`db.Database.MigrateAsync()`) — chấp nhận được với single instance.
- Seed designer + settings mặc định + danh sách ngân hàng khi DB trống.

### Backup

- Cron trên host: `pg_dump -Fc` hằng ngày + `tar` volume `files` → thư mục backup, giữ 14 bản,
  đồng bộ off-site (rclone tới Google Drive/R2) — khuyến nghị.
- Script `deploy/restore.sh` + kiểm thử restore mỗi tháng.

## 7. Observability

- Serilog JSON ra stdout; log request (method, path, status, duration) — **không** log token portal,
  mật khẩu, body upload.
- Endpoint `GET /health` (DB ping) cho Docker healthcheck / uptime monitor.

## 8. Khả năng mở rộng sau này (không làm bây giờ)

- `IFileStorage` → thêm `S3FileStorage` (R2/MinIO) khi dung lượng lớn.
- Background job (Hangfire/Quartz) khi cần email nhắc hạn, dọn file.
- Multi-tenant: thêm `owner_id` + global query filter — hiện **cố ý không làm** (ADR-002).
