\set ON_ERROR_STOP on

-- =============================================================================
-- 1. AUTHENTICATION_DB: Tạo tài khoản Customer mới & Organizer mới
-- =============================================================================
\connect authentication_db

-- 1.1. Tài khoản Customer mới: Nguyễn Minh Khôi (Password: Customer@123)
INSERT INTO users (
    full_name, email, phone_number, password_hash,
    is_verified, is_active, auth_provider, has_password, ekyc_status, created_at
) VALUES (
    'Nguyễn Minh Khôi',
    'minhkhoi.customer@ticketbox.com',
    '0908123456',
    '$2a$11$H4HcBDSBWC6uPn1WXyMm0.EqGbPaKbRLxzs7I/lgLohWmW77ekYeS',
    true, true, 'local', true, 'Verified', NOW()
)
ON CONFLICT (email) DO UPDATE SET
    full_name = EXCLUDED.full_name,
    phone_number = EXCLUDED.phone_number,
    password_hash = EXCLUDED.password_hash,
    is_verified = true,
    is_active = true,
    ekyc_status = 'Verified';

SELECT user_id AS cust_id FROM users WHERE email = 'minhkhoi.customer@ticketbox.com' \gset

-- 1.2. Tài khoản Organizer mới: Starlight Live Entertainment (Password: Organizer@123)
INSERT INTO users (
    full_name, email, phone_number, password_hash,
    is_verified, is_active, auth_provider, has_password, ekyc_status, created_at
) VALUES (
    'Starlight Live Entertainment',
    'starlight.organizer@ticketbox.com',
    '0918888999',
    '$2a$11$7SKcRyOK56NKx/gH4B8RWevtpMW9FfPGaWQCnVZqmrc7a8CfgcHAu',
    true, true, 'local', true, 'Verified', NOW()
)
ON CONFLICT (email) DO UPDATE SET
    full_name = EXCLUDED.full_name,
    phone_number = EXCLUDED.phone_number,
    password_hash = EXCLUDED.password_hash,
    is_verified = true,
    is_active = true,
    ekyc_status = 'Verified';

SELECT user_id AS org_id FROM users WHERE email = 'starlight.organizer@ticketbox.com' \gset

-- 1.3. Gán quyền (Roles)
INSERT INTO user_roles (user_id, role_id)
SELECT :cust_id, role_id FROM roles WHERE role_name = 'Customer'
ON CONFLICT DO NOTHING;

INSERT INTO user_roles (user_id, role_id)
SELECT :org_id, role_id FROM roles WHERE role_name IN ('Customer', 'Organizer')
ON CONFLICT DO NOTHING;

-- 1.4. Lưu thông tin CCCD (079201012345) & eKYC Passed cho Customer
ALTER TABLE ekyc_verifications ADD COLUMN IF NOT EXISTS cccd_number_hash VARCHAR(64);
ALTER TABLE ekyc_verifications ADD COLUMN IF NOT EXISTS consent_version VARCHAR(20);
ALTER TABLE ekyc_verifications ADD COLUMN IF NOT EXISTS consented_at TIMESTAMPTZ;
ALTER TABLE ekyc_verifications ADD COLUMN IF NOT EXISTS data_purged_at TIMESTAMPTZ;
ALTER TABLE ekyc_verifications ADD COLUMN IF NOT EXISTS legal_hold BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE ekyc_verifications ADD COLUMN IF NOT EXISTS legal_hold_reason VARCHAR(500);
ALTER TABLE ekyc_verifications ADD COLUMN IF NOT EXISTS legal_hold_at TIMESTAMPTZ;
ALTER TABLE ekyc_verifications ADD COLUMN IF NOT EXISTS reviewed_by INT;
ALTER TABLE ekyc_verifications ADD COLUMN IF NOT EXISTS reviewed_at TIMESTAMPTZ;

