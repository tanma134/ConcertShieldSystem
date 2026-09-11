-- ============================================================
-- payment_db  (PaymentAPI)
-- Tables: vouchers, voucher_usages, payment_transactions
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.vouchers
(
    voucher_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    code character varying(50) COLLATE pg_catalog."default" NOT NULL,
    scope character varying(20) COLLATE pg_catalog."default" NOT NULL,
    organizer_id integer,                            -- soft ref -> identity_db.users
    event_id integer,                                 -- soft ref -> event_db.events
    discount_type character varying(10) COLLATE pg_catalog."default" NOT NULL,
    discount_amount bigint,
    discount_percent numeric(5, 2),
    max_discount_amount bigint,
    min_order_amount bigint DEFAULT 0,
    total_quantity integer NOT NULL,
    used_quantity integer NOT NULL DEFAULT 0,
    max_usage_per_user integer NOT NULL DEFAULT 1,
    starts_at timestamp with time zone NOT NULL,
    ends_at timestamp with time zone NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_by integer NOT NULL,                      -- soft ref -> identity_db.users
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,                                -- soft ref -> identity_db.users
    CONSTRAINT vouchers_pkey PRIMARY KEY (voucher_id),
    CONSTRAINT vouchers_code_key UNIQUE (code)
);
CREATE INDEX IF NOT EXISTS ix_vouchers_event_id ON public.vouchers(event_id);

CREATE TABLE IF NOT EXISTS public.voucher_usages
(
    voucher_usage_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    voucher_id integer NOT NULL,
    order_id integer NOT NULL,                        -- soft ref -> ticket_db.orders
    user_id integer NOT NULL,                          -- soft ref -> identity_db.users
    discount_amount bigint NOT NULL,
    used_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT voucher_usages_pkey PRIMARY KEY (voucher_usage_id),
    CONSTRAINT uq_voucher_usages_voucher_order_user UNIQUE (voucher_id, order_id, user_id)
);

CREATE TABLE IF NOT EXISTS public.payment_transactions
(
    payment_transaction_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    order_id integer NOT NULL,                          -- soft ref -> ticket_db.orders
    gateway character varying(30) COLLATE pg_catalog."default" NOT NULL DEFAULT 'VNPay'::character varying,
    gateway_transaction_ref character varying(100) COLLATE pg_catalog."default",
    amount bigint NOT NULL,
    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Initiated'::character varying,
    webhook_payload jsonb,
    requested_at timestamp with time zone NOT NULL DEFAULT now(),
    responded_at timestamp with time zone,
    CONSTRAINT payment_transactions_pkey PRIMARY KEY (payment_transaction_id)
);
CREATE INDEX IF NOT EXISTS ix_payment_transactions_order_id ON public.payment_transactions(order_id);

-- ===== FK cùng DB (giữ nguyên) =====

ALTER TABLE IF EXISTS public.voucher_usages
    ADD CONSTRAINT fk_voucher_usages_voucher FOREIGN KEY (voucher_id)
    REFERENCES public.vouchers (voucher_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

-- ===== Soft reference (KHÔNG có FK) =====
-- vouchers.organizer_id / created_by / deleted_by -> identity_db.users
-- vouchers.event_id -> event_db.events
-- voucher_usages.order_id / payment_transactions.order_id -> ticket_db.orders
-- voucher_usages.user_id -> identity_db.users

-- LƯU Ý: khi checkout, PaymentAPI nên trả về voucher_code +
-- discount_amount đã tính để TicketAPI lưu SNAPSHOT vào orders,
-- không cần giữ FK sống sang payment_db.

END;
