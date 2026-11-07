# 10 — Implementation Plan (Phase 1)

Ước lượng cho **1 dev full-stack**, tính theo ngày làm việc (d). Tổng ≈ **42–50 d** (~9–10 tuần).
Mỗi milestone kết thúc bằng app chạy được + test pass, merge vào `main` qua PR.

## Definition of Done (mọi task)

- [ ] Rule liên quan (BR-xx) có unit test; endpoint có integration test (happy + lỗi chính).
- [ ] Endpoint Portal mới có case trong bảng isolation (09 §4).
- [ ] Thay đổi trạng thái/tiền có activity log.
- [ ] Migration EF tạo được DB từ đầu và nâng cấp từ bản trước.
- [ ] UI có loading/empty/error state; text tiếng Việt.
- [ ] `dotnet test` + `npm test` + `npm run lint` + `npm run typecheck` pass.

## M0 — Nền móng (4–5 d)

| # | Task | Output |
|---|---|---|
| 0.1 | Repo, `CLAUDE.md`, `.editorconfig`, `.gitignore`, `.env.example` | |
| 0.2 | Backend: solution, Minimal API, Serilog, ProblemDetails, `IClock`, health check | `GET /health` |
| 0.3 | EF Core + Npgsql, `AppDbContext`, interceptor `created_at/updated_at`, xmin concurrency, mapping enum → string | Migration rỗng chạy được |
| 0.4 | Test infra: xUnit, Testcontainers, `ApiFactory`, `TestData.Seed()` | 1 test mẫu xanh |
| 0.5 | Frontend: Vite + React + TS + Tailwind + shadcn + Router + TanStack Query + api client (CSRF header, xử lý ProblemDetails) | Trang trống 2 layout Admin/Portal |
| 0.6 | Docker: Dockerfile backend, build frontend, `docker-compose.yml` + Caddyfile; `docker-compose.dev.yml` (chỉ Postgres) | `docker compose up` chạy |
| 0.7 | CI (GitHub Actions): build + test BE/FE | |

## M1 — Auth, Settings, Catalog (3–4 d)

| # | Task | FR / BR |
|---|---|---|
| 1.1 | Bảng `designer_accounts`, seed từ env, login/logout/me, lockout, rate limit, cookie scheme `Designer` | SET-05, 07 §2 |
| 1.2 | CSRF middleware + Origin check | 07 §3 |
| 1.3 | `banks` seed, `settings` singleton, API + UI Cài đặt | SET-01..04 |
| 1.4 | Services CRUD + UI | SVC-01..03 |
| 1.5 | **VietQR**: `Crc16Ccitt`, `VietQrPayloadBuilder`, memo validator, test vectors (06 §7), PNG endpoint; QR thử 10.000 ₫ ở Settings | 06 |

> Làm VietQR sớm để designer quét thử thật ngay từ tuần 2 — rủi ro kỹ thuật lớn nhất của sản phẩm.

## M2 — Client, Project, Brief, Deliverable (5–6 d)

| # | Task | FR / BR |
|---|---|---|
| 2.1 | Clients CRUD, tìm kiếm, archive | CLI-01, 04, 05, BR-10/11 |
| 2.2 | Activity log: entity, service `ActivityWriter`, trigger append-only, endpoint timeline | ACT-01..04 |
| 2.3 | Projects CRUD + state machine `Project.ChangeStatus` + warnings | PRJ-01..06, BR-20/21 |
| 2.4 | Brief versioning + trigger immutable + UI editor/preview/diff | BRF-01..05, BR-25..28 |
| 2.5 | Deliverables + `used_rounds` query (tạm = 0) | DLV-01..03 |
| 2.6 | UI: danh sách dự án, chi tiết (Overview, Brief, Deliverables, Activity) | 08 §2 |

## M3 — Files (2–3 d)

| # | Task | FR |
|---|---|---|
| 3.1 | `IFileStorage` + `LocalFileStorage`, kiểm magic bytes, whitelist, sha256 | FIL-01..04, 07 §5 |
| 3.2 | API upload/list/patch/delete/content; header an toàn | |
| 3.3 | UI `FileUploader`, lưới file, lightbox, toggle chia sẻ | |

## M4 — Quotation (7–8 d) ⭐

| # | Task | FR / BR |
|---|---|---|
| 4.1 | Domain `Quotation` + `QuotationItem`: tính tiền (half-up), state machine, `Send`, `NewVersion`, `MarkAccepted/Rejected`, `ChangePaymentStatus`, `EffectiveStatus(today)` — **TDD** | BR-30..39, 70..73 |
| 4.2 | `quotation_sequences` + sinh số trong transaction; test đồng thời | BR-32, AC-B4 |
| 4.3 | Snapshot builder (schemaVersion 1) | 04 §5 |
| 4.4 | Trigger khóa quotation/items; unique index "one live" | BR-35, 09 §5 |
| 4.5 | API admin quotations (CRUD DRAFT, send, new-version, duplicate, accept, reject, payment-status, qr) | 05 §3.10 |
| 4.6 | INITIAL từ deliverables | QUO-01 |
| 4.7 | UI trình soạn DRAFT + preview; màn hình xem snapshot + `QrCard` + hành động trạng thái; upload bằng chứng | 08 §2.2 |