DELETE FROM ekyc_verifications WHERE user_id = :cust_id;
INSERT INTO ekyc_verifications (
    user_id, cccd_number_encrypted, cccd_number_hash,
    ocr_raw_data, face_match_score, liveness_score, liveness_passed,
    status, consent_version, consented_at, verified_at, created_at
) VALUES (
    :cust_id,
    '079201012345',
    'sha256_079201012345_minhkhoi',
    '{"id":"079201012345","name":"NGUYỄN MINH KHÔI","dob":"15/08/2001","sex":"Nam","nationality":"Việt Nam","address":"125 Lê Lợi, Phường Bến Thành, Quận 1, TP. Hồ Chí Minh"}'::jsonb,
    98.50, 99.10, true,
    'Passed', 'v1', NOW(), NOW(), NOW()
);

-- 1.5. Lưu hồ sơ Become Organizer đã được Admin duyệt cho Organizer
ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS tax_code VARCHAR(50);
ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS representative_name VARCHAR(200);
ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS review_note VARCHAR(1000);
ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS rejection_reason VARCHAR(1000);
ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS bank_account_holder VARCHAR(200);
ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS bank_account_number VARCHAR(100);
ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS bank_name VARCHAR(150);
ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS bank_branch VARCHAR(150);

DELETE FROM organizer_requests WHERE user_id = :org_id;
INSERT INTO organizer_requests (
    user_id, company_name, tax_code, representative_name,
    phone_number, website, experience, reason,
    bank_account_holder, bank_account_number, bank_name, bank_branch,
    status, review_note, reviewed_by, reviewed_at, created_at, updated_at, is_deleted
) VALUES (
    :org_id,
    'Starlight Live Entertainment Co., Ltd',
    '0316888999',
    'Trần Quốc Bảo',
    '0918888999',
    'https://starlightlive.vn',
    '10 years organizing stadium and arena music festivals in Vietnam.',
    'Official concert organizer account for Starlight Live Entertainment.',
    'CONG TY TNHH STARLIGHT LIVE ENTERTAINMENT',
    '0071001234567',
    'Vietcombank',
    'Chi nhánh TP. Hồ Chí Minh',
    'Approved',
    'Verified business license and tax code 0316888999.',
    1,
    NOW(),
    NOW() - INTERVAL '5 days',
    NOW() - INTERVAL '4 days',
    false
);

-- =============================================================================
-- 2. EVENT_DB: Tạo 2 Sự kiện cho Organizer (1 Sự kiện đang hoạt động, 1 Sự kiện đã gửi yêu cầu hủy & được chấp nhận)
-- =============================================================================
\connect event_db

-- 2.1. Sự kiện 1 (Chưa bị hủy - Đang mở bán / Published): STARLIGHT SYMPHONY FEST 2026 – LIVE IN SAIGON
INSERT INTO events (
    organizer_id, category_id, title, slug, short_description, description,
    poster_url, poster_public_id, banner_url, banner_public_id,
    location_name, address, city, starts_at, ends_at, timezone,
    has_seating_chart, seating_mode, requires_virtual_queue, status,
    is_featured, total_tickets, sold_tickets, min_tickets_per_account, max_tickets_per_account,
    meta_title, meta_description,
    compliance_status, compliance_version, compliance_reviewed_version, schedule_version, sales_frozen,
    submitted_at, approved_at, published_at, created_by, updated_by, is_deleted
) VALUES (
    :org_id, 1,
    'STARLIGHT SYMPHONY FEST 2026 – LIVE IN SAIGON',
    'starlight-symphony-fest-2026-saigon',
    'Đại nhạc hội Starlight Symphony Fest 2026 tại SECC Outdoor Arena.',
    'Đêm nhạc quy tụ dàn nghệ sĩ hàng đầu với hệ thống âm thanh ánh sáng chuẩn quốc tế tại SECC Outdoor Arena, Quận 7, TP. Hồ Chí Minh.',
    'https://images.unsplash.com/photo-1540039155733-5bb30b53aa14?auto=format&fit=crop&w=1200&q=80',
    'seed/starlight-symphony-2026-poster',
    'https://images.unsplash.com/photo-1470229722913-7c0e2dbbafd3?auto=format&fit=crop&w=1600&q=80',
    'seed/starlight-symphony-2026-banner',
    'SECC Outdoor Arena',
    '799 Nguyễn Văn Linh, Phường Tân Phú, Quận 7',
    'Hồ Chí Minh',
    '2026-12-26 19:00:00+07'::timestamptz,
    '2026-12-26 23:00:00+07'::timestamptz,
    'SE Asia Standard Time',
    false, 'GeneralAdmission', false, 'Published',
    true, 300, 5, 1, 10,
    'STARLIGHT SYMPHONY FEST 2026', 'Starlight Live Entertainment · 26/12/2026',
    'Approved', 1, 1, 0, false,
    NOW() - INTERVAL '3 days', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days',
    :org_id, :org_id, false
)
ON CONFLICT (slug) WHERE is_deleted = false DO UPDATE SET
    organizer_id = EXCLUDED.organizer_id,
    title = EXCLUDED.title,
    status = 'Published',
    sales_frozen = false,
    sold_tickets = 5,
    updated_at = NOW();

