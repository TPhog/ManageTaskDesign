# FreelanceFlow — Bộ tài liệu kỹ thuật

> Phiên bản: 1.0 · Ngày: 2026-11-07 · Trạng thái: Baseline cho Phase 1

FreelanceFlow là web app **cá nhân** cho **một designer** quản lý khách hàng, dự án, revision, báo giá
và cho khách hàng xem tiến độ/báo giá qua Client Portal. App **không xử lý thanh toán**; báo giá có
**VietQR** để khách chuyển khoản nhanh, designer tự đánh dấu đã nhận tiền.

## Mục lục

| # | Tài liệu | Dùng khi |
|---|---|---|
| 01 | [PRD — Product Requirements](01-prd.md) | Hiểu phạm vi, người dùng, yêu cầu chức năng (FR) và phi chức năng (NFR) |
| 02 | [Business Rules & State Machines](02-business-rules.md) | Code domain logic: quota, billing, báo giá, trạng thái |
| 03 | [System Architecture](03-system-architecture.md) | Dựng solution, cấu trúc thư mục, công nghệ, deploy |
| 04 | [Database Design](04-database-design.md) | ERD, DDL PostgreSQL, constraint, index |
| 05 | [API Specification](05-api-spec.md) | Endpoint Admin + Portal, DTO, lỗi |
| 06 | [VietQR Specification](06-vietqr-spec.md) | Sinh payload QR chuyển khoản, CRC, test vector |
| 07 | [Security & Access Control](07-security.md) | Auth designer, magic link khách, phân quyền, upload |
| 08 | [UI/UX Specification](08-ui-spec.md) | Danh sách màn hình, nội dung, hành động |
| 09 | [Test Plan & Acceptance Criteria](09-test-plan.md) | Viết test, nghiệm thu |
| 10 | [Implementation Plan](10-implementation-plan.md) | Thứ tự làm, milestone, Definition of Done |
| 11 | [Architecture Decision Records](11-adr.md) | Lý do của các quyết định thiết kế |
| — | [CLAUDE.md template](CLAUDE.md.template) | Copy vào root repo khi bắt đầu code với Claude Code |

## Thứ tự đọc khuyến nghị

- **Bắt đầu code:** 01 → 02 → 03 → 04 → 10.
- **Làm 1 feature cụ thể:** FR tương ứng trong 01 → rule trong 02 → bảng trong 04 → endpoint trong 05 → AC trong 09.

## Thuật ngữ (Glossary)

| Thuật ngữ | Nghĩa |
|---|---|
| Designer | Người dùng duy nhất có quyền quản trị (admin/owner) |
| Client / Khách | Khách hàng của designer; chỉ xem và gửi request qua Portal |
| Portal | Giao diện dành cho khách, truy cập bằng magic link |
| Project | Một dự án thiết kế của một khách |
| Deliverable | Một hạng mục bàn giao trong dự án (VD: Logo, 5 Banner). Quota revision gắn ở đây |
| Brief | Mô tả yêu cầu/phạm vi; có version, bản đã confirm bị khóa |
| Client Request | Yêu cầu khách gửi qua Portal (hộp thư đến của designer) |
| Revision Round | Một đợt feedback cho một deliverable; là đơn vị tính quota |
| Revision Item | Một thay đổi cụ thể trong round |
| Quota | Số round sửa miễn phí của một deliverable (`included_rounds`) |
| Quotation / Báo giá | Báo giá INITIAL (ban đầu) hoặc ADDITIONAL (phát sinh); có version, snapshot |
| Snapshot | Bản chụp bất biến của báo giá tại thời điểm gửi |
| VietQR | Mã QR chuyển khoản liên ngân hàng chuẩn NAPAS/EMVCo |

## Ngoài phạm vi (toàn dự án)

- Thanh toán online, payment gateway, webhook, hóa đơn điện tử, invoice/payment ledger.
- Nhiều designer / multi-tenant / SaaS.
- Khách Accept/Reject hoặc chỉnh sửa dữ liệu trong app.
