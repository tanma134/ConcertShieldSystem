-- =============================================================================
-- CONCERTSHIELD SYSTEM - CONSOLIDATED DATABASE CHANGES & MIGRATIONS
-- File: all_db_migrations_and_additions.sql
-- Hướng dẫn: Script được viết dưới dạng an toàn (IF NOT EXISTS), có thể chạy 
-- từng phần tương ứng vào từng Database trong PostgreSQL.
-- =============================================================================


-- =============================================================================
-- PHẦN 1: ÁP DỤNG CHO DATABASE [ authentication_db ]
-- =============================================================================
\connect authentication_db;

-- 1.1. Bảng quản lý nhân viên trực thuộc từng Organizer (Staff Management)
CREATE TABLE IF NOT EXISTS public.organizer_staff_members (
    organizer_id integer NOT NULL,
    staff_user_id integer NOT NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT pk_organizer_staff_members PRIMARY KEY (organizer_id, staff_user_id)
);
CREATE INDEX IF NOT EXISTS ix_organizer_staff_organizer ON public.organizer_staff_members(organizer_id);
CREATE INDEX IF NOT EXISTS ix_organizer_staff_staff ON public.organizer_staff_members(staff_user_id);

-- 1.2. Thêm trường lưu public_id ảnh đại diện (Cloudinary) trong bảng users
ALTER TABLE public.users ADD COLUMN IF NOT EXISTS avatar_public_id text;

-- 1.3. Bổ sung các trường thông tin doanh nghiệp & thẩm định trong organizer_requests
ALTER TABLE public.organizer_requests ADD COLUMN IF NOT EXISTS tax_code varchar(50);
ALTER TABLE public.organizer_requests ADD COLUMN IF NOT EXISTS representative_name varchar(200);
ALTER TABLE public.organizer_requests ADD COLUMN IF NOT EXISTS review_note varchar(500);
ALTER TABLE public.organizer_requests ADD COLUMN IF NOT EXISTS rejection_reason varchar(500);