SELECT event_id AS event1_id FROM events WHERE slug = 'starlight-symphony-fest-2026-saigon' AND is_deleted = false \gset

-- 2.2. Sự kiện 2 (Đã gửi yêu cầu hủy và đã được Admin chấp nhận): STARLIGHT WINTER COUNTDOWN 2026
INSERT INTO events (
    organizer_id, category_id, title, slug, short_description, description,
    poster_url, poster_public_id, banner_url, banner_public_id,
    location_name, address, city, starts_at, ends_at, timezone,
    has_seating_chart, seating_mode, requires_virtual_queue, status,
    is_featured, total_tickets, sold_tickets, min_tickets_per_account, max_tickets_per_account,
    meta_title, meta_description,
    compliance_status, compliance_version, compliance_reviewed_version, schedule_version, sales_frozen,
    submitted_at, approved_at, published_at, created_by, updated_by, is_deleted
) VALUES (
    :org_id, 1,
    'STARLIGHT WINTER COUNTDOWN 2026',
    'starlight-winter-countdown-2026-cancelled',
    'Sự kiện Countdown mùa đông của Starlight Live Entertainment (Đã gửi yêu cầu hủy và được Admin chấp nhận).',
    'Sự kiện đã gửi yêu cầu hủy/dừng tổ chức lên Admin và đã được chấp thuận.',
    'https://images.unsplash.com/photo-1514525253161-7a46d19cd819?auto=format&fit=crop&w=1200&q=80',
    'seed/starlight-winter-cancelled-poster',
    'https://images.unsplash.com/photo-1514525253161-7a46d19cd819?auto=format&fit=crop&w=1600&q=80',
    'seed/starlight-winter-cancelled-banner',
    'Sân vận động Quân khu 7',
    '202 Hoàng Văn Thụ, Phường 9, Quận Phú Nhuận',
    'Hồ Chí Minh',
    '2026-12-31 20:00:00+07'::timestamptz,
    '2026-12-31 23:59:00+07'::timestamptz,
    'SE Asia Standard Time',
    false, 'GeneralAdmission', false, 'Cancelled',
    false, 200, 0, 1, 10,
    'STARLIGHT WINTER COUNTDOWN 2026', 'Cancelled Concert · Starlight Live Entertainment',
    'Approved', 1, 1, 1, true,
    NOW() - INTERVAL '4 days', NOW() - INTERVAL '3 days', NOW() - INTERVAL '3 days',
    :org_id, :org_id, false
)
ON CONFLICT (slug) WHERE is_deleted = false DO UPDATE SET
    organizer_id = EXCLUDED.organizer_id,
    title = EXCLUDED.title,
    status = 'Cancelled',
    sales_frozen = true,
    schedule_version = 1,
    updated_at = NOW();

