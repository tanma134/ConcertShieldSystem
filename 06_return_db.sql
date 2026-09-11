-- ============================================================
-- return_db  (ReturnAPI)
-- Tables: ticket_return_requests, disputes
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.ticket_return_requests
(
    return_request_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    ticket_id integer NOT NULL,                          -- soft ref -> ticket_db.tickets
    order_id integer NOT NULL,                             -- soft ref -> ticket_db.orders
    requested_by integer NOT NULL,                          -- soft ref -> identity_db.users
    refund_policy_id integer,                                -- soft ref -> event_db.refund_policies
    reason character varying(500) COLLATE pg_catalog."default",
    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Requested'::character varying,
    refund_amount bigint,
    reissued_to_user_id integer,                              -- soft ref -> identity_db.users
    reissued_ticket_id integer,                                -- soft ref -> ticket_db.tickets
    processed_by integer,                                       -- soft ref -> identity_db.users
    requested_at timestamp with time zone NOT NULL DEFAULT now(),
    processed_at timestamp with time zone,
    CONSTRAINT ticket_return_requests_pkey PRIMARY KEY (return_request_id)
);
CREATE INDEX IF NOT EXISTS ix_return_req_ticket_id ON public.ticket_return_requests(ticket_id);

CREATE TABLE IF NOT EXISTS public.disputes
(
    dispute_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    dispute_type character varying(30) COLLATE pg_catalog."default" NOT NULL,
    related_event_id integer,                                    -- soft ref -> event_db.events
    related_order_id integer,                                     -- soft ref -> ticket_db.orders
    related_ticket_id integer,                                     -- soft ref -> ticket_db.tickets
    raised_by integer NOT NULL,                                     -- soft ref -> identity_db.users
    assigned_to integer,                                             -- soft ref -> identity_db.users
    description character varying(2000) COLLATE pg_catalog."default",
    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Open'::character varying,
    resolution character varying(2000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    resolved_at timestamp with time zone,
    CONSTRAINT disputes_pkey PRIMARY KEY (dispute_id)
);

-- ===== FK cùng DB =====
-- Không có bảng nào trong return_db tham chiếu tới bảng khác cùng DB.

-- ===== Soft reference (KHÔNG có FK) =====
-- ticket_return_requests.ticket_id/reissued_ticket_id -> ticket_db.tickets
-- ticket_return_requests.order_id -> ticket_db.orders
-- ticket_return_requests.requested_by/reissued_to_user_id/processed_by -> identity_db.users
-- ticket_return_requests.refund_policy_id -> event_db.refund_policies
-- disputes.related_event_id -> event_db.events
-- disputes.related_order_id -> ticket_db.orders
-- disputes.related_ticket_id -> ticket_db.tickets
-- disputes.raised_by/assigned_to -> identity_db.users

END;
