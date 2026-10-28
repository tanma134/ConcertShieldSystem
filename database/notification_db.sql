

-- =================================================================
-- NOTIFICATION DELIVERY KEYS
-- =================================================================
-- CHẠY TRONG notification_db
-- Chạy MỘT LẦN, an toàn khi chạy lại (CREATE/ALTER ... IF NOT EXISTS).


-- ---------- 010 notification keys ----------
-- Chạy trong notification_db, không chạy ở EventDB/TicketDB.
BEGIN;
CREATE TABLE IF NOT EXISTS notifications (
 notification_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 user_id integer NOT NULL, title varchar(255) NOT NULL, message text NOT NULL,
 category varchar(50) NOT NULL DEFAULT 'event_new', target_url varchar(500),
 is_read boolean NOT NULL DEFAULT false, read_at timestamptz,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE notifications ADD COLUMN IF NOT EXISTS delivery_key varchar(200);
CREATE UNIQUE INDEX IF NOT EXISTS uq_notification_delivery_key ON notifications(delivery_key) WHERE delivery_key IS NOT NULL;
COMMIT;