SELECT event_id AS event2_id FROM events WHERE slug = 'starlight-winter-countdown-2026-cancelled' AND is_deleted = false \gset

-- 2.3. Hình ảnh, Chính sách hoàn tiền, Cổng Check-in và Hồ sơ pháp lý (Compliance) cho 2 sự kiện
DELETE FROM event_images WHERE event_id IN (:event1_id, :event2_id);
INSERT INTO event_images (event_id, image_url, public_id, sort_order, is_main) VALUES
(:event1_id, 'https://images.unsplash.com/photo-1540039155733-5bb30b53aa14?auto=format&fit=crop&w=1200&q=80', 'seed/starlight-symphony-2026-poster', 1, true),
(:event2_id, 'https://images.unsplash.com/photo-1514525253161-7a46d19cd819?auto=format&fit=crop&w=1200&q=80', 'seed/starlight-winter-cancelled-poster', 1, true);

DELETE FROM refund_policies WHERE event_id IN (:event1_id, :event2_id);
INSERT INTO refund_policies (event_id, policy_name, description, deadline_before_event_hours, refund_percent, requires_organizer_approval, is_active) VALUES
(:event1_id, 'Standard 72h Refund Policy', 'Refund 80% up to 72 hours before event start.', 72, 80.00, true, true),
(:event2_id, 'Full Cancellation Refund Policy', '100% refund upon event cancellation.', 24, 100.00, false, true);

DELETE FROM event_gates WHERE event_id IN (:event1_id, :event2_id);
INSERT INTO event_gates (event_id, gate_name, description, allowed_ticket_types) VALUES
(:event1_id, 'Gate A - VIP Entrance', 'Dedicated VIP Diamond admission gate', 'VIP Diamond'),
(:event1_id, 'Gate B - Standard Entrance', 'General Admission Standing gate', 'Standard GA');

DELETE FROM event_compliance_documents WHERE event_id IN (:event1_id, :event2_id);
INSERT INTO event_compliance_documents (
    event_id, version, document_type, file_name, content_type,
    public_id, secure_url, resource_type, size, submitted_by, submitted_at
) VALUES
(:event1_id, 1, 'EventPermit', 'starlight_symphony_permit.pdf', 'application/pdf', 'seed/starlight_symphony_permit', 'https://res.cloudinary.com/demo/image/upload/sample.pdf', 'raw', 245000, :org_id, NOW() - INTERVAL '3 days'),
(:event1_id, 1, 'SafetyPlan', 'starlight_symphony_safety.pdf', 'application/pdf', 'seed/starlight_symphony_safety', 'https://res.cloudinary.com/demo/image/upload/sample.pdf', 'raw', 310000, :org_id, NOW() - INTERVAL '3 days'),
(:event2_id, 1, 'EventPermit', 'starlight_winter_permit.pdf', 'application/pdf', 'seed/starlight_winter_permit', 'https://res.cloudinary.com/demo/image/upload/sample.pdf', 'raw', 240000, :org_id, NOW() - INTERVAL '4 days'),
(:event2_id, 1, 'SafetyPlan', 'starlight_winter_safety.pdf', 'application/pdf', 'seed/starlight_winter_safety', 'https://res.cloudinary.com/demo/image/upload/sample.pdf', 'raw', 295000, :org_id, NOW() - INTERVAL '4 days');

