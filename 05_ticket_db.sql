-- ============================================================
-- ticket_db  (TicketAPI)
-- Tables: orders, order_details, tickets, ticket_qr_tokens
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.orders
(
    order_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    customer_id integer NOT NULL,                     -- soft ref -> identity_db.users
    event_id integer NOT NULL,                          -- soft ref -> event_db.events
    queue_session_id integer,                            -- soft ref -> queue_db.queue_sessions
    voucher_id integer,                                   -- soft ref -> payment_db.vouchers
    full_name character varying(255) COLLATE pg_catalog."default",
    email character varying(255) COLLATE pg_catalog."default",
    phone character varying(50) COLLATE pg_catalog."default",
    event_name text COLLATE pg_catalog."default",         -- denormalized snapshot
    poster_url text COLLATE pg_catalog."default",          -- denormalized snapshot
    order_date timestamp with time zone DEFAULT now(),
    starts_at timestamp with time zone,
    ends_at timestamp with time zone,
    total_amount bigint NOT NULL,
    discount_amount bigint DEFAULT 0,
    final_amount bigint NOT NULL,
    payment_method character varying(50) COLLATE pg_catalog."default",
    status character varying(20) COLLATE pg_catalog."default" DEFAULT 'Pending'::character varying,
    expires_at timestamp with time zone,
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,                                    -- soft ref -> identity_db.users
    CONSTRAINT orders_pkey PRIMARY KEY (order_id)
);
CREATE INDEX IF NOT EXISTS ix_orders_customer_id ON public.orders(customer_id);
CREATE INDEX IF NOT EXISTS ix_orders_event_id ON public.orders(event_id);

CREATE TABLE IF NOT EXISTS public.order_details
(
    order_detail_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    order_id integer NOT NULL,
    ticket_type_id integer NOT NULL,                     -- soft ref -> event_db.ticket_types
    ticket_type_name character varying(100) COLLATE pg_catalog."default", -- denormalized snapshot
    quantity integer NOT NULL,
    unit_price bigint NOT NULL,
    CONSTRAINT order_details_pkey PRIMARY KEY (order_detail_id)
);
CREATE INDEX IF NOT EXISTS ix_order_details_order_id ON public.order_details(order_id);

CREATE TABLE IF NOT EXISTS public.tickets
(
    ticket_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    ticket_code uuid NOT NULL DEFAULT gen_random_uuid(),
    order_id integer NOT NULL,
    event_id integer NOT NULL,                            -- soft ref -> event_db.events
    ticket_type_id integer NOT NULL,                        -- soft ref -> event_db.ticket_types
    seat_id integer,                                         -- soft ref -> queue_db.seats
    owner_user_id integer,                                   -- soft ref -> identity_db.users
    ticket_type_name character varying(100) COLLATE pg_catalog."default", -- denormalized snapshot
    owner_name character varying(100) COLLATE pg_catalog."default",       -- denormalized snapshot
    status character varying(20) COLLATE pg_catalog."default" DEFAULT 'Active'::character varying,
    checked_in_at timestamp with time zone,
    checked_in_by integer,                                    -- soft ref -> identity_db.users (staff)
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,                                        -- soft ref -> identity_db.users
    CONSTRAINT tickets_pkey PRIMARY KEY (ticket_id),
    CONSTRAINT tickets_ticket_code_key UNIQUE (ticket_code)
);
CREATE INDEX IF NOT EXISTS ix_tickets_event_id ON public.tickets(event_id);
CREATE INDEX IF NOT EXISTS ix_tickets_order_id ON public.tickets(order_id);
CREATE INDEX IF NOT EXISTS ix_tickets_owner_user_id ON public.tickets(owner_user_id);
CREATE INDEX IF NOT EXISTS uq_tickets_seat_active ON public.tickets(seat_id);
CREATE INDEX IF NOT EXISTS ix_tickets_ticket_type_id ON public.tickets(ticket_type_id);

CREATE TABLE IF NOT EXISTS public.ticket_qr_tokens
(
    ticket_qr_token_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    ticket_id integer NOT NULL,
    qr_token character varying(200) COLLATE pg_catalog."default" NOT NULL,
    issued_at timestamp with time zone NOT NULL DEFAULT now(),
    expires_at timestamp with time zone NOT NULL,
    is_revoked boolean NOT NULL DEFAULT false,
    revoked_at timestamp with time zone,
    is_used boolean NOT NULL DEFAULT false,
    used_at timestamp with time zone,
    CONSTRAINT ticket_qr_tokens_pkey PRIMARY KEY (ticket_qr_token_id),
    CONSTRAINT ticket_qr_tokens_qr_token_key UNIQUE (qr_token)
);
CREATE INDEX IF NOT EXISTS uq_ticket_qr_tokens_active ON public.ticket_qr_tokens(ticket_id);

-- ===== FK cùng DB (giữ nguyên) =====

ALTER TABLE IF EXISTS public.order_details
    ADD CONSTRAINT fk_order_details_order FOREIGN KEY (order_id)
    REFERENCES public.orders (order_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.tickets
    ADD CONSTRAINT fk_tickets_order FOREIGN KEY (order_id)
    REFERENCES public.orders (order_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.ticket_qr_tokens
    ADD CONSTRAINT fk_ticket_qr_tokens_ticket FOREIGN KEY (ticket_id)
    REFERENCES public.tickets (ticket_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

-- ===== Soft reference (KHÔNG có FK) =====
-- orders.customer_id/deleted_by -> identity_db.users
-- orders.event_id -> event_db.events
-- orders.queue_session_id -> queue_db.queue_sessions
-- orders.voucher_id -> payment_db.vouchers
-- order_details.ticket_type_id -> event_db.ticket_types
-- tickets.event_id/ticket_type_id -> event_db
-- tickets.seat_id -> queue_db.seats  (KHUYẾN NGHỊ: lưu thêm seat_label
--   snapshot vì seat có thể bị đổi trạng thái ở queue_db sau khi vé phát hành)
-- tickets.owner_user_id/checked_in_by/deleted_by -> identity_db.users

-- SAGA: việc phát hành vé (INSERT tickets) phải phối hợp với
-- QueueAPI (confirm hold -> release lock) và PaymentAPI (payment
-- succeeded) trong cùng 1 luồng nghiệp vụ checkout.

END;
