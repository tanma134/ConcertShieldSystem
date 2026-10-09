

-- =================================================================
-- PAYMENT REFUND EXTENSION ONLY - ORIGINAL ZIP HAS NO BASE PAYMENT SCHEMA
-- =================================================================
-- CHẠY TRONG payment_db (04_payment_db)
-- Chạy MỘT LẦN, an toàn khi chạy lại (CREATE/ALTER ... IF NOT EXISTS).


-- ---------- 011 payment refunds ----------
CREATE TABLE IF NOT EXISTS public.payment_refunds (
    payment_refund_id        integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    return_request_id        integer NOT NULL,
    order_id                 integer NOT NULL,
    amount                   bigint  NOT NULL,
    gateway                  varchar(30) NOT NULL DEFAULT 'VNPay',
    gateway_refund_ref       varchar(100) NULL,
    status                   varchar(20) NOT NULL DEFAULT 'Pending',
    error_message            varchar(500) NULL,
    created_at               timestamp with time zone NOT NULL DEFAULT now(),
    completed_at             timestamp with time zone NULL,
    CONSTRAINT ck_payment_refunds_status CHECK (status IN ('Pending', 'Refunded', 'Failed')),
    CONSTRAINT uq_payment_refunds_return UNIQUE (return_request_id)
);
CREATE INDEX IF NOT EXISTS ix_payment_refunds_order ON public.payment_refunds (order_id);
