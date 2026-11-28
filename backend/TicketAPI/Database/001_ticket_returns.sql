-- UC_9: ticket return requests. Apply once to 05_ticket_db.
-- A ticket can have many requests over time, but only ONE open (Pending) request.
CREATE TABLE IF NOT EXISTS public.ticket_return_requests (
    ticket_return_request_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ticket_id        integer      NOT NULL,
    order_id         integer      NOT NULL,
    event_id         integer      NOT NULL,
    requester_user_id integer     NOT NULL,
    reason           varchar(500) NOT NULL,
    status           varchar(40)  NOT NULL DEFAULT 'Pending',
    refund_amount    bigint       NOT NULL DEFAULT 0,
    created_at       timestamp with time zone NOT NULL DEFAULT now(),
    updated_at       timestamp with time zone NOT NULL DEFAULT now(),
    cancelled_at     timestamp with time zone NULL,
    reviewed_by      integer      NULL,
    reviewed_at      timestamp with time zone NULL,
    review_note      varchar(500) NULL,
    bank_account_name varchar(100) NULL,
    bank_account_number varchar(30) NULL,
    bank_name varchar(100) NULL,
    manual_refund_reference varchar(100) NULL,
    manual_refund_processed_by integer NULL,
    manual_refund_processed_at timestamptz NULL,
    CONSTRAINT fk_ticket_return_requests_ticket FOREIGN KEY (ticket_id) REFERENCES public.tickets (ticket_id),
    CONSTRAINT ck_ticket_return_requests_status CHECK (status IN ('Pending', 'Approved', 'ApprovedPendingManualRefund', 'Rejected', 'Cancelled', 'Refunded', 'RefundFailed', 'RefundProcessing', 'RefundInfoRequired'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_ticket_return_requests_open
    ON public.ticket_return_requests (ticket_id) WHERE status = 'Pending';
CREATE INDEX IF NOT EXISTS ix_ticket_return_requests_requester
    ON public.ticket_return_requests (requester_user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_ticket_return_requests_event
    ON public.ticket_return_requests (event_id, status);
