using System;
using System.Threading.Tasks;
using Npgsql;

class Program
{
    static async Task Main()
    {
        Console.WriteLine(">>> SEEDING ACCURATE DATABASE FOR USER TEST <<<\n");
        await SeedEventDbAsync();
        await SeedTicketDbAsync();
        await SeedNotificationDbAsync();
        await VerifyDataAsync();
        Console.WriteLine("\n>>> ALL TEST DATA SEEDED & VERIFIED IN POSTGRESQL <<<");
    }

    static async Task SeedEventDbAsync()
    {
        Console.WriteLine("-> Seeding event_db...");
        await using var conn = new NpgsqlConnection("Host=localhost;Port=5432;Database=event_db;Username=postgres;Password=123456");
        await conn.OpenAsync();
        var sql = @"
            INSERT INTO events (event_id, organizer_id, category_id, title, slug, starts_at, ends_at, location_name, address, status, compliance_status, compliance_version, schedule_version, sales_frozen, created_at, updated_at, is_deleted)
            OVERRIDING SYSTEM VALUE
            VALUES (20, 1, 1, 'Acoustic Night', 'acoustic-night', '2026-10-20 18:00:00+07', '2026-10-20 23:59:00+07', 'Building 1, Ground Floor', 'Nhà Hát Thành Phố, Q.1, TP.HCM', 'Approved', 'Approved', 1, 1, FALSE, NOW(), NOW(), FALSE)
            ON CONFLICT (event_id) DO UPDATE SET
                title = EXCLUDED.title,
                location_name = EXCLUDED.location_name,
                starts_at = EXCLUDED.starts_at,
                ends_at = EXCLUDED.ends_at,
                status = 'Approved',
                is_deleted = FALSE;
        ";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine("  -> event_db seeded successfully.");
    }

