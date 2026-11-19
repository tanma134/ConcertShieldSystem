-- =============================================================================
-- DATABASE: authentication_db
-- System: PostgreSQL 13+
-- Description: Init script for AuthenticationAPI database schema & seed data
-- An toàn khi chạy lại hoặc tạo mới (DROP CASCADE rồi CREATE).
-- =============================================================================

-- Drop existing tables (reverse dependency order)
DROP TABLE IF EXISTS kyc_access_logs CASCADE;
DROP TABLE IF EXISTS kyc_deletion_requests CASCADE;
DROP TABLE IF EXISTS kyc_settings CASCADE;
DROP TABLE IF EXISTS kyc_consent_versions CASCADE;
DROP TABLE IF EXISTS ekyc_verifications CASCADE;
DROP TABLE IF EXISTS organizer_requests CASCADE;
DROP TABLE IF EXISTS refresh_tokens CASCADE;
DROP TABLE IF EXISTS user_roles CASCADE;
DROP TABLE IF EXISTS roles CASCADE;
DROP TABLE IF EXISTS users CASCADE;
DROP TABLE IF EXISTS pending_registrations CASCADE;

-- 1. Roles table
CREATE TABLE roles (
    role_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    role_name VARCHAR(50) NOT NULL UNIQUE
);

