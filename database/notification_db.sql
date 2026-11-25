-- =================================================================
-- NOTIFICATION DATABASE SCHEMA (notification_db)
-- =================================================================
-- Phục vụ NotificationAPI (UC thông báo hệ thống, thông báo rủi ro, v.v.)
-- Chạy MỘT LẦN, an toàn khi chạy lại (CREATE TABLE / INDEX IF NOT EXISTS).

BEGIN;

CREATE TABLE IF NOT EXISTS public.notifications (
    notification_id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id         INTEGER NOT NULL,
    title           VARCHAR(255) NOT NULL,
    message         TEXT NOT NULL,
    category        VARCHAR(50) NOT NULL DEFAULT 'event_new',
    target_url      VARCHAR(500) NULL,
    is_read         BOOLEAN NOT NULL DEFAULT false,
    read_at         TIMESTAMPTZ NULL,
    is_deleted      BOOLEAN NOT NULL DEFAULT false,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    delivery_key    VARCHAR(200) NULL
);

-- Chỉ mục tối ưu truy vấn thông báo người dùng
CREATE INDEX IF NOT EXISTS ix_notifications_user_deleted_created 
    ON public.notifications(user_id, is_deleted, created_at DESC);

-- Unique index chặn duplicate notification (Idempotency)
CREATE UNIQUE INDEX IF NOT EXISTS uq_notification_delivery_key 
    ON public.notifications(delivery_key) WHERE delivery_key IS NOT NULL;

COMMIT;
