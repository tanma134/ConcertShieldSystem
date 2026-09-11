-- ============================================================
-- queue_db  (QueueAPI)
-- Tables: queue_sessions, seats
-- Ghi chú: seats.seat_zone_id nên được cache thêm zone_name /
-- ticket_type_id ngay trong bảng (denormalize) vì đây là bảng
-- chịu tải cao nhất hệ thống -- xem phần lưu ý dưới cuối file.
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.queue_sessions
(
    queue_session_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,                      -- soft ref -> event_db.events
    user_id integer NOT NULL,                        -- soft ref -> identity_db.users
    queue_position integer,
    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Waiting'::character varying,
    entered_queue_at timestamp with time zone NOT NULL DEFAULT now(),
    activated_at timestamp with time zone,
    session_expires_at timestamp with time zone,
    CONSTRAINT queue_sessions_pkey PRIMARY KEY (queue_session_id)
);
CREATE INDEX IF NOT EXISTS ix_queue_sessions_event_id ON public.queue_sessions(event_id);

CREATE TABLE IF NOT EXISTS public.seats
(
    seat_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    seat_zone_id integer NOT NULL,                   -- soft ref -> event_db.seat_zones
    row_label character varying(10) COLLATE pg_catalog."default" NOT NULL,
    seat_number character varying(10) COLLATE pg_catalog."default" NOT NULL,
    x_coordinate integer,
    y_coordinate integer,
    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Available'::character varying,
    held_by_user_id integer,                         -- soft ref -> identity_db.users
    hold_expires_at timestamp with time zone,
    updated_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT seats_pkey PRIMARY KEY (seat_id),
    CONSTRAINT uq_seats_zone_row_number UNIQUE (seat_zone_id, row_label, seat_number)
);
CREATE INDEX IF NOT EXISTS ix_seats_seat_zone_id ON public.seats(seat_zone_id);
CREATE INDEX IF NOT EXISTS ix_seats_held_by_user_id ON public.seats(held_by_user_id);

-- ===== FK cùng DB =====
-- Không có bảng nào trong queue_db tham chiếu tới bảng khác trong cùng DB.

-- ===== Soft reference (KHÔNG có FK) =====
-- queue_sessions.event_id -> event_db.events
-- queue_sessions.user_id  -> identity_db.users
-- seats.seat_zone_id      -> event_db.seat_zones
-- seats.held_by_user_id   -> identity_db.users

-- KHUYẾN NGHỊ: nên thêm cột cache trên seats để tránh gọi ngược
-- EventAPI mỗi lần load ghế (bảng seats chịu tải cao nhất hệ thống):
--   zone_name character varying(100)
--   ticket_type_id integer
-- Các cột này được điền lúc EventAPI publish event
-- "seating_chart.published" và giữ đồng bộ qua event tiếp theo
-- nếu tổ chức sửa layout.

END;
