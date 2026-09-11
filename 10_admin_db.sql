-- ============================================================
-- admin_db  (AdminAPI)
-- Tables: audit_logs, fraud_alerts, system_parameters
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.audit_logs
(
    audit_log_id bigint NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 9223372036854775807 CACHE 1 ),
    user_id integer,                                       -- soft ref -> identity_db.users
    actor_type character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'user'::character varying,
    action character varying(100) COLLATE pg_catalog."default" NOT NULL,
    entity_type character varying(50) COLLATE pg_catalog."default" NOT NULL,
    entity_id character varying(50) COLLATE pg_catalog."default",   -- đa hình theo entity_type
    old_value jsonb,
    new_value jsonb,
    ip_address inet,
    request_id character varying(100) COLLATE pg_catalog."default",
    user_agent character varying(500) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT audit_logs_pkey PRIMARY KEY (audit_log_id)
);
CREATE INDEX IF NOT EXISTS ix_audit_logs_user_id ON public.audit_logs(user_id);
CREATE INDEX IF NOT EXISTS ix_audit_logs_entity ON public.audit_logs(entity_type, entity_id);

CREATE TABLE IF NOT EXISTS public.fraud_alerts
(
    fraud_alert_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    user_id integer,                                        -- soft ref -> identity_db.users
    order_id integer,                                         -- soft ref -> ticket_db.orders
    ticket_id integer,                                          -- soft ref -> ticket_db.tickets
    event_id integer,                                            -- soft ref -> event_db.events
    alert_type character varying(30) COLLATE pg_catalog."default" NOT NULL,
    risk_score numeric(5, 2) NOT NULL,
    details text COLLATE pg_catalog."default",
    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Open'::character varying,
    reviewed_by integer,                                          -- soft ref -> identity_db.users
    reviewed_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT fraud_alerts_pkey PRIMARY KEY (fraud_alert_id)
);
CREATE INDEX IF NOT EXISTS ix_fraud_alerts_user_id ON public.fraud_alerts(user_id);
CREATE INDEX IF NOT EXISTS ix_fraud_alerts_order_id ON public.fraud_alerts(order_id);
CREATE INDEX IF NOT EXISTS ix_fraud_alerts_ticket_id ON public.fraud_alerts(ticket_id);
CREATE INDEX IF NOT EXISTS ix_fraud_alerts_event_id ON public.fraud_alerts(event_id);

CREATE TABLE IF NOT EXISTS public.system_parameters
(
    system_parameter_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    param_key character varying(100) COLLATE pg_catalog."default" NOT NULL,
    param_value character varying(500) COLLATE pg_catalog."default" NOT NULL,
    category character varying(50) COLLATE pg_catalog."default" NOT NULL,
    description character varying(500) COLLATE pg_catalog."default",
    updated_by integer NOT NULL,                                    -- soft ref -> identity_db.users
    updated_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT system_parameters_pkey PRIMARY KEY (system_parameter_id),
    CONSTRAINT system_parameters_param_key_key UNIQUE (param_key)
);

-- ===== FK cùng DB =====
-- Không có bảng nào trong admin_db tham chiếu tới bảng khác cùng DB.

-- ===== Soft reference (KHÔNG có FK) =====
-- audit_logs.user_id -> identity_db.users
-- fraud_alerts.user_id/reviewed_by -> identity_db.users
-- fraud_alerts.order_id -> ticket_db.orders
-- fraud_alerts.ticket_id -> ticket_db.tickets
-- fraud_alerts.event_id -> event_db.events
-- system_parameters.updated_by -> identity_db.users

-- Ghi chú: audit_logs.entity_id đã là character varying (đa hình
-- sẵn theo entity_type) -- phù hợp tự nhiên với soft reference,
-- không cần sửa kiểu dữ liệu khi tách DB.

END;
