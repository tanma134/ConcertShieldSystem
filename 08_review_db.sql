-- ============================================================
-- review_db  (ReviewAPI)
-- Tables: reviews
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.reviews
(
    review_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,                          -- soft ref -> event_db.events
    user_id integer NOT NULL,                             -- soft ref -> identity_db.users
    rating integer NOT NULL,
    comment character varying(1000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone,
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,                                   -- soft ref -> identity_db.users
    CONSTRAINT reviews_pkey PRIMARY KEY (review_id),
    CONSTRAINT uq_reviews_event_user UNIQUE (event_id, user_id)
);
CREATE INDEX IF NOT EXISTS ix_reviews_event_id ON public.reviews(event_id);

-- ===== FK cùng DB =====
-- Không có bảng nào khác trong review_db.

-- ===== Soft reference (KHÔNG có FK) =====
-- reviews.event_id -> event_db.events
-- reviews.user_id/deleted_by -> identity_db.users

-- Ghi chú nghiệp vụ: UC_8.1.2 yêu cầu chỉ khách đã check-in mới
-- được review. ReviewAPI cần gọi TicketAPI/CheckinAPI để xác
-- minh trạng thái "Checked-in" trước khi cho phép tạo review
-- (đồng bộ, thực hiện ngay lúc submit).

END;