-- 2. Users table
CREATE TABLE users (
    user_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    full_name VARCHAR(100),
    email VARCHAR(256) NOT NULL UNIQUE,
    phone_number VARCHAR(10),
    password_hash TEXT,
    is_verified BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    auth_provider VARCHAR(20) NOT NULL DEFAULT 'local',
    has_password BOOLEAN NOT NULL DEFAULT TRUE,
    avatar_url TEXT,
    avatar_public_id TEXT,
    ekyc_status VARCHAR(20) NOT NULL DEFAULT 'NotStarted',
    otp_hash VARCHAR(256),
    otp_expired_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 3. User Roles mapping table
CREATE TABLE user_roles (
    user_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    role_id INT NOT NULL REFERENCES roles(role_id) ON DELETE CASCADE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (user_id, role_id)
);

-- 4. Refresh Tokens table
CREATE TABLE refresh_tokens (
    refresh_token_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    token_hash VARCHAR(64) NOT NULL UNIQUE,
    expires_at TIMESTAMPTZ NOT NULL,
    is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 5. Organizer Requests table
CREATE TABLE organizer_requests (
    request_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(user_id),
    company_name VARCHAR(200),
    phone_number VARCHAR(20),
    website VARCHAR(200),
    experience VARCHAR(2000),
    reason VARCHAR(1000) NOT NULL DEFAULT '',
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    rejection_reason VARCHAR(1000),
    review_note VARCHAR(1000),
    reviewed_by INT REFERENCES users(user_id),
    reviewed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ,
    deleted_by INT REFERENCES users(user_id)
);

-- 6. eKYC Verifications table
CREATE TABLE ekyc_verifications (
    ekyc_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(user_id),
    cccd_number_encrypted TEXT,
    cccd_number_hash VARCHAR(64),
    cccd_front_object_key VARCHAR(500),
    cccd_back_object_key VARCHAR(500),
    face_capture_object_key VARCHAR(500),
    face_vector BYTEA,
    ocr_raw_data JSONB,
    face_match_score NUMERIC(5,2),
    liveness_score NUMERIC(5,2),
    liveness_passed BOOLEAN,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    fail_reason VARCHAR(500),
    consent_version VARCHAR(20),
    consented_at TIMESTAMPTZ,
    data_purged_at TIMESTAMPTZ,
    legal_hold BOOLEAN NOT NULL DEFAULT FALSE,
    legal_hold_reason VARCHAR(500),
    legal_hold_at TIMESTAMPTZ,
    reviewed_by INT,
    reviewed_at TIMESTAMPTZ,
    verified_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_ekyc_cccd_number_hash ON ekyc_verifications(cccd_number_hash);
CREATE INDEX IF NOT EXISTS ix_ekyc_user_status ON ekyc_verifications(user_id, status);
CREATE INDEX IF NOT EXISTS ix_ekyc_verifications_purge ON ekyc_verifications(created_at) WHERE (data_purged_at IS NULL);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ekyc_cccd_hash_active ON ekyc_verifications(cccd_number_hash) 
    WHERE (cccd_number_hash IS NOT NULL AND status IN ('Passed', 'ManualReview'));
CREATE UNIQUE INDEX IF NOT EXISTS ux_ekyc_cccd_hash_passed ON ekyc_verifications(cccd_number_hash) 
    WHERE (status = 'Passed');

-- 7. Kyc Consent Versions table
CREATE TABLE kyc_consent_versions (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    version VARCHAR(20) NOT NULL UNIQUE,
    title VARCHAR(500) NOT NULL,
    content_json TEXT NOT NULL,
    checkbox_text TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by INT
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_kyc_consent_versions_one_active ON kyc_consent_versions(is_active) WHERE is_active = true;

-- 8. Kyc Settings table
CREATE TABLE kyc_settings (
    id INT PRIMARY KEY,
    retention_days INT NOT NULL DEFAULT 30,
    keep_document_hash_after_deletion BOOLEAN NOT NULL DEFAULT FALSE,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by INT
);

-- 9. Kyc Deletion Requests table
CREATE TABLE kyc_deletion_requests (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(user_id),
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    requested_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processed_at TIMESTAMPTZ,
    processed_by INT,
    note TEXT,
    reason_code VARCHAR(30)
);

CREATE INDEX IF NOT EXISTS ix_kyc_deletion_requests_status_requested ON kyc_deletion_requests(status, requested_at DESC);
CREATE INDEX IF NOT EXISTS ix_kyc_deletion_requests_user_status ON kyc_deletion_requests(user_id, status);
CREATE UNIQUE INDEX IF NOT EXISTS ux_kyc_deletion_one_pending_per_user ON kyc_deletion_requests(user_id) WHERE (status = 'Pending');

-- 10. Kyc Access Logs table
CREATE TABLE kyc_access_logs (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    subject_user_id INT NOT NULL REFERENCES users(user_id),
    ekyc_id INT REFERENCES ekyc_verifications(ekyc_id),
    actor_user_id INT REFERENCES users(user_id),
    actor_type VARCHAR(50) NOT NULL,
    action VARCHAR(50) NOT NULL,
    ip_address VARCHAR(50),
    details TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_kyc_access_logs_actor_created ON kyc_access_logs(actor_user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_kyc_access_logs_created ON kyc_access_logs(created_at DESC);
CREATE INDEX IF NOT EXISTS ix_kyc_access_logs_ekyc ON kyc_access_logs(ekyc_id);
CREATE INDEX IF NOT EXISTS ix_kyc_access_logs_subject_created ON kyc_access_logs(subject_user_id, created_at DESC);

-- 11. Pending Registrations table
CREATE TABLE pending_registrations (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    full_name VARCHAR(100),
    email VARCHAR(256) NOT NULL,
    phone_number VARCHAR(20),
    password_hash TEXT NOT NULL,
    otp_code VARCHAR(10) NOT NULL,
    expired_at TIMESTAMPTZ NOT NULL
);

-- =============================================================================
-- SEED DATA FOR AUTHENTICATION_DB
-- =============================================================================

-- Seed Roles
INSERT INTO roles (role_name) VALUES ('Admin'), ('Customer'), ('Organizer');

-- Seed Users (Passwords: Admin@123 / Customer@123 / Organizer@123)
-- 1. Admin: admin@ticketbox.com / Admin@123
INSERT INTO users (full_name, email, phone_number, password_hash, is_verified, is_active, auth_provider, has_password, ekyc_status)
VALUES ('System Administrator', 'admin@ticketbox.com', '0123456789', '$2a$11$TbO.cYJDLYCbmUEj9htvCemDN0vkRgD7lPGnj2Tb8.1Xpyu9YS67m', true, true, 'local', true, 'NotSubmitted');

-- 2. Customer: customer@ticketbox.com / Customer@123
INSERT INTO users (full_name, email, phone_number, password_hash, is_verified, is_active, auth_provider, has_password, ekyc_status)
VALUES ('Test Customer', 'customer@ticketbox.com', '0987654321', '$2a$11$H4HcBDSBWC6uPn1WXyMm0.EqGbPaKbRLxzs7I/lgLohWmW77ekYeS', true, true, 'local', true, 'NotSubmitted');

-- 3. Organizer: organizer@ticketbox.com / Organizer@123
INSERT INTO users (full_name, email, phone_number, password_hash, is_verified, is_active, auth_provider, has_password, ekyc_status)
VALUES ('Test Organizer', 'organizer@ticketbox.com', '0912345678', '$2a$11$7SKcRyOK56NKx/gH4B8RWevtpMW9FfPGaWQCnVZqmrc7a8CfgcHAu', true, true, 'local', true, 'NotSubmitted');

-- Assign Roles
-- Admin user (user_id 1) -> Admin
INSERT INTO user_roles (user_id, role_id) VALUES (1, (SELECT role_id FROM roles WHERE role_name = 'Admin'));
-- Customer user (user_id 2) -> Customer
INSERT INTO user_roles (user_id, role_id) VALUES (2, (SELECT role_id FROM roles WHERE role_name = 'Customer'));
-- Organizer user (user_id 3) -> Customer + Organizer
INSERT INTO user_roles (user_id, role_id) VALUES (3, (SELECT role_id FROM roles WHERE role_name = 'Customer'));
INSERT INTO user_roles (user_id, role_id) VALUES (3, (SELECT role_id FROM roles WHERE role_name = 'Organizer'));

-- Seed Default KYC Settings
INSERT INTO kyc_settings (id, retention_days, keep_document_hash_after_deletion, updated_at)
VALUES (1, 30, false, NOW()) ON CONFLICT (id) DO NOTHING;
