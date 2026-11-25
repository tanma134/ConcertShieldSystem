-- =================================================================
-- PAYMENT DB INITIALIZATION / BOOTSTRAP SCRIPT
-- =================================================================
-- Chay trong payment_db. An toan khi chay lai (IF NOT EXISTS).

BEGIN;

-- ---------- payment_transactions ----------
CREATE TABLE IF NOT EXISTS public.payment_transactions (
    payment_transaction_id   integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id                 integer NOT NULL,
    gateway                  varchar(30) NOT NULL DEFAULT 'VNPay',
    gateway_transaction_ref  varchar(100) NULL,
    amount                   bigint NOT NULL,
    status                   varchar(20) NOT NULL DEFAULT 'Initiated',
    webhook_payload          jsonb NULL,
    requested_at             timestamp with time zone NOT NULL DEFAULT now(),
    responded_at             timestamp with time zone NULL,
    user_id                  integer NULL,
    client_ip                varchar(64) NULL,
    device_fp                varchar(128) NULL,
    session_id               varchar(128) NULL
);

CREATE INDEX IF NOT EXISTS ix_payment_transactions_order_id ON public.payment_transactions(order_id);
CREATE INDEX IF NOT EXISTS ix_payment_transactions_user_status ON public.payment_transactions(user_id, status);
CREATE INDEX IF NOT EXISTS ix_payment_transactions_order_gateway_requested ON public.payment_transactions(order_id, gateway, requested_at DESC);
CREATE UNIQUE INDEX IF NOT EXISTS uq_payment_transactions_success ON public.payment_transactions(order_id, gateway) WHERE (status = 'Success');

-- ---------- vouchers ----------
CREATE TABLE IF NOT EXISTS public.vouchers (
    voucher_id               integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code                     varchar(50) NOT NULL UNIQUE,
    scope                    varchar(20) NOT NULL,
    organizer_id             integer NULL,
    event_id                 integer NULL,
    discount_type            varchar(10) NOT NULL,
    discount_amount          bigint NULL,
    discount_percent         numeric(5,2) NULL,
    max_discount_amount      bigint NULL,
    min_order_amount         bigint DEFAULT 0,
    total_quantity           integer NOT NULL,
    used_quantity            integer NOT NULL DEFAULT 0,
    max_usage_per_user       integer NOT NULL DEFAULT 1,
    starts_at                timestamp with time zone NOT NULL,
    ends_at                  timestamp with time zone NOT NULL,
    is_active                boolean NOT NULL DEFAULT true,
    created_by               integer NOT NULL,
    created_at               timestamp with time zone NOT NULL DEFAULT now(),
    is_deleted               boolean NOT NULL DEFAULT false,
    deleted_at               timestamp with time zone NULL,
    deleted_by               integer NULL
);

CREATE INDEX IF NOT EXISTS ix_vouchers_event_id ON public.vouchers(event_id);
CREATE INDEX IF NOT EXISTS ix_vouchers_organizer_id ON public.vouchers(organizer_id);

-- ---------- voucher_usages ----------
CREATE TABLE IF NOT EXISTS public.voucher_usages (
    voucher_usage_id         integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    voucher_id               integer NOT NULL REFERENCES public.vouchers(voucher_id),
    order_id                 integer NOT NULL,
    user_id                  integer NOT NULL,
    discount_amount          bigint NOT NULL,
    used_at                  timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT uq_voucher_usages_voucher_order_user UNIQUE (voucher_id, order_id, user_id)
);

CREATE INDEX IF NOT EXISTS ix_voucher_usages_user ON public.voucher_usages(voucher_id, user_id);

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
CREATE UNIQUE INDEX IF NOT EXISTS uq_payment_refunds_return_request ON public.payment_refunds (return_request_id);

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

COMMIT;
