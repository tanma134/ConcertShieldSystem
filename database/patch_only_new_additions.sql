-- =============================================================================
-- CONCERTSHIELD SYSTEM - BẢN CẬP NHẬT DATABASE RÚT GỌN (CHỈ GỒM BẢNG VÀ CỘT MỚI)
-- File: patch_only_new_additions.sql
-- Mô tả: File SQL này CHỈ chứa những bảng và cột THỰC SỰ MỚI được bổ sung trong
--       quá trình phát triển các tính năng gần đây. Không bao gồm các bảng cũ.
-- =============================================================================

-- =============================================================================
-- PHẦN 1: KẾT NỐI VÀO DATABASE "authentication_db"
-- =============================================================================
\c authentication_db;

-- 1.1. Bổ sung các cột hồ sơ doanh nghiệp & lý do duyệt vào bảng organizer_requests
ALTER TABLE public.organizer_requests
    ADD COLUMN IF NOT EXISTS tax_code VARCHAR(50) NULL,
    ADD COLUMN IF NOT EXISTS representative_name VARCHAR(200) NULL,
    ADD COLUMN IF NOT EXISTS review_note VARCHAR(1000) NULL,
    ADD COLUMN IF NOT EXISTS rejection_reason VARCHAR(500) NULL;

-- 1.2. Tạo bảng lưu tài liệu / giấy phép kinh doanh đính kèm khi yêu cầu Become Organizer
CREATE TABLE IF NOT EXISTS public.organizer_request_documents (
    id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    request_id INT NOT NULL REFERENCES public.organizer_requests(request_id) ON DELETE CASCADE,
    document_type VARCHAR(50) NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    file_url TEXT NOT NULL,
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_org_req_docs_request_id ON public.organizer_request_documents(request_id);

-- 1.3. Tạo bảng cô lập nhân viên (Staff) riêng theo từng Organizer
CREATE TABLE IF NOT EXISTS public.organizer_staff_members (
    organizer_id INT NOT NULL,
    staff_user_id INT NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'Active',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by INT NULL,
    CONSTRAINT pk_organizer_staff_members PRIMARY KEY (organizer_id, staff_user_id)
);
CREATE INDEX IF NOT EXISTS ix_organizer_staff_organizer ON public.organizer_staff_members(organizer_id);
CREATE INDEX IF NOT EXISTS ix_organizer_staff_staff ON public.organizer_staff_members(staff_user_id);


-- =============================================================================
-- PHẦN 2: KẾT NỐI VÀO DATABASE "event_db"
-- =============================================================================
\c event_db;

-- 2.1. Tạo bảng danh mục cổng soát vé sự kiện (Event Gates)
CREATE TABLE IF NOT EXISTS public.event_gates (
    gate_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id INT NOT NULL REFERENCES public.events(event_id) ON DELETE CASCADE,
    gate_name VARCHAR(100) NOT NULL,
    description VARCHAR(255) NULL,
    allowed_ticket_types TEXT NULL,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_event_gates_event_id ON public.event_gates(event_id);

-- 2.2. Bổ sung các cột phân ca trực & phân quyền 4 vai trò cho nhân viên trong event_staff
ALTER TABLE public.event_staff
    ADD COLUMN IF NOT EXISTS shift_date DATE NULL,
    ADD COLUMN IF NOT EXISTS shift_start_time TIME WITHOUT TIME ZONE NULL,
    ADD COLUMN IF NOT EXISTS shift_end_time TIME WITHOUT TIME ZONE NULL,
    ADD COLUMN IF NOT EXISTS assigned_by INT NULL,
    ADD COLUMN IF NOT EXISTS can_manage_incidents BOOLEAN DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS can_manage_support_cases BOOLEAN DEFAULT FALSE;

CREATE INDEX IF NOT EXISTS ix_event_staff_shift_schedule 
    ON public.event_staff(event_id, shift_date, shift_start_time, shift_end_time);


-- =============================================================================
-- PHẦN 3: KẾT NỐI VÀO DATABASE "ticket_db"
-- =============================================================================
\c ticket_db;

-- 3.1. Bổ sung cột gate_id vào bảng tickets (Lưu cổng soát vé thực tế)
ALTER TABLE public.tickets
    ADD COLUMN IF NOT EXISTS gate_id INT NULL;

-- 3.2. Bảng đồng bộ cổng sự kiện bên ticket_db phục vụ kiểm tra loại vé khi quét QR
CREATE TABLE IF NOT EXISTS public.event_gates (
    gate_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id INT NOT NULL,
    gate_name VARCHAR(100) NOT NULL,
    description VARCHAR(255) NULL,
    allowed_ticket_types TEXT NULL,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_ticket_event_gates_event ON public.event_gates(event_id);

-- 3.3. Bảng xử lý xung đột soát vé Offline (Offline Sync Conflicts)
CREATE TABLE IF NOT EXISTS public.offline_sync_conflicts (
    conflict_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ticket_id INT NOT NULL,
    event_id INT NOT NULL,
    ticket_code VARCHAR(100) NOT NULL,
    device_id VARCHAR(100) NOT NULL,
    scanned_at TIMESTAMPTZ NOT NULL,
    server_checked_in_at TIMESTAMPTZ NULL,
    conflict_type VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'PENDING',
    resolution VARCHAR(50) NULL,
    resolved_by INT NULL,
    resolved_at TIMESTAMPTZ NULL,
    details TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_offline_sync_conflicts_event_id ON public.offline_sync_conflicts(event_id, status);

-- 3.4. Bảng đối soát vé tổng thể khi sự kiện bị dời lịch / hủy (Ticket Reconciliations)
CREATE TABLE IF NOT EXISTS public.ticket_reconciliations (
    reconciliation_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id INT NOT NULL,
    run_by INT NOT NULL,
    run_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    status VARCHAR(50) NOT NULL,
    total_system_tickets INT NOT NULL,
    total_gate_scans INT NOT NULL,
    matched_count INT NOT NULL,
    unmatched_count INT NOT NULL,
    discrepancy_details TEXT NULL,
    resolution_notes TEXT NULL,
    event_title VARCHAR(255) NULL,
    admin_name VARCHAR(100) NULL,
    total_tickets_revoked INT NULL,
    reason_type VARCHAR(50) NULL,
    notes TEXT NULL,
    refund_status VARCHAR(50) DEFAULT 'QUEUED',
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_ticket_reconciliations_event ON public.ticket_reconciliations(event_id);

-- 3.5. Bảng lưu vết lịch sử thu hồi vé (Ticket Revocations - cả đơn lẻ và hàng loạt)
CREATE TABLE IF NOT EXISTS public.ticket_revocations (
    revocation_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ticket_code VARCHAR(100) NOT NULL,
    event_id INT NOT NULL,
    event_title VARCHAR(255) NOT NULL,
    revoked_by_admin_id INT NOT NULL,
    revoked_by_name VARCHAR(255) NOT NULL,
    revoked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason_type VARCHAR(50) NOT NULL,
    reason_note TEXT NULL,
    reconciliation_id INT NULL,
    is_bulk BOOLEAN NOT NULL DEFAULT false,
    refund_queued BOOLEAN NOT NULL DEFAULT true,
    notification_sent BOOLEAN NOT NULL DEFAULT true,
    seat_or_zone VARCHAR(100) NULL,
    customer_name VARCHAR(255) NULL
);
CREATE INDEX IF NOT EXISTS ix_ticket_revocations_event ON public.ticket_revocations(event_id);


-- =============================================================================
-- PHẦN 4: KẾT NỐI VÀO DATABASE "notification_db"
-- =============================================================================
\c notification_db;

-- 4.1. Bổ sung các cột điều hướng và đọc thông báo vào bảng notifications
ALTER TABLE public.notifications
    ADD COLUMN IF NOT EXISTS target_url VARCHAR(500) NULL,
    ADD COLUMN IF NOT EXISTS read_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS message TEXT NULL,
    ADD COLUMN IF NOT EXISTS category VARCHAR(50) NOT NULL DEFAULT 'event_new';

CREATE INDEX IF NOT EXISTS ix_notifications_user_read 
    ON public.notifications(user_id, is_read, created_at DESC);
