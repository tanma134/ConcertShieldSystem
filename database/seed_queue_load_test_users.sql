BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM roles WHERE role_name = 'Customer') THEN
        RAISE EXCEPTION 'Customer role is missing; seed roles before test users.';
    END IF;
END;
$$;

INSERT INTO users (
    full_name,
    email,
    phone_number,
    password_hash,
    is_verified,
    is_active,
    auth_provider,
    has_password,
    ekyc_status
)
SELECT
    'Queue Load Test ' || lpad(user_number::text, 3, '0'),
    'queue-loadtest-' || lpad(user_number::text, 3, '0') || '@example.test',
    (1000000000 + user_number)::text,
    '$2a$11$H4HcBDSBWC6uPn1WXyMm0.EqGbPaKbRLxzs7I/lgLohWmW77ekYeS',
    TRUE,
    TRUE,
    'local',
    TRUE,
    'NotSubmitted'
FROM generate_series(1, 50) AS gs(user_number)
ON CONFLICT (email) DO UPDATE SET
    full_name = EXCLUDED.full_name,
    phone_number = EXCLUDED.phone_number,
    password_hash = EXCLUDED.password_hash,
    is_verified = TRUE,
    is_active = TRUE,
    auth_provider = 'local',
    has_password = TRUE,
    ekyc_status = 'NotSubmitted';

INSERT INTO user_roles (user_id, role_id)
SELECT u.user_id, r.role_id
FROM users AS u
CROSS JOIN roles AS r
WHERE r.role_name = 'Customer'
  AND u.email IN (
      SELECT 'queue-loadtest-' || lpad(user_number::text, 3, '0') || '@example.test'
      FROM generate_series(1, 50) AS gs(user_number)
  )
ON CONFLICT (user_id, role_id) DO NOTHING;

COMMIT;