-- 1.4. Bảng lưu trữ giấy tờ, hồ sơ pháp lý nộp kèm (Become Organizer Documents)
CREATE TABLE IF NOT EXISTS public.organizer_request_documents (
    document_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    request_id integer NOT NULL REFERENCES public.organizer_requests(request_id) ON DELETE CASCADE,
    document_type varchar(50) NOT NULL,
    file_name varchar(255) NOT NULL,
    file_url text NOT NULL,
    preview_url text,
    public_id varchar(255),
    is_image boolean NOT NULL DEFAULT true,
    uploaded_at timestamp with time zone NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_organizer_request_documents_request_id ON public.organizer_request_documents(request_id);


-- =============================================================================
-- PHẦN 2: ÁP DỤNG CHO DATABASE [ event_db ]
-- =============================================================================
\connect event_db;

-- 2.1. Bảng quản lý cổng sự kiện & phân quyền loại vé (Event Gates & Admission Policies)
CREATE TABLE IF NOT EXISTS public.event_gates (
    gate_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id integer NOT NULL REFERENCES public.events(event_id) ON DELETE CASCADE,
    gate_name varchar(100) NOT NULL,
    description varchar(255) NULL,
    allowed_ticket_types text NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_event_gates_event_id ON public.event_gates(event_id);

-- 2.2. Bổ sung ca trực, ngày trực & quyền soát vé cho nhân viên trong event_staff
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS shift_date date NULL;
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS shift_start_time varchar(50) NULL;
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS shift_end_time varchar(50) NULL;
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS can_review_returns boolean NOT NULL DEFAULT false;

CREATE INDEX IF NOT EXISTS ix_event_staff_shifts ON public.event_staff(event_id, staff_user_id, shift_date);


-- =============================================================================
-- PHẦN 3: ÁP DỤNG CHO DATABASE [ ticket_db ]
-- =============================================================================
\connect ticket_db;

-- 3.1. Bảng cổng soát vé đồng bộ cho dịch vụ Ticket & Checkin
CREATE TABLE IF NOT EXISTS public.event_gates (
    gate_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id integer NOT NULL,
    gate_name varchar(100) NOT NULL,
    location varchar(255) NULL,
    created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_ticket_event_gates_event_id ON public.event_gates(event_id);

-- 3.2. Bổ sung thông tin cổng, ra vào (Re-entry), trạng thái bên trong hội trường cho vé (tickets)
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS gate_id varchar(50) NULL;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS checked_in_gate varchar(100) NULL;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS is_inside boolean NOT NULL DEFAULT false;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS last_exit_at timestamp with time zone NULL;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS re_entry_count integer NOT NULL DEFAULT 0;

-- 3.3. Bảng xử lý xung đột check-in khi quét ngoại tuyến (Offline Sync Conflicts)
CREATE TABLE IF NOT EXISTS public.offline_sync_conflicts (
    conflict_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ticket_id integer NOT NULL,
    event_id integer NOT NULL,
    device_id varchar(100) NOT NULL,
    scanned_at timestamp with time zone NOT NULL,
    server_checked_in_at timestamp with time zone NULL,
    conflict_type varchar(50) NOT NULL,
    status varchar(50) NOT NULL DEFAULT 'PENDING',
    resolution varchar(50) NULL,
    resolved_by integer NULL,
    resolved_at timestamp with time zone NULL,
    details text NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_offline_sync_conflicts_event_id ON public.offline_sync_conflicts(event_id, status);

-- 3.4. Bảng đối soát vé sự kiện (Ticket Reconciliations)
CREATE TABLE IF NOT EXISTS public.ticket_reconciliations (
    reconciliation_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id integer NOT NULL,
    run_by integer NOT NULL,
    run_at timestamp with time zone NOT NULL DEFAULT now(),
    status varchar(50) NOT NULL,
    total_system_tickets integer NOT NULL,
    total_gate_scans integer NOT NULL,
    matched_count integer NOT NULL,
    unmatched_count integer NOT NULL,
    discrepancy_details text NULL,
    resolution_notes text NULL
);
CREATE INDEX IF NOT EXISTS ix_ticket_reconciliations_event ON public.ticket_reconciliations(event_id);

-- 3.5. Bảng thu hồi vé bất thường / huỷ vé (Ticket Revocations)
CREATE TABLE IF NOT EXISTS public.ticket_revocations (
    revocation_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ticket_code varchar(100) NOT NULL,
    event_id integer NOT NULL,
    event_title varchar(255) NOT NULL,
    revoked_by_admin_id integer NOT NULL,
    revoked_by_name varchar(255) NOT NULL,
    revoked_at timestamp with time zone NOT NULL DEFAULT now(),
    reason_type varchar(50) NOT NULL,
    reason_note text NULL,
    reconciliation_id integer NULL,
    is_bulk boolean NOT NULL DEFAULT false,
    refund_queued boolean NOT NULL DEFAULT true,
    notification_sent boolean NOT NULL DEFAULT true,
    seat_or_zone varchar(100) NULL,
    customer_name varchar(255) NULL
);
CREATE INDEX IF NOT EXISTS ix_ticket_revocations_event ON public.ticket_revocations(event_id);

-- 3.6. [KHÔNG BẮT BUỘC / TÙY CHỌN]: Bảng cấp lại vé (Ticket Reissues)
-- Lưu ý: Hệ thống hiện đã chuyển sang đọc trực tiếp từ bảng status_histories + tickets + orders,
-- nên bảng ticket_reissues này KHÔNG BẮT BUỘC phải tạo trong database.
-- CREATE TABLE IF NOT EXISTS public.ticket_reissues ( ... );

-- 3.7. Bảng ghi nhận thay đổi lịch biểu sự kiện & vé bị ảnh hưởng (Reschedule / Postpone Governance)
CREATE TABLE IF NOT EXISTS public.applied_event_changes (
    change_id bigint PRIMARY KEY,
    event_id integer NOT NULL,
    type text NOT NULL CHECK(type IN ('Postpone','Reschedule')),
    starts_at timestamp with time zone NULL,
    ends_at timestamp with time zone NULL,
    schedule_version integer NOT NULL,
    reason text NOT NULL,
    title text NOT NULL,
    slug text NOT NULL,
    applied_at timestamp with time zone NOT NULL,
    UNIQUE(event_id, schedule_version)
);

CREATE TABLE IF NOT EXISTS public.affected_tickets (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    change_id bigint NOT NULL REFERENCES public.applied_event_changes(change_id),
    ticket_id integer NOT NULL REFERENCES public.tickets(ticket_id),
    order_id integer NOT NULL REFERENCES public.orders(order_id),
    customer_id integer NOT NULL,
    paid_amount bigint NOT NULL CHECK(paid_amount >= 0),
    notified_at timestamp with time zone NULL,
    UNIQUE(change_id, ticket_id)
);
CREATE INDEX IF NOT EXISTS ix_affected_notification_pending ON public.affected_tickets(id) WHERE notified_at IS NULL;


-- =============================================================================
-- PHẦN 4: ÁP DỤNG CHO DATABASE [ notification_db ]
-- =============================================================================
\connect notification_db;

-- 4.1. Bổ sung các cột điều hướng, thời gian đọc và phân loại thông báo
ALTER TABLE public.notifications ADD COLUMN IF NOT EXISTS target_url varchar(255) NULL;
ALTER TABLE public.notifications ADD COLUMN IF NOT EXISTS read_at timestamp with time zone NULL;
ALTER TABLE public.notifications ADD COLUMN IF NOT EXISTS message text NULL;
ALTER TABLE public.notifications ADD COLUMN IF NOT EXISTS category varchar(50) NULL;

CREATE INDEX IF NOT EXISTS ix_notifications_user_read ON public.notifications(user_id, is_read, is_deleted);
