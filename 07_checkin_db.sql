-- ============================================================
-- checkin_db  (CheckinAPI)
-- Tables: validator_roles, event_gates, event_staff,
--         gate_scan_logs, incident_reports
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.validator_roles
(
    validator_role_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    role_code character varying(30) COLLATE pg_catalog."default" NOT NULL,
    role_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    description character varying(300) COLLATE pg_catalog."default",
    CONSTRAINT validator_roles_pkey PRIMARY KEY (validator_role_id),
    CONSTRAINT validator_roles_role_code_key UNIQUE (role_code)
);

CREATE TABLE IF NOT EXISTS public.event_gates
(
    gate_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,                            -- soft ref -> event_db.events
    gate_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    location character varying(200) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT event_gates_pkey PRIMARY KEY (gate_id)
);
CREATE INDEX IF NOT EXISTS ix_event_gates_event_id ON public.event_gates(event_id);

CREATE TABLE IF NOT EXISTS public.event_staff
(
    event_staff_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,                            -- soft ref -> event_db.events
    staff_id integer NOT NULL,                              -- soft ref -> identity_db.users
    validator_role_id integer,
    assigned_at timestamp with time zone NOT NULL DEFAULT now(),
    expired_at timestamp with time zone,
    is_active boolean NOT NULL DEFAULT true,
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,                                     -- soft ref -> identity_db.users
    CONSTRAINT event_staff_pkey PRIMARY KEY (event_staff_id)
);
CREATE INDEX IF NOT EXISTS ix_event_staff_event_id ON public.event_staff(event_id);

CREATE TABLE IF NOT EXISTS public.gate_scan_logs
(
    gate_scan_log_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    ticket_id integer,                                       -- soft ref -> ticket_db.tickets
    event_id integer NOT NULL,                                -- soft ref -> event_db.events
    gate_id integer,
    scanned_by integer NOT NULL,                               -- soft ref -> identity_db.users
    scanner_device_id character varying(100) COLLATE pg_catalog."default",
    scan_result character varying(20) COLLATE pg_catalog."default" NOT NULL,
    face_match_score numeric(5, 2),
    liveness_score numeric(5, 2),
    device_info character varying(200) COLLATE pg_catalog."default",
    scanned_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT gate_scan_logs_pkey PRIMARY KEY (gate_scan_log_id)
);
CREATE INDEX IF NOT EXISTS ix_gate_scan_logs_event_id ON public.gate_scan_logs(event_id);

CREATE TABLE IF NOT EXISTS public.incident_reports
(
    incident_report_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,                                -- soft ref -> event_db.events
    reported_by integer NOT NULL,                               -- soft ref -> identity_db.users
    incident_type character varying(30) COLLATE pg_catalog."default" NOT NULL,
    description character varying(1000) COLLATE pg_catalog."default",
    related_ticket_id integer,                                   -- soft ref -> ticket_db.tickets
    lookup_phone_masked character varying(20) COLLATE pg_catalog."default",
    lookup_cccd_last4 character varying(4) COLLATE pg_catalog."default",
    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Open'::character varying,
    resolved_by integer,                                          -- soft ref -> identity_db.users
    resolved_at timestamp with time zone,
    resolution_note character varying(1000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT incident_reports_pkey PRIMARY KEY (incident_report_id)
);
CREATE INDEX IF NOT EXISTS ix_incident_reports_event_id ON public.incident_reports(event_id);

-- ===== FK cùng DB (giữ nguyên) =====

ALTER TABLE IF EXISTS public.event_staff
    ADD CONSTRAINT fk_event_staff_validator_role FOREIGN KEY (validator_role_id)
    REFERENCES public.validator_roles (validator_role_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.gate_scan_logs
    ADD CONSTRAINT fk_gate_scan_logs_gate FOREIGN KEY (gate_id)
    REFERENCES public.event_gates (gate_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

-- ===== Soft reference (KHÔNG có FK) =====
-- event_gates.event_id / event_staff.event_id / gate_scan_logs.event_id
--   / incident_reports.event_id -> event_db.events
-- event_staff.staff_id/deleted_by -> identity_db.users
-- gate_scan_logs.ticket_id / incident_reports.related_ticket_id -> ticket_db.tickets
-- gate_scan_logs.scanned_by -> identity_db.users
-- incident_reports.reported_by/resolved_by -> identity_db.users

-- LƯU Ý: xác thực vé lúc quét QR (UC_29) cần gọi ĐỒNG BỘ sang
-- TicketAPI (không thể chờ eventual consistency) để chặn vé giả
-- ngay tại cổng.

END;
