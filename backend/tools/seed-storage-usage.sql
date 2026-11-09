-- ============================================================================================
-- CHỈ dùng DB dev. Không bao giờ chạy trên dữ liệu thật.
--
-- Chèn các dòng ảnh GIẢ vào bảng `files` (không tạo file thật trong storage) để dung lượng đang dùng
-- đạt :target_percent % của quota — dùng để xem các ngưỡng 70/85/90/95 % trên Dashboard và trang Dung lượng
-- (docs/04-database-design.md "files", Domain/Files/ProjectFile.CreateImage).
--
-- Cách chạy:
--   psql "$DEV_DATABASE_URL" -v target_percent=85 -f backend/tools/seed-storage-usage.sql
--   psql "$DEV_DATABASE_URL" -v target_percent=95 -v quota_bytes=1073741824 -f backend/tools/seed-storage-usage.sql
--
--   target_percent  bắt buộc, 1..100: mức sử dụng cần đạt (đã tính cả dung lượng ảnh đang có)
--   dev_db          tuỳ chọn, mặc định freelanceflow: script thoát nếu current_database() khác tên này
--   quota_bytes     tuỳ chọn, mặc định 10737418240 (10 GiB = Storage:QuotaBytes trong appsettings.json)
--
-- Mỗi dòng giả có tên "seed-storage-N.webp", sha256 ngẫu nhiên, thuộc dự án cũ nhất và là ảnh "Cập nhật tiến độ".
-- Xoá bản giả: DELETE FROM files WHERE original_name LIKE 'seed-storage-%';
-- (ảnh giả không có object thật: mở/xem ảnh sẽ lỗi, nhưng danh sách và các con số dung lượng hoạt động)
-- ============================================================================================
\set ON_ERROR_STOP on
\if :{?target_percent}
\else
  \echo 'Thiếu -v target_percent=<1..100>'
  \quit
\endif
\if :{?quota_bytes}
\else
  \set quota_bytes 10737418240
\endif

-- Guard: refuse to run anywhere but the dev database (override only with -v dev_db=<name> for another dev DB).
\if :{?dev_db}
\else
  \set dev_db freelanceflow
\endif
SELECT (current_database() = :'dev_db') AS is_dev_db \gset
\if :is_dev_db
\else
  \echo 'Từ chối: database hiện tại không phải' :dev_db '- script chỉ dùng cho DB dev.'
  \quit
\endif

BEGIN;

WITH missing AS (
  SELECT GREATEST(
           0,
           (:quota_bytes::numeric * :target_percent::numeric / 100)::bigint
             - COALESCE((SELECT SUM(size_bytes) FROM files WHERE deleted_at IS NULL), 0)
         ) AS bytes
),
plan AS (
  -- rows of up to 50 MiB each
  SELECT bytes, CEIL(bytes / 52428800.0)::int AS n FROM missing
),
target_project AS (
  SELECT id FROM projects ORDER BY created_at LIMIT 1
)
INSERT INTO files (
  id, project_id, kind, original_name, content_type, size_bytes, storage_key, sha256,
  visible_to_client, uploaded_by, created_at,
  owner_type, owner_id, width, height, original_bytes, preview_bytes, thumb_bytes
)
SELECT
  gen_random_uuid(),
  p.id,
  'PREVIEW',
  'seed-storage-' || g || '.webp',
  'image/webp',
  LEAST(52428800, plan.bytes - (g - 1) * 52428800),
  'seed/' || gen_random_uuid(),
  decode(md5(random()::text) || md5(random()::text), 'hex'),
  false,
  'DESIGNER',
  now() - (g || ' days')::interval,
  'PROGRESS_UPDATE',
  gen_random_uuid(),
  4000, 3000,
  GREATEST(3, LEAST(52428800, plan.bytes - (g - 1) * 52428800) * 80 / 100),
  GREATEST(2, LEAST(52428800, plan.bytes - (g - 1) * 52428800) * 15 / 100),
  GREATEST(1, LEAST(52428800, plan.bytes - (g - 1) * 52428800) * 5 / 100)
FROM plan
CROSS JOIN target_project p
CROSS JOIN LATERAL generate_series(1::bigint, plan.n) AS g
WHERE plan.bytes > 0;

SELECT
  pg_size_pretty(COALESCE(SUM(size_bytes), 0)) AS used,
  round(100.0 * COALESCE(SUM(size_bytes), 0) / :quota_bytes::numeric, 1) AS percent_of_quota
FROM files
WHERE deleted_at IS NULL;

COMMIT;
