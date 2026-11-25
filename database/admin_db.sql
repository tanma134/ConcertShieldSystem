-- =================================================================
-- ADMIN & SUPPORT DATABASE SCHEMA (admin_db)
-- =================================================================
-- Phục vụ AdminAPI & Nghiệp vụ Hỗ trợ khách hàng / Giám sát rủi ro:
-- 1. support_cases
-- 2. support_case_notes
-- 3. audit_logs
-- 4. fraud_alerts
-- 5. risk_decision
-- 6. risk_appeal
-- 7. risk_list
-- 8. risk_notification
-- 9. system_parameters
-- An toàn khi chạy lại (CREATE TABLE IF NOT EXISTS / CREATE INDEX IF NOT EXISTS).

BEGIN;

-- ---------- 1. support_cases ----------
CREATE TABLE IF NOT EXISTS public.support_cases (
    case_id SERIAL PRIMARY KEY,
    case_number VARCHAR(32) NOT NULL UNIQUE,
    customer_id INT NULL,
    customer_name VARCHAR(150) NOT NULL,
    customer_email VARCHAR(150) NOT NULL,
    customer_phone VARCHAR(50) NULL,
    title VARCHAR(255) NOT NULL,
    description TEXT NOT NULL,
    category VARCHAR(50) NOT NULL DEFAULT 'General Inquiry',
    priority VARCHAR(20) NOT NULL DEFAULT 'Medium',
    status VARCHAR(20) NOT NULL DEFAULT 'Open',
    order_id INT NULL,
    ticket_id INT NULL,
    payment_reference VARCHAR(100) NULL,
    assigned_staff_id INT NULL,
    assigned_staff_name VARCHAR(150) NULL,
    assigned_at TIMESTAMPTZ NULL,
    assigned_by INT NULL,
    resolution TEXT NULL,
    resolved_by INT NULL,
    resolved_by_name VARCHAR(150) NULL,
    resolved_at TIMESTAMPTZ NULL,
    created_by INT NOT NULL,
    created_by_name VARCHAR(150) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_support_cases_status ON public.support_cases(status);
CREATE INDEX IF NOT EXISTS ix_support_cases_priority ON public.support_cases(priority);
CREATE INDEX IF NOT EXISTS ix_support_cases_assigned_staff ON public.support_cases(assigned_staff_id);
CREATE INDEX IF NOT EXISTS ix_support_cases_customer_email ON public.support_cases(customer_email);

-- ---------- 2. support_case_notes ----------
CREATE TABLE IF NOT EXISTS public.support_case_notes (
    note_id SERIAL PRIMARY KEY,
    case_id INT NOT NULL REFERENCES public.support_cases(case_id) ON DELETE CASCADE,
    author_id INT NOT NULL,
    author_name VARCHAR(150) NOT NULL,
    author_role VARCHAR(50) NOT NULL,
    note_type VARCHAR(50) NOT NULL DEFAULT 'InternalNote',
    content TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_support_case_notes_case_id ON public.support_case_notes(case_id);

-- ---------- 3. audit_logs ----------
CREATE TABLE IF NOT EXISTS public.audit_logs (
    audit_log_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id INT NULL,
    actor_type VARCHAR(20) NOT NULL DEFAULT 'user',
    action VARCHAR(100) NOT NULL,
    entity_type VARCHAR(50) NOT NULL,
    entity_id VARCHAR(50) NULL,
    old_value JSONB NULL,
    new_value JSONB NULL,
    ip_address INET NULL,
    request_id VARCHAR(100) NULL,
    user_agent VARCHAR(500) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_audit_logs_entity ON public.audit_logs(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS ix_audit_logs_user_id ON public.audit_logs(user_id);

-- ---------- 4. fraud_alerts ----------
CREATE TABLE IF NOT EXISTS public.fraud_alerts (
    fraud_alert_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id INT NULL,
    order_id INT NULL,
    ticket_id INT NULL,
    event_id INT NULL,
    alert_type VARCHAR(30) NOT NULL,
    risk_score NUMERIC(5,2) NOT NULL,
    details TEXT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Open',
    reviewed_by INT NULL,
    reviewed_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    bot_score NUMERIC(5,2) NOT NULL DEFAULT 0,
    fraud_score NUMERIC(5,2) NOT NULL DEFAULT 0,
    risk_level VARCHAR(10) NOT NULL DEFAULT 'Low',
    session_id VARCHAR(100) NULL,
    ai_summary JSONB NULL,
    ai_summary_at TIMESTAMPTZ NULL,
    assigned_to INT NULL,
    assigned_at TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ix_fraud_alerts_created_at ON public.fraud_alerts(created_at DESC);
CREATE INDEX IF NOT EXISTS ix_fraud_alerts_event_id ON public.fraud_alerts(event_id);
CREATE INDEX IF NOT EXISTS ix_fraud_alerts_level_status ON public.fraud_alerts(risk_level, status);
CREATE INDEX IF NOT EXISTS ix_fraud_alerts_order_id ON public.fraud_alerts(order_id);
CREATE INDEX IF NOT EXISTS ix_fraud_alerts_ticket_id ON public.fraud_alerts(ticket_id);
CREATE INDEX IF NOT EXISTS ix_fraud_alerts_user_id ON public.fraud_alerts(user_id);
CREATE INDEX IF NOT EXISTS ix_fraud_alerts_assigned_to ON public.fraud_alerts(assigned_to) WHERE (assigned_to IS NOT NULL);

-- ---------- 5. risk_decision ----------
CREATE TABLE IF NOT EXISTS public.risk_decision (
    risk_decision_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    decision_code VARCHAR(30) NOT NULL UNIQUE,
    fraud_alert_id INT NULL REFERENCES public.fraud_alerts(fraud_alert_id) ON DELETE SET NULL,
    user_id INT NULL,
    order_id INT NULL,
    ticket_id INT NULL,
    session_id VARCHAR(100) NULL,
    device_fp VARCHAR(128) NULL,
    ip_address INET NULL,
    bot_score NUMERIC(5,2) NOT NULL DEFAULT 0,
    fraud_score NUMERIC(5,2) NOT NULL DEFAULT 0,
    risk_level VARCHAR(10) NOT NULL,
    dominant_type VARCHAR(10) NULL,
    score_by_group JSONB NULL,
    triggered_rules JSONB NULL,
    trust_modifiers JSONB NULL,
    hard_rule VARCHAR(20) NULL,
    signals_missing JSONB NULL,
    rule_version VARCHAR(20) NOT NULL,
    action VARCHAR(20) NOT NULL,
    scope VARCHAR(20) NULL,
    reason_public VARCHAR(300) NULL,
    escalation_level SMALLINT NOT NULL DEFAULT 1,
    is_shadow BOOLEAN NOT NULL DEFAULT FALSE,
    status VARCHAR(15) NOT NULL DEFAULT 'Active',
    expires_at TIMESTAMPTZ NULL,
    decided_by_type VARCHAR(10) NOT NULL DEFAULT 'SYSTEM',
    decided_by INT NULL,
    manual_reason VARCHAR(500) NULL,
    restored_by INT NULL,
    restored_at TIMESTAMPTZ NULL,
    restore_reason VARCHAR(500) NULL,
    ai_summary JSONB NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason_code VARCHAR(60) NULL,
    notified_at TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ix_risk_decision_alert_id ON public.risk_decision(fraud_alert_id);
CREATE INDEX IF NOT EXISTS ix_risk_decision_created_at ON public.risk_decision(created_at DESC);
CREATE INDEX IF NOT EXISTS ix_risk_decision_device_fp ON public.risk_decision(device_fp);
CREATE INDEX IF NOT EXISTS ix_risk_decision_ip ON public.risk_decision(ip_address);
CREATE INDEX IF NOT EXISTS ix_risk_decision_user_id ON public.risk_decision(user_id);
CREATE INDEX IF NOT EXISTS ix_risk_decision_active ON public.risk_decision(status, expires_at) WHERE (status = 'Active');

-- ---------- 6. risk_appeal ----------
CREATE TABLE IF NOT EXISTS public.risk_appeal (
    risk_appeal_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    risk_decision_id INT NOT NULL UNIQUE REFERENCES public.risk_decision(risk_decision_id) ON DELETE CASCADE,
    user_id INT NOT NULL,
    reason VARCHAR(2000) NOT NULL,
    evidence JSONB NULL,
    status VARCHAR(15) NOT NULL DEFAULT 'Pending',
    sla_due_at TIMESTAMPTZ NOT NULL,
    reviewed_by INT NULL,
    reviewed_at TIMESTAMPTZ NULL,
    review_note VARCHAR(1000) NULL,
    ai_summary JSONB NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_risk_appeal_user_id ON public.risk_appeal(user_id);
CREATE INDEX IF NOT EXISTS ix_risk_appeal_pending ON public.risk_appeal(sla_due_at) WHERE (status = 'Pending');

-- ---------- 7. risk_list ----------
CREATE TABLE IF NOT EXISTS public.risk_list (
    risk_list_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    list_type VARCHAR(10) NOT NULL,
    value_type VARCHAR(15) NOT NULL,
    value_hash VARCHAR(128) NOT NULL,
    reason VARCHAR(500) NOT NULL,
    expires_at TIMESTAMPTZ NULL,
    created_by INT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_risk_list_entry UNIQUE (list_type, value_type, value_hash)
);

CREATE INDEX IF NOT EXISTS ix_risk_list_lookup ON public.risk_list(value_type, value_hash);

-- ---------- 8. risk_notification ----------
CREATE TABLE IF NOT EXISTS public.risk_notification (
    risk_notification_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id INT NOT NULL,
    risk_decision_id INT NULL REFERENCES public.risk_decision(risk_decision_id) ON DELETE SET NULL,
    risk_appeal_id INT NULL,
    type VARCHAR(30) NOT NULL,
    scope VARCHAR(20) NULL,
    title VARCHAR(200) NOT NULL,
    message VARCHAR(1000) NOT NULL,
    can_appeal BOOLEAN NOT NULL DEFAULT FALSE,
    appeal_deadline_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    delivered_at TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ix_risk_notification_user ON public.risk_notification(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_risk_notification_pending ON public.risk_notification(created_at) WHERE (delivered_at IS NULL);

-- ---------- 9. system_parameters ----------
CREATE TABLE IF NOT EXISTS public.system_parameters (
    system_parameter_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    param_key VARCHAR(100) NOT NULL UNIQUE,
    param_value VARCHAR(500) NOT NULL,
    category VARCHAR(50) NOT NULL,
    description VARCHAR(500) NULL,
    updated_by INT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_system_parameters_category ON public.system_parameters(category);

COMMIT;
