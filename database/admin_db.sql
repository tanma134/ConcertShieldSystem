-- Admin & Support Database Schema
CREATE TABLE IF NOT EXISTS support_cases (
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

CREATE TABLE IF NOT EXISTS support_case_notes (
    note_id SERIAL PRIMARY KEY,
    case_id INT NOT NULL REFERENCES support_cases(case_id) ON DELETE CASCADE,
    author_id INT NOT NULL,
    author_name VARCHAR(150) NOT NULL,
    author_role VARCHAR(50) NOT NULL,
    note_type VARCHAR(50) NOT NULL DEFAULT 'InternalNote',
    content TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_support_cases_status ON support_cases(status);
CREATE INDEX IF NOT EXISTS ix_support_cases_priority ON support_cases(priority);
CREATE INDEX IF NOT EXISTS ix_support_cases_assigned_staff ON support_cases(assigned_staff_id);
CREATE INDEX IF NOT EXISTS ix_support_cases_customer_email ON support_cases(customer_email);
CREATE INDEX IF NOT EXISTS ix_support_case_notes_case_id ON support_case_notes(case_id);