-- 2.4. Bản ghi Yêu cầu hủy/hoãn sự kiện đã được Admin chấp nhận (Approved) cho Sự kiện 2
DELETE FROM event_change_requests WHERE event_id = :event2_id;
INSERT INTO event_change_requests (
    event_id, type, status, reason,
    old_starts_at, old_ends_at, new_starts_at, new_ends_at,
    old_status, event_version, submitted_by, submitted_at,
    reviewed_by, reviewed_at, review_notes, processing_status
) VALUES (
    :event2_id,
    'Postpone',
    'Approved',
    '[Venue Structural Safety Hazard] Organizer requested event cancellation due to severe stage weather damage and safety directive.',
    '2026-12-31 20:00:00+07'::timestamptz,
    '2026-12-31 23:59:00+07'::timestamptz,
    NULL,
    NULL,
    'Published',
    0,
    :org_id,
    NOW() - INTERVAL '1 day',
    1,
    NOW() - INTERVAL '12 hours',
    'Approved organizer cancellation request. Event status updated to Cancelled and ticket sales frozen.',
    'Applied'
);

-- 2.5. Các hạng vé (Ticket Types) cho Sự kiện 1 & Sự kiện 2
DELETE FROM ticket_types WHERE event_id IN (:event1_id, :event2_id);

INSERT INTO ticket_types (
    event_id, type_name, description, price, original_price,
    quantity, sold_quantity, min_per_order, max_per_order,
    color_code, sort_order, status, sales_starts_at, sales_ends_at
) VALUES (
    :event1_id, 'VIP Diamond', 'Khu vực VIP gần sân khấu + Quà tặng độc quyền', 2500000, 2500000,
    100, 3, 1, 10, '#ec4899', 1, 'Active',
    NOW() - INTERVAL '10 days', '2026-12-26 18:00:00+07'::timestamptz
);

SELECT ticket_type_id AS tt_vip_id FROM ticket_types WHERE event_id = :event1_id AND type_name = 'VIP Diamond' AND is_deleted = false \gset

INSERT INTO ticket_types (
    event_id, type_name, description, price, original_price,
    quantity, sold_quantity, min_per_order, max_per_order,
    color_code, sort_order, status, sales_starts_at, sales_ends_at
) VALUES (
    :event1_id, 'Standard GA', 'Vé đứng khu vực General Admission tiêu chuẩn', 1200000, 1200000,
    200, 2, 1, 10, '#3b82f6', 2, 'Active',
    NOW() - INTERVAL '10 days', '2026-12-26 18:00:00+07'::timestamptz
);

SELECT ticket_type_id AS tt_ga_id FROM ticket_types WHERE event_id = :event1_id AND type_name = 'Standard GA' AND is_deleted = false \gset

INSERT INTO ticket_types (
    event_id, type_name, description, price, original_price,
    quantity, sold_quantity, min_per_order, max_per_order,
    color_code, sort_order, status, sales_starts_at, sales_ends_at
) VALUES (
    :event2_id, 'Standard Zone', 'Hạng vé tiêu chuẩn (Sự kiện đã hủy)', 1500000, 1500000,
    200, 0, 1, 10, '#64748b', 1, 'Active',
    NOW() - INTERVAL '10 days', '2026-12-31 18:00:00+07'::timestamptz
);

-- =============================================================================
-- 3. TICKET_DB: Tạo Đơn hàng Paid & 5 Vé của Sự kiện 1 (chưa bị hủy) cho Customer mới
-- =============================================================================
\connect ticket_db

-- Xóa dữ liệu vé cũ của Customer mới (nếu chạy lại script)
DELETE FROM ticket_qr_tokens WHERE ticket_id IN (SELECT ticket_id FROM tickets WHERE owner_user_id = :cust_id);
DELETE FROM tickets WHERE owner_user_id = :cust_id;
DELETE FROM order_attendees WHERE order_id IN (SELECT order_id FROM orders WHERE customer_id = :cust_id);
DELETE FROM order_details WHERE order_id IN (SELECT order_id FROM orders WHERE customer_id = :cust_id);
DELETE FROM orders WHERE customer_id = :cust_id;

