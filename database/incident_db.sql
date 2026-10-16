-- =================================================================
-- INCIDENT BASE SCHEMA
-- =================================================================
-- ============================================================
-- incident_db  (IncidentAPI)
-- Phục vụ các Use Case:
--   UC_16.1 Report On-site Incident
--   UC_16.2 Attach Evidence
--   UC_16.3 Update Incident Report
--   UC_16.4 Escalate Incident to Admin
--   UC_16.5 View Resolution
-- Tables: incidents, incident_evidences, incident_updates
-- ============================================================
BEGIN;

-- 1. Bảng lưu trữ thông tin báo cáo sự cố tại hiện trường (UC_16.1, UC_16.3, UC_16.4, UC_16.5)
CREATE TABLE IF NOT EXISTS public.incidents
(
    id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,                                       -- Soft reference -> event_db.events
    event_name character varying(255) COLLATE pg_catalog."default",
    reporter_id integer NOT NULL,                                    -- Soft reference -> identity_db.users (Staff reporting)
    reporter_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    title character varying(200) COLLATE pg_catalog."default" NOT NULL,
    category character varying(50) COLLATE pg_catalog."default" NOT NULL,    -- Medical, Security, Technical, CrowdControl, Facility, Other
    severity character varying(20) COLLATE pg_catalog."default" NOT NULL,    -- Low, Medium, High, Critical
    location character varying(255) COLLATE pg_catalog."default" NOT NULL,
    description text COLLATE pg_catalog."default" NOT NULL,
    status character varying(30) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Reported', -- Reported, Investigating, InProgress, Escalated, Resolved, Closed
    is_escalated boolean NOT NULL DEFAULT false,
    escalated_to_admin_id integer,                                   -- Soft reference -> identity_db.users (Admin)
    escalated_at timestamp with time zone,
    escalation_reason text COLLATE pg_catalog."default",
    resolution_summary text COLLATE pg_catalog."default",
    action_taken text COLLATE pg_catalog."default",
    resolved_by_id integer,                                          -- Soft reference -> identity_db.users
    resolved_by_name character varying(100) COLLATE pg_catalog."default",
    resolved_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone,
    CONSTRAINT incidents_pkey PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_incidents_event_id ON public.incidents(event_id);
CREATE INDEX IF NOT EXISTS ix_incidents_reporter_id ON public.incidents(reporter_id);
CREATE INDEX IF NOT EXISTS ix_incidents_status ON public.incidents(status);
CREATE INDEX IF NOT EXISTS ix_incidents_severity ON public.incidents(severity);
CREATE INDEX IF NOT EXISTS ix_incidents_created_at ON public.incidents(created_at);

-- 2. Bảng lưu trữ bằng chứng đính kèm sự cố - hình ảnh, video, audio, tài liệu (UC_16.2)
CREATE TABLE IF NOT EXISTS public.incident_evidences
(
    id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    incident_id integer NOT NULL,
    file_url character varying(1000) COLLATE pg_catalog."default" NOT NULL,
    public_id character varying(255) COLLATE pg_catalog."default" NOT NULL,
    file_name character varying(255) COLLATE pg_catalog."default" NOT NULL,
    file_type character varying(50) COLLATE pg_catalog."default" NOT NULL,
    file_size bigint NOT NULL,
    mime_type character varying(100) COLLATE pg_catalog."default" NOT NULL,
    caption character varying(500) COLLATE pg_catalog."default",
    uploaded_by_id integer NOT NULL,
    uploaded_by_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    uploaded_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT incident_evidences_pkey PRIMARY KEY (id),
    CONSTRAINT fk_incident_evidences_incident FOREIGN KEY (incident_id)
        REFERENCES public.incidents (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_incident_evidences_incident_id ON public.incident_evidences(incident_id);

-- 3. Bảng lịch sử cập nhật / tiến độ xử lý sự cố (UC_16.3, UC_16.4, UC_16.5)
CREATE TABLE IF NOT EXISTS public.incident_updates
(
    id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    incident_id integer NOT NULL,
    updated_by_id integer NOT NULL,
    updated_by_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    action_type character varying(50) COLLATE pg_catalog."default" NOT NULL,
    note text COLLATE pg_catalog."default" NOT NULL,
    previous_status character varying(30) COLLATE pg_catalog."default",
    new_status character varying(30) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT incident_updates_pkey PRIMARY KEY (id),
    CONSTRAINT fk_incident_updates_incident FOREIGN KEY (incident_id)
        REFERENCES public.incidents (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_incident_updates_incident_id ON public.incident_updates(incident_id);

COMMIT;
