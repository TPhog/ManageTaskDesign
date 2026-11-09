# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**FreelanceFlow** — web app cá nhân cho 1 designer quản lý khách, dự án, deliverable, revision (quota), task, báo giá có VietQR, và Client Portal (khách chỉ xem + gửi request). **Không xử lý thanh toán.**

## Docs (nguồn sự thật — đọc trước khi code)

| File | Dùng khi |
|---|---|
| `docs/02-business-rules.md` | Mọi logic domain — ID BR-xx là ground truth |
| `docs/04-database-design.md` | Schema, DDL, constraint, index |
| `docs/05-api-spec.md` | Endpoint, DTO, lỗi |
| `docs/06-vietqr-spec.md` | VietQR payload, CRC, test vector |
| `docs/07-security.md` | Auth, portal magic link, CSRF, upload |
| `docs/09-test-plan.md` | Acceptance criteria (AC-xx), portal isolation |
| `docs/10-implementation-plan.md` | Thứ tự milestone, Definition of Done |

Thứ tự đọc khi bắt đầu feature mới: FR trong `01` → rule trong `02` → bảng trong `04` → endpoint trong `05` → AC trong `09`.

## Stack

- **Backend:** .NET 9 Minimal APIs + EF Core 9 + Npgsql + PostgreSQL 17
- **Frontend:** React 19 + TypeScript + Vite + Tailwind CSS 4 + shadcn/ui + TanStack Query + react-hook-form + zod · font tự host `@fontsource` ghim 5.3.0 (Bricolage Grotesque + Be Vietnam Pro cho admin, Fraunces + Public Sans cho portal)
- **Auth:** Cookie (2 scheme: `Designer` SameSite=Strict, `Portal` SameSite=Lax) — không JWT
- **Test BE:** xUnit + FluentAssertions + Testcontainers.PostgreSql
- **Test FE:** Vitest + Testing Library · E2E: Playwright

## Commands

```bash
# Dev database
docker compose -f docker-compose.dev.yml up -d

# Backend
dotnet build backend/FreelanceFlow.sln
dotnet test backend/FreelanceFlow.sln
dotnet test backend/FreelanceFlow.sln --filter "FullyQualifiedName~UnitTests"
dotnet test backend/FreelanceFlow.sln --filter "FullyQualifiedName~IntegrationTests"
dotnet test backend/FreelanceFlow.sln --filter "FullyQualifiedName~QuotationTests.Send"  # single test class

# Frontend
cd frontend
npm run dev          # dev server
npm test             # Vitest
npm run lint
npm run typecheck
npm run build        # production build → dist/

# Full stack
docker compose up --build   # production-like
```

## Architecture

### Cấu trúc backend

```
src/FreelanceFlow.Api/
├── Domain/           ← entity + enum + domain method (không biết EF/HTTP)
│   ├── Common/       ← Money helpers, DomainException(code), IClock
│   └── {Aggregate}/  ← Clients/ Projects/ Revisions/ Quotations/ ...
├── Features/         ← mỗi feature: Endpoints.cs + DTOs + Validators + Handlers
│   └── Portal/       ← TẤT CẢ endpoint /api/portal/*
├── Data/             ← AppDbContext, Configurations/, Migrations/
└── Infrastructure/
    ├── VietQr/       ← VietQrPayloadBuilder, Crc16Ccitt, BankDirectory
    ├── Storage/      ← IFileStorage + LocalFileStorage
    └── Security/     ← PortalTokenService, auth handlers, rate limit
```

### Quy ước phân lớp (bắt buộc)

- `Domain/` chứa **business method** trên entity (ví dụ: `Quotation.Send(clock, seq, settings)`, `Project.ChangeStatus(...)`). Không gọi DB, không biết HTTP. Ném `DomainException(code)` khi vi phạm rule.
- `Features/` **điều phối**: load entity qua DbContext, gọi method domain, ghi activity log, `SaveChangesAsync` trong 1 transaction. **Không viết domain logic ở endpoint.**
- Không dùng Repository pattern / MediatR / CQRS. DbContext dùng trực tiếp (single-user app).
- Truy vấn đọc: projection `Select(x => new Dto {...})` + `AsNoTracking()`.

### Ba nhóm endpoint