-- 3.1. Tạo Order (5 vé: 3 VIP Diamond + 2 Standard GA = 9,900,000 VNĐ)
INSERT INTO orders (
    customer_id, event_id, full_name, email, phone,
    event_name, poster_url, order_date, starts_at, ends_at,
    total_amount, discount_amount, final_amount,
    payment_method, payment_gateway_ref, status, is_deleted
) VALUES (
    :cust_id, :event1_id,
    'Nguyễn Minh Khôi', 'minhkhoi.customer@ticketbox.com', '0908123456',
    'STARLIGHT SYMPHONY FEST 2026 – LIVE IN SAIGON',
    'https://images.unsplash.com/photo-1540039155733-5bb30b53aa14?auto=format&fit=crop&w=1200&q=80',
    NOW() - INTERVAL '2 hours',
    '2026-12-26 19:00:00+07'::timestamptz,
    '2026-12-26 23:00:00+07'::timestamptz,
    9900000, 0, 9900000,
    'VNPay', 'VNPAY-STARLIGHT-5TIX-2026', 'Paid', false
);

SELECT order_id AS ord_id FROM orders WHERE customer_id = :cust_id AND payment_gateway_ref = 'VNPAY-STARLIGHT-5TIX-2026' ORDER BY order_id DESC LIMIT 1 \gset

-- 3.2. Tạo Order Details
INSERT INTO order_details (order_id, ticket_type_id, ticket_type_name, quantity, unit_price) VALUES
(:ord_id, :tt_vip_id, 'VIP Diamond', 3, 2500000),
(:ord_id, :tt_ga_id, 'Standard GA', 2, 1200000);

-- 3.3. Tạo 5 Order Attendees (đầy đủ Họ tên, SĐT 0908123456, CCCD 079201012345) và 5 Tickets + 5 QR Tokens
-- Vé 1 (VIP Diamond - Primary Buyer)
INSERT INTO order_attendees (order_id, ticket_type_id, full_name, citizen_id, phone, is_primary_buyer)
VALUES (:ord_id, :tt_vip_id, 'Nguyễn Minh Khôi', '079201012345', '0908123456', true);
SELECT max(attendee_id) AS att1_id FROM order_attendees WHERE order_id = :ord_id \gset

INSERT INTO tickets (order_id, event_id, ticket_type_id, owner_user_id, ticket_type_name, owner_name, status, gate_id, order_attendee_id, is_deleted)
VALUES (:ord_id, :event1_id, :tt_vip_id, :cust_id, 'VIP Diamond', 'Nguyễn Minh Khôi', 'Active', 'Gate A - VIP Entrance', :att1_id, false);
SELECT max(ticket_id) AS tix1_id FROM tickets WHERE order_id = :ord_id \gset

INSERT INTO ticket_qr_tokens (ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used)
VALUES (:tix1_id, 'QR-STARLIGHT-VIP-01-' || :tix1_id, NOW(), NOW() + INTERVAL '60 days', false, false);

-- Vé 2 (VIP Diamond)
INSERT INTO order_attendees (order_id, ticket_type_id, full_name, citizen_id, phone, is_primary_buyer)
VALUES (:ord_id, :tt_vip_id, 'Nguyễn Minh Khôi', '079201012345', '0908123456', false);
SELECT max(attendee_id) AS att2_id FROM order_attendees WHERE order_id = :ord_id \gset

INSERT INTO tickets (order_id, event_id, ticket_type_id, owner_user_id, ticket_type_name, owner_name, status, gate_id, order_attendee_id, is_deleted)
VALUES (:ord_id, :event1_id, :tt_vip_id, :cust_id, 'VIP Diamond', 'Nguyễn Minh Khôi', 'Active', 'Gate A - VIP Entrance', :att2_id, false);
SELECT max(ticket_id) AS tix2_id FROM tickets WHERE order_id = :ord_id \gset