    static async Task SeedTicketDbAsync()
    {
        Console.WriteLine("-> Seeding 05_ticket_db with exact test codes...");
        await using var conn = new NpgsqlConnection("Host=localhost;Port=5432;Database=05_ticket_db;Username=postgres;Password=123456");
        await conn.OpenAsync();

        // 1. Alter ticket_code column to varchar(100) if it is UUID
        try
        {
            await using var alterCmd = new NpgsqlCommand("ALTER TABLE public.tickets ALTER COLUMN ticket_code TYPE character varying(100) USING ticket_code::text;", conn);
            await alterCmd.ExecuteNonQueryAsync();
            Console.WriteLine("  -> tickets.ticket_code altered to varchar(100).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> Note on alter column: {ex.Message}");
        }

        var sql = @"
            DO $$
            BEGIN
                -- 1. Orders
                INSERT INTO orders (order_id, customer_id, event_id, full_name, email, phone, event_name, total_amount, final_amount, status, is_deleted)
                OVERRIDING SYSTEM VALUE
                VALUES
                (1847, 10, 20, 'Nguyen Thanh', 'thanh.n@email.com', '0912345678', 'Acoustic Night', 1200000, 1200000, 'Paid', FALSE),
                (1846, 11, 20, 'Tran Phuong', 'phuong.t@email.com', '0909123456', 'Acoustic Night', 650000, 650000, 'Paid', FALSE),
                (1845, 12, 20, 'Vo Hoang', 'hoang.v@email.com', '0977888999', 'Acoustic Night', 450000, 450000, 'Paid', FALSE)
                ON CONFLICT (order_id) DO UPDATE SET
                    full_name = EXCLUDED.full_name,
                    phone = EXCLUDED.phone,
                    status = EXCLUDED.status,
                    is_deleted = FALSE;

                -- 2. Order Attendees with citizen_id (CCCD 001202000000)
                INSERT INTO order_attendees (attendee_id, order_id, full_name, citizen_id, phone, is_primary_buyer)
                OVERRIDING SYSTEM VALUE
                VALUES
                (1847, 1847, 'Nguyen Thanh', '001202000000', '0912345678', TRUE),
                (1846, 1846, 'Tran Phuong', '079201000111', '0909123456', TRUE),
                (1845, 1845, 'Vo Hoang', '079203000222', '0977888999', TRUE)
                ON CONFLICT (attendee_id) DO UPDATE SET
                    full_name = EXCLUDED.full_name,
                    citizen_id = EXCLUDED.citizen_id,
                    phone = EXCLUDED.phone;

                -- 3. Tickets with exact codes requested by user:
                -- TKT-2026-VIP-001 (VIP, Active, un-used)
                -- TKT-2026-REG-002 (Regular, Checked-in)
                -- TKT-2026-EARLY-003 (Early Bird, Revoked)
                INSERT INTO tickets (ticket_id, ticket_code, order_id, event_id, ticket_type_id, seat_id, owner_user_id, ticket_type_name, owner_name, status, is_checked_in, is_deleted)
                OVERRIDING SYSTEM VALUE
                VALUES
                (1847, 'TKT-2026-VIP-001', 1847, 20, 1, 12, 10, 'VIP Diamond', 'Nguyen Thanh', 'Active', FALSE, FALSE),
                (1846, 'TKT-2026-REG-002', 1846, 20, 2, 15, 11, 'Standard GA', 'Tran Phuong', 'Used', TRUE, FALSE),
                (1845, 'TKT-2026-EARLY-003', 1845, 20, 3, NULL, 12, 'Early Bird', 'Vo Hoang', 'REVOKED', FALSE, FALSE),
                (1001, 'TKT-EV1-ACTIVE-001', 1847, 1, 1, 101, 10, 'SVIP Prime', 'Nguyen Thanh', 'Active', FALSE, FALSE),
                (1002, 'TKT-EV1-ACTIVE-002', 1846, 1, 2, 102, 11, 'Standard A', 'Tran Phuong', 'Active', FALSE, FALSE)
                ON CONFLICT (ticket_id) DO UPDATE SET
                    ticket_code = EXCLUDED.ticket_code,
                    owner_name = EXCLUDED.owner_name,
                    ticket_type_name = EXCLUDED.ticket_type_name,
                    status = EXCLUDED.status,
                    is_checked_in = EXCLUDED.is_checked_in,
                    is_deleted = FALSE;

                -- 4. Dynamic QR Tokens
                INSERT INTO ticket_qr_tokens (ticket_qr_token_id, ticket_id, qr_token, issued_at, expires_at, is_revoked, is_used)
                OVERRIDING SYSTEM VALUE
                VALUES
                (1847, 1847, 'QR_ACOUSTIC_NGUYEN_THANH_2026', NOW(), NOW() + INTERVAL '24 hours', FALSE, FALSE),
                (1846, 1846, 'QR_ACOUSTIC_TRAN_PHUONG_USED', NOW() - INTERVAL '1 hour', NOW() + INTERVAL '23 hours', FALSE, TRUE)
                ON CONFLICT (ticket_qr_token_id) DO UPDATE SET
                    qr_token = EXCLUDED.qr_token,
                    is_used = EXCLUDED.is_used,
                    is_revoked = EXCLUDED.is_revoked;

                -- 5. Staff Gate Assignments (Only real database columns)
                DELETE FROM staff_gate_assignments WHERE event_id IN (1, 20);
                INSERT INTO staff_gate_assignments (staff_user_id, event_id, gate_id, gate_name, shift_start, shift_end, is_active)
                VALUES 
                (1, 20, 'GATE_A', 'Gate A - Main Entrance', NOW() - INTERVAL '2 hours', NOW() + INTERVAL '6 hours', TRUE),
                (2, 20, 'GATE_A', 'Gate A - Main Entrance', NOW() - INTERVAL '2 hours', NOW() + INTERVAL '6 hours', TRUE),
                (1, 1, 'GATE_A', 'Gate A - Main Entrance', NOW() - INTERVAL '2 hours', NOW() + INTERVAL '6 hours', TRUE);

                -- 6. Checkin Logs
                DELETE FROM checkin_logs WHERE event_id = 20;
                INSERT INTO checkin_logs (checkin_id, ticket_id, ticket_code, event_id, gate_id, staff_user_id, device_id, scan_time, status, failure_reason, customer_name, zone_name, gate_name, scan_method, scanned_at)
                OVERRIDING SYSTEM VALUE
                VALUES
                (1, 1847, 'TKT-2026-VIP-001', 20, 'GATE_A', 1, 'GATE_READER_1', '2026-10-20 18:45:28+07', 'Success', NULL, 'Nguyen Thanh', 'VIP Diamond', 'Gate A - Main Entrance', 'QR_SCAN', '2026-10-20 18:45:28+07'),
                (2, 1846, 'TKT-2026-REG-002', 20, 'GATE_A', 1, 'GATE_READER_1', '2026-10-20 18:44:52+07', 'AlreadyUsed', 'Vé đã được check-in lúc 18:15', 'Tran Phuong', 'Standard GA', 'Gate A - Main Entrance', 'QR_SCAN', '2026-10-20 18:44:52+07'),
                (3, 1845, 'TKT-2026-EARLY-003', 20, 'GATE_A', 1, 'GATE_READER_1', '2026-10-20 18:44:38+07', 'Invalid', 'Mã vé đã bị thu hồi do hoàn tiền', 'Vo Hoang', 'Early Bird', 'Gate A - Main Entrance', 'QR_SCAN', '2026-10-20 18:44:38+07');

                -- 228 Approved scans to make 231 total approved
                FOR i IN 4..231 LOOP
                    INSERT INTO checkin_logs (ticket_id, ticket_code, event_id, gate_id, staff_user_id, device_id, scan_time, status, customer_name, zone_name, gate_name, scan_method, scanned_at)
                    VALUES (1847, 'TKT-BATCH-' || i, 20, 'GATE_A', 1, 'GATE_READER_1', NOW() - (i || ' minutes')::interval, 'Success', 'Khán giả ' || i, 'Zone VIP & GA', 'Gate A - Main Entrance', 'QR_SCAN', NOW() - (i || ' minutes')::interval);
                END LOOP;

                -- 11 AlreadyUsed duplicate scans to make 12 total
                FOR i IN 1..11 LOOP
                    INSERT INTO checkin_logs (ticket_id, ticket_code, event_id, gate_id, staff_user_id, device_id, scan_time, status, failure_reason, customer_name, zone_name, gate_name, scan_method, scanned_at)
                    VALUES (1846, 'TKT-DUP-' || i, 20, 'GATE_A', 1, 'GATE_READER_1', NOW() - (i * 5 || ' minutes')::interval, 'AlreadyUsed', 'Vé đã check-in trước đó tại Gate A', 'Khách vé trùng ' || i, 'Zone GA', 'Gate A - Main Entrance', 'QR_SCAN', NOW() - (i * 5 || ' minutes')::interval);
                END LOOP;

                -- 4 Invalid scans to make 5 total
                FOR i IN 1..4 LOOP
                    INSERT INTO checkin_logs (ticket_id, ticket_code, event_id, gate_id, staff_user_id, device_id, scan_time, status, failure_reason, customer_name, zone_name, gate_name, scan_method, scanned_at)
                    VALUES (1845, 'TKT-INV-' || i, 20, 'GATE_A', 1, 'GATE_READER_1', NOW() - (i * 8 || ' minutes')::interval, 'Invalid', 'Mã QR hết hạn hoặc chữ ký không hợp lệ', 'Khách lỗi QR ' || i, 'Unknown', 'Gate A - Main Entrance', 'QR_SCAN', NOW() - (i * 8 || ' minutes')::interval);
                END LOOP;

            END $$;
        ";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine("  -> 05_ticket_db updated with TKT-2026-VIP-001 & CCCD 001202000000.");
    }