- `/api/admin/*` — Designer (cookie scheme `Designer`)
- `/api/portal/*` — Khách (cookie scheme `Portal`; `clientId` chỉ được lấy từ `PortalPrincipal`, **không** từ route/body)
- `/api/public/*` — Ẩn danh (form khách mới `/f/{slug}`, P13): `AllowAnonymous`, **không** CSRF, **không bao giờ** đọc `HttpContext.User`; bắt buộc rate limit có tên (`RateLimit:*`), giới hạn kích thước, honeypot, không echo dữ liệu; route mới phải thêm vào `PublicRouteGuardTests` + docs/07 §4.5

### Frontend

```
src/
├── lib/              ← api client (CSRF header), query keys, format tiền/ngày, zod schemas
├── components/       ← shared: MoneyText, StatusBadge, QuotaMeter, FileUploader, QrCard, Tile, StatTile, PageHeader, dataTable
├── features/admin/   ← dashboard, clients, projects, requests, tasks, quotations, settings
└── features/portal/  ← home, project detail, quotation, request-new; kit/ ← editorial kit + usePortalTheme
```

## Hard rules

### Tiền & số học
- Lưu `long` VND (`bigint` DB). **Không** `decimal`/`double` cho tiền.
- Làm tròn `Math.Round(x, MidpointRounding.AwayFromZero)` (half-up). Tính ở backend.
- Công thức: `subtotal → taxable = subtotal − discount → vat_amount (half-up) → total → deposit_amount (half-up)`.

### Trạng thái & bất biến
- Báo giá ≠ DRAFT → **bất biến**; hiển thị từ `snapshot` (cả Admin lẫn Portal).
- DB trigger khóa sửa báo giá/items đã gửi; sửa = tạo version mới.
- Trạng thái `EXPIRED` tính khi đọc (`effective_status`), không lưu DB.

### Transaction & activity log
- Mỗi command = 1 transaction (`SaveChangesAsync`).
- **Mọi** thay đổi trạng thái, tiền, quota, portal token → ghi `activity_logs` **trong cùng transaction**.
- Activity log là append-only.

### Clock & timezone
- Inject `IClock` (không gọi `DateTime.Now`/`DateTimeOffset.UtcNow` trực tiếp).
- `TodayVn` cho ngày lịch Việt Nam. DB dùng `timestamptz` (thời điểm) và `date` (ngày: deadline, expiry).

### Portal isolation
- Mọi tài nguyên portal scope theo `clientId` từ session. Ngoài scope → 404 (không 403).
- Endpoint portal mới **bắt buộc** thêm case vào bảng isolation test (xem `docs/09-test-plan.md` §4).

### UI tokens
- Chỉ dùng token semantic (`docs/08` §6): admin = Studio Bento (`:root`), portal/public = Editorial (`data-theme="editorial"`). Không class bảng màu Tailwind/hex trong admin, components, portal, public (test `palette.all`; ngoại lệ ghi `palette-ok: <lý do>` cùng dòng). Chữ trên cam son luôn là mực. Trang portal dùng editorial kit.

### Security
- Không log portal token, mật khẩu, body upload.
- Portal SPA: `Referrer-Policy: no-referrer` cho trang `/p/*` và form khách mới `/f/*` (Caddy + Vite dev); header bảo mật toàn site ở `deploy/Caddyfile` (docs/07 §6).
- Magic link: token 256-bit, chỉ lưu SHA-256 hash, kiểm bằng `CryptographicOperations.FixedTimeEquals`.

## Debug pointers

| Triệu chứng | Nhìn vào |
|---|---|
| Quota sai | `Domain/Revisions/` → `SuggestBilling`, `used_rounds` query |
| Tiền báo giá sai | `Domain/Quotations/` → tính tiền, snapshot builder |
| QR không quét được | `Infrastructure/VietQr/` + test vectors `docs/06-vietqr-spec.md` §7 |
| Khách thấy dữ liệu lạ | `Features/Portal/` + isolation tests |
| Concurrency conflict | `xmin` concurrency token trên `quotations`, `projects`, `revision_items`, `client_requests` |
| Số báo giá trùng | `quotation_sequences` + `SELECT ... FOR UPDATE` trong transaction `Send` |
