-- =============================================================================
-- ConcertShield — Migration: cho phép loại change request 'Cancel'
-- File: event_change_cancel_type.sql
--
-- Lý do: ràng buộc CHECK trên event_change_requests chỉ cho phép
--        'Postpone' | 'Reschedule', nên yêu cầu huỷ concert của Organizer
--        (type = 'Cancel') không thể ghi xuống DB.
-- Áp dụng cho database đang chạy (event_db.sql chỉ dùng khi tạo mới).
-- =============================================================================

\c event_db;

BEGIN;

-- 1. Nới ràng buộc type để chấp nhận 'Cancel'
ALTER TABLE public.event_change_requests
    DROP CONSTRAINT IF EXISTS event_change_requests_type_check;

ALTER TABLE public.event_change_requests
    ADD CONSTRAINT event_change_requests_type_check
    CHECK (type IN ('Postpone', 'Reschedule', 'Cancel'));

-- 2. Nới ràng buộc về ngày: 'Cancel' (giống 'Postpone') không có lịch mới
ALTER TABLE public.event_change_requests
    DROP CONSTRAINT IF EXISTS event_change_requests_check;

ALTER TABLE public.event_change_requests
    ADD CONSTRAINT event_change_requests_check
    CHECK (
        (type IN ('Postpone', 'Cancel') AND new_starts_at IS NULL AND new_ends_at IS NULL)
        OR (type = 'Reschedule' AND new_starts_at IS NOT NULL AND new_ends_at > new_starts_at)
    );

-- 3. Cột lưu tài liệu chứng cứ pháp lý đính kèm yêu cầu thay đổi/huỷ
ALTER TABLE public.event_change_requests
    ADD COLUMN IF NOT EXISTS evidence_url text;

COMMIT;

-- Kiểm tra sau khi chạy:
--   SELECT conname, pg_get_constraintdef(oid)
--   FROM pg_constraint
--   WHERE conrelid = 'public.event_change_requests'::regclass;