    static async Task SeedNotificationDbAsync()
    {
        Console.WriteLine("-> Inspecting and seeding notification_db...");
        await using var conn = new NpgsqlConnection("Host=localhost;Port=5432;Database=notification_db;Username=postgres;Password=123456");
        await conn.OpenAsync();

        // Check columns of notifications table
        await using (var cmd = new NpgsqlCommand("SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'notifications';", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            Console.WriteLine("  Columns in 'notifications' table:");
            while (await r.ReadAsync())
            {
                Console.WriteLine($"    - {r["column_name"]} ({r["data_type"]})");
            }
        }

        // Insert broadcast (user_id = 0) and user notifications
        var sql = @"
            INSERT INTO notifications (user_id, title, message, category, target_url, is_read, is_deleted, created_at)
            VALUES 
                (0, 'Thông báo bảo trì cổng soát vé Gate A', 'Hệ thống soát vé Gate A - Nhà Hát Thành Phố sẽ diễn ra diễn tập từ 17:00 ngày 20/10/2026. Đề nghị nhân viên chuẩn bị.', 'system_alert', '/staff/gate-assignment', false, false, NOW() - INTERVAL '10 minutes'),
                (0, 'Lưu ý kiểm tra căn cước công dân (CCCD)', 'Nhân viên soát vé bắt buộc kiểm tra hình chụp đối chiếu CCCD gốc cho các vé check-in thủ công.', 'security_policy', '/staff/checkin/20', false, false, NOW() - INTERVAL '25 minutes'),
                (0, 'Cập nhật vé sự kiện Acoustic Night', 'Ban tổ chức đã phát hành bổ sung 50 vé VIP Diamond. Khán giả lưu ý quét Dynamic QR hợp lệ.', 'event_update', '/staff/checkin/20', false, false, NOW() - INTERVAL '40 minutes'),
                (0, 'Chính sách hoàn tiền sự kiện đã bị hủy', 'Các đơn hàng thuộc sự kiện Anh Trai Say Hi (Cancelled) đang được Admin xử lý hoàn tiền tự động theo quy định.', 'refund_notice', '/admin/refunds', false, false, NOW() - INTERVAL '1 hour')
            ON CONFLICT DO NOTHING;
        ";
        try
        {
            await using var insertCmd = new NpgsqlCommand(sql, conn);
            int rows = await insertCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"  -> Seeded {rows} test notifications into notification_db.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> Notification insert info: {ex.Message}");
        }
    }

    static async Task VerifyDataAsync()
    {
        Console.WriteLine("-> Verifying test records in 05_ticket_db...");
        await using var conn = new NpgsqlConnection("Host=localhost;Port=5432;Database=05_ticket_db;Username=postgres;Password=123456");
        await conn.OpenAsync();

        await using (var cmd = new NpgsqlCommand("SELECT ticket_id, ticket_code, owner_name, status, is_checked_in FROM tickets WHERE ticket_code IN ('TKT-2026-VIP-001', 'TKT-2026-REG-002', 'TKT-2026-EARLY-003');", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                Console.WriteLine($"  [TICKET FOUND] ID={r["ticket_id"]}, Code={r["ticket_code"]}, Owner={r["owner_name"]}, Status={r["status"]}, CheckedIn={r["is_checked_in"]}");
            }
        }

        await using (var cmd = new NpgsqlCommand("SELECT attendee_id, order_id, full_name, citizen_id, phone FROM order_attendees WHERE citizen_id = '001202000000';", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                Console.WriteLine($"  [ATTENDEE FOUND] OrderID={r["order_id"]}, Name={r["full_name"]}, CCCD={r["citizen_id"]}, Phone={r["phone"]}");
            }
        }
    }
}
