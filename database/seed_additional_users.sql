-- Seed additional accounts for testing (Idempotent)
INSERT INTO users (full_name, email, phone_number, password_hash, is_verified, is_active, auth_provider, has_password, ekyc_status, created_at)
VALUES 
    ('Customer One (Nguyen Van An)', 'customer1@ticketbox.com', '0981111111', '$2a$11$H4HcBDSBWC6uPn1WXyMm0.EqGbPaKbRLxzs7I/lgLohWmW77ekYeS', true, true, 'local', true, 'NotSubmitted', NOW()),
    ('Customer Two (Tran Thi Binh)', 'customer2@ticketbox.com', '0982222222', '$2a$11$H4HcBDSBWC6uPn1WXyMm0.EqGbPaKbRLxzs7I/lgLohWmW77ekYeS', true, true, 'local', true, 'NotSubmitted', NOW()),
    ('Customer Three (Le Van Cuong)', 'customer3@ticketbox.com', '0983333333', '$2a$11$H4HcBDSBWC6uPn1WXyMm0.EqGbPaKbRLxzs7I/lgLohWmW77ekYeS', true, true, 'local', true, 'NotSubmitted', NOW()),
    ('Customer Four (Pham Thi Dung)', 'customer4@ticketbox.com', '0984444444', '$2a$11$H4HcBDSBWC6uPn1WXyMm0.EqGbPaKbRLxzs7I/lgLohWmW77ekYeS', true, true, 'local', true, 'NotSubmitted', NOW()),
    ('Customer Five (Hoang Van Em)', 'customer5@ticketbox.com', '0985555555', '$2a$11$H4HcBDSBWC6uPn1WXyMm0.EqGbPaKbRLxzs7I/lgLohWmW77ekYeS', true, true, 'local', true, 'NotSubmitted', NOW()),
    ('Customer Six (Vu Thi Giang)', 'customer6@ticketbox.com', '0986666666', '$2a$11$H4HcBDSBWC6uPn1WXyMm0.EqGbPaKbRLxzs7I/lgLohWmW77ekYeS', true, true, 'local', true, 'NotSubmitted', NOW()),
    ('Customer Seven (Dang Van Hai)', 'customer7@ticketbox.com', '0987777777', '$2a$11$H4HcBDSBWC6uPn1WXyMm0.EqGbPaKbRLxzs7I/lgLohWmW77ekYeS', true, true, 'local', true, 'NotSubmitted', NOW()),
    ('Minh Test Customer', 'minhttv12@gmail.com', '0989999999', '$2a$11$H4HcBDSBWC6uPn1WXyMm0.EqGbPaKbRLxzs7I/lgLohWmW77ekYeS', true, true, 'local', true, 'NotSubmitted', NOW()),
    ('Organizer One (Saigon Sound Entertainment)', 'organizer1@ticketbox.com', '0911111111', '$2a$11$7SKcRyOK56NKx/gH4B8RWevtpMW9FfPGaWQCnVZqmrc7a8CfgcHAu', true, true, 'local', true, 'NotSubmitted', NOW()),
    ('Organizer Two (Hanoi Music Production)', 'organizer2@ticketbox.com', '0912222222', '$2a$11$7SKcRyOK56NKx/gH4B8RWevtpMW9FfPGaWQCnVZqmrc7a8CfgcHAu', true, true, 'local', true, 'NotSubmitted', NOW())
ON CONFLICT (email) DO NOTHING;

-- Assign Customer Role
INSERT INTO user_roles (user_id, role_id)
SELECT u.user_id, r.role_id
FROM users u, roles r
WHERE u.email IN (
    'customer1@ticketbox.com', 'customer2@ticketbox.com', 'customer3@ticketbox.com',
    'customer4@ticketbox.com', 'customer5@ticketbox.com', 'customer6@ticketbox.com',
    'customer7@ticketbox.com', 'minhttv12@gmail.com', 'organizer1@ticketbox.com', 'organizer2@ticketbox.com'
)
  AND r.role_name = 'Customer'
ON CONFLICT DO NOTHING;

-- Assign Organizer Role
INSERT INTO user_roles (user_id, role_id)
SELECT u.user_id, r.role_id
FROM users u, roles r
WHERE u.email IN ('organizer1@ticketbox.com', 'organizer2@ticketbox.com')
  AND r.role_name = 'Organizer'
ON CONFLICT DO NOTHING;