## M5 — Requests & Revisions (6–7 d) ⭐

| # | Task | FR / BR |
|---|---|---|
| 5.1 | Domain: `RevisionRound`, `RevisionItem`, quota counting, `SuggestBilling` — **TDD** (AC-Q1..Q7) | BR-40..45 |
| 5.2 | Client requests (designer tạo hộ), inbox, decline | REQ-02..05, BR-50..52 |
| 5.3 | Triage endpoint + `suggest-billing` | REQ-04, 05 §3.7.1 |
| 5.4 | Work status + scope guard + billing lock | BR-60..62, AC-S* |
| 5.5 | `from-revisions` → ADDITIONAL DRAFT (BR-46), link qua version (BR-47) | AC-Q8, AC-B6 |
| 5.6 | UI inbox, triage, tab Revisions, chọn nhiều item → báo giá; cập nhật `QuotaMeter` thật | 08 §2.3 |

## M6 — Tasks (3–4 d)

| # | Task | FR |
|---|---|---|
| 6.1 | Tasks CRUD + checklist + move (sortOrder dạng số thực, chèn giữa) | TSK-01..05 |
| 6.2 | Tạo task từ revision item | REV-06 |
| 6.3 | UI List + Kanban (@dnd-kit) + trang "Tất cả task" | |

## M7 — Client Portal (6–7 d) ⭐

| # | Task | FR |
|---|---|---|
| 7.1 | Magic link: tạo/thu hồi, `POST /portal/session`, scheme `Portal`, kiểm token version | CLI-03, 07 §4 |
| 7.2 | `PortalPrincipal`, DTO portal riêng, toàn bộ endpoint `/api/portal/*` | POR-01..06 |
| 7.3 | Request từ portal (multipart, rate limit, file khách) | REQ-01, 06, BR-53/54 |
| 7.4 | **Isolation test suite** tự sinh theo route | 09 §4 |
| 7.5 | UI portal mobile-first: dự án, chi tiết, báo giá + QR, yêu cầu | 08 §3 |
| 7.6 | Admin: "Xem như khách" (mở portal preview read-only bằng session đặc biệt chỉ cho designer) | |
| 7.7 | Timeline công khai: thêm `client_message` cho các action ở BR-95 | ACT, BR-95 |

## M8 — Dashboard & hoàn thiện (4–5 d)

| # | Task | FR |
|---|---|---|
| 8.1 | API dashboard (BR-90) + UI 3 khối + biểu đồ 6 tháng | DSH-01..04 |
| 8.2 | Tìm kiếm nhanh toàn cục | |
| 8.3 | Tin nhắn mẫu sau khi gửi báo giá / tạo link (copy) | 08 §2.3 |
| 8.4 | E2E Playwright 5 luồng | 09 §3 |
| 8.5 | Responsive pass, a11y pass (contrast, focus, label) | NFR-09 |

## M9 — Go-live (2 d)

| # | Task |
|---|---|
| 9.1 | VPS + domain + Caddy TLS; `.env` production; seed designer rồi xóa mật khẩu khỏi env |
| 9.2 | Cron backup DB + files + off-site; thử restore |
| 9.3 | Checklist bảo mật 07 §9; kiểm thử thủ công 09 §6 (QR thật, Zalo in-app browser) |
| 9.4 | Nhập dữ liệu thật: dịch vụ, khách đang làm |

## Phụ thuộc & thứ tự

```text
M0 → M1 → M2 → M3 ─┬─→ M4 ─→ M5 ─→ M7 → M8 → M9
                   └─→ M6 (song song sau M2)
```

- M4 trước M5 vì báo giá phát sinh (5.5) cần Quotation domain.
- M7 sau M5 vì portal hiển thị revision và gửi request.
- M6 độc lập, chèn vào bất cứ lúc nào sau M2.

## Rủi ro

| Rủi ro | Ảnh hưởng | Giảm thiểu |
|---|---|---|
| QR không quét được ở một số app ngân hàng | Mất tính năng cốt lõi | Làm ở M1, quét thật sớm; luôn hiển thị STK + nội dung dạng text |
| Zalo in-app browser chặn cookie/upload | Khách không dùng được portal | Test sớm ở M7; fallback hướng dẫn "Mở bằng trình duyệt" |
| Logic quota sai | Tính tiền sai, mất uy tín | TDD + AC-Q*, designer luôn override được |
| Phạm vi phình | Trễ | Mọi tính năng ngoài PRD → backlog Phase 2 |

## Backlog Phase 2 (đã chốt, chưa làm)

PDF báo giá (QuestPDF) kèm QR · version file + watermark preview · thông báo request mới (email/Telegram bot) ·
TOTP · công cụ ẩn danh hóa khách · dọn file đã xóa mềm · ClamAV · Calendar view task.