INSERT INTO ticket_qr_tokens (ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used)
VALUES (:tix2_id, 'QR-STARLIGHT-VIP-02-' || :tix2_id, NOW(), NOW() + INTERVAL '60 days', false, false);

-- Vé 3 (VIP Diamond)
INSERT INTO order_attendees (order_id, ticket_type_id, full_name, citizen_id, phone, is_primary_buyer)
VALUES (:ord_id, :tt_vip_id, 'Nguyễn Minh Khôi', '079201012345', '0908123456', false);
SELECT max(attendee_id) AS att3_id FROM order_attendees WHERE order_id = :ord_id \gset

INSERT INTO tickets (order_id, event_id, ticket_type_id, owner_user_id, ticket_type_name, owner_name, status, gate_id, order_attendee_id, is_deleted)
VALUES (:ord_id, :event1_id, :tt_vip_id, :cust_id, 'VIP Diamond', 'Nguyễn Minh Khôi', 'Active', 'Gate A - VIP Entrance', :att3_id, false);
SELECT max(ticket_id) AS tix3_id FROM tickets WHERE order_id = :ord_id \gset

INSERT INTO ticket_qr_tokens (ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used)
VALUES (:tix3_id, 'QR-STARLIGHT-VIP-03-' || :tix3_id, NOW(), NOW() + INTERVAL '60 days', false, false);

-- Vé 4 (Standard GA)
INSERT INTO order_attendees (order_id, ticket_type_id, full_name, citizen_id, phone, is_primary_buyer)
VALUES (:ord_id, :tt_ga_id, 'Nguyễn Minh Khôi', '079201012345', '0908123456', false);
SELECT max(attendee_id) AS att4_id FROM order_attendees WHERE order_id = :ord_id \gset

INSERT INTO tickets (order_id, event_id, ticket_type_id, owner_user_id, ticket_type_name, owner_name, status, gate_id, order_attendee_id, is_deleted)
VALUES (:ord_id, :event1_id, :tt_ga_id, :cust_id, 'Standard GA', 'Nguyễn Minh Khôi', 'Active', 'Gate B - Standard Entrance', :att4_id, false);
SELECT max(ticket_id) AS tix4_id FROM tickets WHERE order_id = :ord_id \gset

INSERT INTO ticket_qr_tokens (ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used)
VALUES (:tix4_id, 'QR-STARLIGHT-GA-04-' || :tix4_id, NOW(), NOW() + INTERVAL '60 days', false, false);

-- Vé 5 (Standard GA)
INSERT INTO order_attendees (order_id, ticket_type_id, full_name, citizen_id, phone, is_primary_buyer)
VALUES (:ord_id, :tt_ga_id, 'Nguyễn Minh Khôi', '079201012345', '0908123456', false);
SELECT max(attendee_id) AS att5_id FROM order_attendees WHERE order_id = :ord_id \gset

INSERT INTO tickets (order_id, event_id, ticket_type_id, owner_user_id, ticket_type_name, owner_name, status, gate_id, order_attendee_id, is_deleted)
VALUES (:ord_id, :event1_id, :tt_ga_id, :cust_id, 'Standard GA', 'Nguyễn Minh Khôi', 'Active', 'Gate B - Standard Entrance', :att5_id, false);
SELECT max(ticket_id) AS tix5_id FROM tickets WHERE order_id = :ord_id \gset

INSERT INTO ticket_qr_tokens (ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used)
VALUES (:tix5_id, 'QR-STARLIGHT-GA-05-' || :tix5_id, NOW(), NOW() + INTERVAL '60 days', false, false);

SELECT
    :cust_id AS customer_user_id,
    :org_id AS organizer_user_id,
    :event1_id AS active_event_id,
    :event2_id AS cancelled_event_id,
    :ord_id AS order_id,
    :tix1_id AS ticket_1,
    :tix2_id AS ticket_2,
    :tix3_id AS ticket_3,
    :tix4_id AS ticket_4,
    :tix5_id AS ticket_5;
