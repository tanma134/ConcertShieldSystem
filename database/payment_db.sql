-- =================================================================
-- PAYMENT DB INITIALIZATION / EXTENSION SCRIPT
-- =================================================================
-- CHẠY TRONG payment_db (04_payment_db)
-- Chạy MỘT LẦN, an toàn khi chạy lại (CREATE/ALTER ... IF NOT EXISTS).

-- ---------- payment_refunds ----------
CREATE TABLE IF NOT EXISTS public.payment_refunds (
    payment_refund_id        integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    return_request_id        integer NULL,
    order_id                 integer NOT NULL,
    amount                   bigint  NOT NULL,
    gateway                  varchar(30) NOT NULL DEFAULT 'VNPay',
    gateway_refund_ref       varchar(100) NULL,
    status                   varchar(20) NOT NULL DEFAULT 'Pending',
    error_message            varchar(500) NULL,
    created_at               timestamp with time zone NOT NULL DEFAULT now(),
    completed_at             timestamp with time zone NULL,
    refund_type              varchar(20) DEFAULT 'Full',
    reason                   varchar(255) NULL,
    processed_by_user_id     integer NULL,
    processed_by_user_name   varchar(100) NULL,
    customer_name            varchar(150) NULL,
    gateway_error_code       varchar(50) NULL,
    retry_count              integer NOT NULL DEFAULT 0,
    last_retry_at            timestamp with time zone NULL,
    gateway_amount           bigint NULL,
    reconciliation_status    varchar(30) DEFAULT 'Matched',
    event_id                 integer NULL,
    event_name               varchar(255) NULL,
    CONSTRAINT ck_payment_refunds_status CHECK (status IN ('Pending', 'Refunded', 'Failed')),
    CONSTRAINT uq_payment_refunds_return UNIQUE (return_request_id)
);

CREATE INDEX IF NOT EXISTS ix_payment_refunds_order ON public.payment_refunds (order_id);

-- An toàn cho database cũ đã tạo trước đó:
ALTER TABLE public.payment_refunds ALTER COLUMN return_request_id DROP NOT NULL;
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS refund_type varchar(20) DEFAULT 'Full';
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS reason varchar(255);
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS processed_by_user_id integer;
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS processed_by_user_name varchar(100);
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS customer_name varchar(150);
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS gateway_error_code varchar(50);
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS retry_count integer NOT NULL DEFAULT 0;
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS last_retry_at timestamp with time zone;
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS gateway_amount bigint;
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS reconciliation_status varchar(30) DEFAULT 'Matched';
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS event_id integer;
ALTER TABLE public.payment_refunds ADD COLUMN IF NOT EXISTS event_name varchar(255);

-- ---------- payment_reconciliation_resolutions ----------
CREATE TABLE IF NOT EXISTS public.payment_reconciliation_resolutions (
    payment_reconciliation_resolution_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    payment_transaction_id               integer NOT NULL,
    resolution_action                    varchar(50) NOT NULL,
    resolution_note                      text NOT NULL,
    resolution_status                    varchar(20) NOT NULL,
    resolved_by_user_id                  integer NOT NULL,
    resolved_at                          timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ck_reconciliation_resolution_status CHECK (resolution_status IN ('Resolved', 'Dismissed'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_reconciliation_resolution_transaction 
    ON public.payment_reconciliation_resolutions (payment_transaction_id);
