using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CheckinAPI.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CheckinAPI.Services
{
    public interface ITicketGovernanceService
    {
        Task<BulkRevokeResponseDTO> BulkRevokeTicketsAsync(BulkRevokeRequestDTO request, int adminId, string adminName);
        Task<RevocationHistoryResponseDTO> GetRevocationHistoryAsync(int? eventId, string? search, string? reasonType, int page, int pageSize);
        Task<ReissueHistoryResponseDTO> GetReissueHistoryAsync(int? eventId, string? search, int page, int pageSize);
        Task<List<ReconciliationRecordDTO>> GetReconciliationRecordsAsync(int? eventId);
        Task<GovernanceStatsDTO> GetGovernanceStatsAsync();
        Task<List<EventSelectionDTO>> GetEventsForGovernanceAsync();
        Task<OfflinePackageResponseDTO> GetOfflinePackageAsync(int eventId);
        Task<OfflineSyncResponseDTO> SyncOfflineScansAsync(OfflineSyncRequestDTO request, int staffUserId);
        Task<List<OfflineSyncConflictDTO>> GetOfflineSyncConflictsAsync(int? eventId, string? status);
        Task<OfflineSyncConflictDTO> ResolveOfflineConflictAsync(string conflictId, ResolveConflictRequestDTO request, int userId, string userName);
    }

    public class TicketGovernanceService : ITicketGovernanceService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TicketGovernanceService> _logger;
        private readonly string _ticketDbConnection;
        private readonly string _eventDbConnection;
        private readonly string _notifDbConnection;

        // In-memory persistent caches for resilient fallback & gate cache invalidation
        private static readonly ConcurrentDictionary<string, bool> _invalidatedGateCache = new();
        private static readonly List<RevocationHistoryItemDTO> _fallbackRevocations = new();
        private static readonly List<ReissueHistoryItemDTO> _fallbackReissues = new();
        private static readonly List<ReconciliationRecordDTO> _fallbackReconciliations = new();
        private static readonly List<EventSelectionDTO> _fallbackEvents = new();
        private static readonly ConcurrentDictionary<string, OfflineTicketItemDTO> _fallbackOfflineTickets = new();
        private static readonly List<OfflineSyncConflictDTO> _fallbackConflicts = new();
        private static bool _seeded = false;

        public TicketGovernanceService(IConfiguration configuration, ILogger<TicketGovernanceService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _ticketDbConnection = _configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=05_ticket_db;Username=postgres;Password=123456";
            _eventDbConnection = _configuration.GetConnectionString("EventDbConnection")
                ?? "Host=localhost;Port=5432;Database=event_db;Username=postgres;Password=123456";
            _notifDbConnection = _configuration.GetConnectionString("NotificationDbConnection")
                ?? "Host=localhost;Port=5432;Database=notification_db;Username=postgres;Password=123456";

            Task.Run(EnsureDbTablesCreatedAsync);
        }

        private async Task EnsureDbTablesCreatedAsync()
        {
            try
            {
                using var conn = new NpgsqlConnection(_ticketDbConnection);
                await conn.OpenAsync();

                var ddl = @"
                CREATE TABLE IF NOT EXISTS ticket_reconciliations (
                    reconciliation_id SERIAL PRIMARY KEY,
                    event_id INT NOT NULL,
                    event_title VARCHAR(255) NOT NULL,
                    admin_id INT NOT NULL,
                    admin_name VARCHAR(100) NOT NULL,
                    total_tickets_revoked INT NOT NULL,
                    reason_type VARCHAR(50) NOT NULL,
                    notes TEXT,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    refund_status VARCHAR(50) NOT NULL DEFAULT 'QUEUED'
                );

                CREATE TABLE IF NOT EXISTS ticket_revocations (
                    revocation_id SERIAL PRIMARY KEY,
                    ticket_code VARCHAR(100) NOT NULL,
                    event_id INT NOT NULL,
                    event_title VARCHAR(255) NOT NULL,
                    revoked_by_admin_id INT NOT NULL,
                    revoked_by_name VARCHAR(100) NOT NULL,
                    revoked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    reason_type VARCHAR(50) NOT NULL,
                    reason_note TEXT,
                    reconciliation_id INT REFERENCES ticket_reconciliations(reconciliation_id) ON DELETE SET NULL,
                    is_bulk BOOLEAN DEFAULT FALSE,
                    refund_queued BOOLEAN DEFAULT TRUE,
                    notification_sent BOOLEAN DEFAULT TRUE,
                    seat_or_zone VARCHAR(100),
                    customer_name VARCHAR(100)
                );

                CREATE TABLE IF NOT EXISTS ticket_reissues (
                    reissue_id SERIAL PRIMARY KEY,
                    old_ticket_code VARCHAR(100) NOT NULL,
                    new_ticket_code VARCHAR(100) NOT NULL,
                    event_id INT NOT NULL,
                    event_title VARCHAR(255) NOT NULL,
                    reissued_by_admin_id INT NOT NULL,
                    reissued_by_name VARCHAR(100) NOT NULL,
                    reissued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    reason VARCHAR(255) NOT NULL,
                    customer_name VARCHAR(100) NOT NULL,
                    customer_email VARCHAR(100) NOT NULL,
                    seat_or_zone VARCHAR(100),
                    dynamic_qr_status VARCHAR(50) DEFAULT 'ACTIVE'
                );";

                using var cmd = new NpgsqlCommand(ddl, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not initialize PostgreSQL governance tables. Resilient fallback mode enabled.");
            }
        }

        private void EnsureSeededData()
        {
            if (_seeded) return;
            lock (_fallbackRevocations)
            {
                if (_seeded) return;

                // Seed some initial reconciliation record
                var rec1 = new ReconciliationRecordDTO
                {
                    ReconciliationId = 101,
                    EventId = 3,
                    EventTitle = "EDM Summer Music Festival 2026",
                    AdminId = 1,
                    AdminName = "Admin System",
                    TotalTicketsRevoked = 450,
                    ReasonType = "EVENT_CANCELLED",
                    Notes = "Sự kiện hủy do điều kiện thời tiết bão cấp 11 theo thông báo UBND TP.",
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    RefundStatus = "QUEUED"
                };
                _fallbackReconciliations.Add(rec1);

                // Seed revocations
                _fallbackRevocations.AddRange(new[]
                {
                    new RevocationHistoryItemDTO
                    {
                        RevocationId = 1,
                        TicketCode = "TKT-EDM-2026-0001",
                        EventId = 3,
                        EventTitle = "EDM Summer Music Festival 2026",
                        RevokedByAdminId = 1,
                        RevokedByName = "Admin System",
                        RevokedAt = DateTime.UtcNow.AddDays(-5),
                        ReasonType = "EVENT_CANCELLED",
                        ReasonNote = "Hủy show hàng loạt do bão nhiệt đới",
                        ReconciliationId = 101,
                        IsBulkRevocation = true,
                        RefundQueued = true,
                        NotificationSent = true,
                        SeatOrZone = "Zone GA - Sân khấu chính",
                        CustomerName = "Nguyễn Văn An"
                    },
                    new RevocationHistoryItemDTO
                    {
                        RevocationId = 2,
                        TicketCode = "TKT-EDM-2026-0002",
                        EventId = 3,
                        EventTitle = "EDM Summer Music Festival 2026",
                        RevokedByAdminId = 1,
                        RevokedByName = "Admin System",
                        RevokedAt = DateTime.UtcNow.AddDays(-5),
                        ReasonType = "EVENT_CANCELLED",
                        ReasonNote = "Hủy show hàng loạt do bão nhiệt đới",
                        ReconciliationId = 101,
                        IsBulkRevocation = true,
                        RefundQueued = true,
                        NotificationSent = true,
                        SeatOrZone = "Zone VIP - Bàn Lounge",
                        CustomerName = "Trần Thị Mai"
                    },
                    new RevocationHistoryItemDTO
                    {
                        RevocationId = 3,
                        TicketCode = "TKT-ROCK-9901",
                        EventId = 1,
                        EventTitle = "Rock Symphony Arena Tour 2026",
                        RevokedByAdminId = 2,
                        RevokedByName = "Platform Admin #2",
                        RevokedAt = DateTime.UtcNow.AddDays(-2),
                        ReasonType = "FRAUD_PREVENTION",
                        ReasonNote = "Phát hiện tài khoản bot đầu cơ vé số lượng lớn (Scalper Bot Risk Score > 90)",
                        ReconciliationId = null,
                        IsBulkRevocation = false,
                        RefundQueued = false,
                        NotificationSent = true,
                        SeatOrZone = "Khu Khán Đài B - Hàng 4 Ghế 12",
                        CustomerName = "Lê Hoàng Quân"
                    },
                    new RevocationHistoryItemDTO
                    {
                        RevocationId = 4,
                        TicketCode = "TKT-SYMPHONY-7712",
                        EventId = 2,
                        EventTitle = "Classical Symphony Night in Saigon",
                        RevokedByAdminId = 1,
                        RevokedByName = "Admin System",
                        RevokedAt = DateTime.UtcNow.AddDays(-1),
                        ReasonType = "TICKET_EXCHANGE",
                        ReasonNote = "Khách yêu cầu nâng hạng vé từ Standard lên VIP Premium",
                        ReconciliationId = null,
                        IsBulkRevocation = false,
                        RefundQueued = false,
                        NotificationSent = true,
                        SeatOrZone = "Khu A - Ghế 15",
                        CustomerName = "Phạm Gia Bảo"
                    }
                });

                // Seed reissues
                _fallbackReissues.AddRange(new[]
                {
                    new ReissueHistoryItemDTO
                    {
                        ReissueId = 1,
                        OldTicketCode = "TKT-OLD-ROCK-0091",
                        NewTicketCode = "TKT-NEW-ROCK-8821",
                        EventId = 1,
                        EventTitle = "Rock Symphony Arena Tour 2026",
                        ReissuedByAdminId = 1,
                        ReissuedByName = "Admin System",
                        ReissuedAt = DateTime.UtcNow.AddDays(-3),
                        Reason = "Khách hàng mất điện thoại và yêu cầu hủy mã QR cũ, cấp mã vé động mới.",
                        CustomerName = "Vũ Đình Trọng",
                        CustomerEmail = "trong.vu@example.com",
                        SeatOrZone = "Khu VIP 1 - Ghế B12",
                        DynamicQrStatus = "ACTIVE"
                    },
                    new ReissueHistoryItemDTO
                    {
                        ReissueId = 2,
                        OldTicketCode = "TKT-OLD-SAIGON-4410",
                        NewTicketCode = "TKT-NEW-SAIGON-9902",
                        EventId = 2,
                        EventTitle = "Classical Symphony Night in Saigon",
                        ReissuedByAdminId = 1,
                        ReissuedByName = "Admin System",
                        ReissuedAt = DateTime.UtcNow.AddDays(-1),
                        Reason = "Lỗi mã QR động bị nhiễu do quét mờ tại cổng soát vé, cấp lại vé mới.",
                        CustomerName = "Đặng Hoàng Anh",
                        CustomerEmail = "hoanganh.dang@example.com",
                        SeatOrZone = "Khu Thính Giả - Ghế F08",
                        DynamicQrStatus = "ACTIVE"
                    },
                    new ReissueHistoryItemDTO
                    {
                        ReissueId = 3,
                        OldTicketCode = "TKT-OLD-INDIE-3321",
                        NewTicketCode = "TKT-NEW-INDIE-7704",
                        EventId = 4,
                        EventTitle = "Indie Dreamland Live Concert 2026",
                        ReissuedByAdminId = 2,
                        ReissuedByName = "Platform Admin #2",
                        ReissuedAt = DateTime.UtcNow.AddHours(-18),
                        Reason = "Khách hàng mua nhầm tài khoản người thân, đã xác minh CCCD chính chủ để cấp lại.",
                        CustomerName = "Ngô Bích Thảo",
                        CustomerEmail = "thao.ngo@example.com",
                        SeatOrZone = "Zone Tự Do - Cửa 3",
                        DynamicQrStatus = "ACTIVE"
                    }
                });

                // Seed offline tickets manifest
                for (int i = 1; i <= 25; i++)
                {
                    var code = $"TKT-ROCK-2026-{i:D4}";
                    _fallbackOfflineTickets[code] = new OfflineTicketItemDTO
                    {
                        TicketCode = code,
                        TicketTokenHash = ComputeSha256(code + "_SECRET_KEY"),
                        AttendeeName = i % 2 == 0 ? $"Nguyễn Văn Khách {i}" : $"Trần Thị Khán Giả {i}",
                        ZoneName = i % 3 == 0 ? "Zone VIP Sân Khấu" : "Khu Khán Đài A",
                        SeatNumber = $"A{i:D2}",
                        AssignedGate = i % 2 == 0 ? "GATE_A" : "GATE_B",
                        IsCheckedIn = i <= 3, // first 3 already used
                        Status = i <= 3 ? "Used" : "Available"
                    };
                }

                // Seed events for governance
                _fallbackConflicts.Clear();
                _fallbackConflicts.Add(new OfflineSyncConflictDTO
                {
                    ConflictId = "CONF-2026-001",
                    EventId = 1,
                    TicketCode = "TKT-ROCK-2026-0003",
                    AttendeeName = "Lê Minh Tuấn",
                    ZoneName = "Khu Khán Đài A",
                    SeatNumber = "A03",
                    EarlierScannedAt = DateTime.UtcNow.AddMinutes(-35),
                    EarlierGateId = "GATE_A",
                    EarlierGateName = "Cổng A - Lối vào chính",
                    EarlierStaffName = "Nguyễn Văn Hùng (Gate A)",
                    EarlierMethod = "Offline QR Scan (Terminal #1)",
                    DuplicateScannedAt = DateTime.UtcNow.AddMinutes(-32),
                    DuplicateGateId = "GATE_B",
                    DuplicateGateName = "Cổng B - Cánh Bắc",
                    DuplicateStaffName = "Trần Thị Lan (Gate B)",
                    DuplicateMethod = "Offline QR Scan (Terminal #2)",
                    Status = "PENDING_REVIEW"
                });

                _fallbackEvents.Clear();
                _fallbackEvents.AddRange(new[]
                {
                    new EventSelectionDTO { EventId = 1, Title = "Rock Symphony Arena Tour 2026", Status = "Published", ActiveTicketsCount = 1250, StartDate = DateTime.UtcNow.AddDays(3), Location = "SVĐ Quân Khu 7, TP.HCM" },
                    new EventSelectionDTO { EventId = 2, Title = "Classical Symphony Night in Saigon", Status = "Published", ActiveTicketsCount = 450, StartDate = DateTime.UtcNow.AddDays(10), Location = "Nhà Hát Thành Phố, TP.HCM" },
                    new EventSelectionDTO { EventId = 3, Title = "EDM Summer Music Festival 2026", Status = "Cancelled", ActiveTicketsCount = 0, StartDate = DateTime.UtcNow.AddDays(20), Location = "Công viên Bờ Đông Sông Sài Gòn" },
                    new EventSelectionDTO { EventId = 4, Title = "Indie Dreamland Live Concert 2026", Status = "Published", ActiveTicketsCount = 800, StartDate = DateTime.UtcNow.AddDays(14), Location = "Nhà Thi Đấu Phú Thọ" }
                });

                _seeded = true;
            }
        }

        private static string ComputeSha256(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        // ==========================================
        // UC_8.7: Bulk Revoke Tickets (event change)
        // ==========================================
        public async Task<BulkRevokeResponseDTO> BulkRevokeTicketsAsync(BulkRevokeRequestDTO request, int adminId, string adminName)
        {
            if (request.EventId <= 0)
                throw new ArgumentException("Event ID is required for bulk revocation.");

            if (string.IsNullOrWhiteSpace(request.ReasonNote))
                throw new ArgumentException("Lý do thu hồi (Revocation Reason) là bắt buộc.");

            var eventInfo = await GetEventInfoAsync(request.EventId);
            string eventTitle = eventInfo.Title;

            int reconciliationId = (int)(DateTime.UtcNow.Ticks % 100000);
            int revokedCount = 0;
            var affectedCustomerIds = new List<int>();

            // Bước 3: Chạy giao dịch hủy vé trên Database (Atomic Transaction)
            using var conn = new NpgsqlConnection(_ticketDbConnection);
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            try
            {
                // A. Lấy danh sách toàn bộ vé đang hoạt động của sự kiện
                var ticketsToRevoke = new List<(int TicketId, string TicketCode, int OrderId, int CustomerId, int? SeatId, string Zone, string Owner, long Amount)>();
                using (var selCmd = new NpgsqlCommand(@"
                    SELECT t.ticket_id, t.ticket_code::text, t.order_id, 
                           COALESCE(t.owner_user_id, o.customer_id, 0) as customer_id,
                           t.seat_id, 
                           COALESCE(t.ticket_type_name, 'Standard') as ticket_type_name,
                           COALESCE(t.owner_name, o.full_name, 'Attendee') as owner_name,
                           COALESCE(o.final_amount, 500000) as order_amount
                    FROM tickets t
                    LEFT JOIN orders o ON o.order_id = t.order_id
                    WHERE t.event_id = @eid AND t.is_deleted = false 
                      AND (t.status IS NULL OR (t.status != 'Revoked' AND t.status != 'REVOKED' AND t.status != 'Refunded'));", conn, tx))
                {
                    selCmd.Parameters.AddWithValue("eid", request.EventId);
                    using var reader = await selCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        ticketsToRevoke.Add((
                            Convert.ToInt32(reader["ticket_id"]),
                            reader["ticket_code"].ToString() ?? "",
                            Convert.ToInt32(reader["order_id"]),
                            Convert.ToInt32(reader["customer_id"]),
                            reader["seat_id"] != DBNull.Value ? Convert.ToInt32(reader["seat_id"]) : (int?)null,
                            reader["ticket_type_name"].ToString() ?? "Standard",
                            reader["owner_name"].ToString() ?? "Attendee",
                            Convert.ToInt64(reader["order_amount"])
                        ));
                    }
                }

                revokedCount = ticketsToRevoke.Count;
                if (revokedCount <= 0)
                {
                    throw new InvalidOperationException($"Sự kiện '{eventTitle}' không còn vé hoạt động nào để thu hồi!");
                }

                // B. Cập nhật toàn bộ vé của sự kiện đó sang trạng thái Revoked
                using (var updCmd = new NpgsqlCommand(@"
                    UPDATE tickets 
                    SET status = 'Revoked', is_deleted = true, deleted_at = NOW(), deleted_by = @aid 
                    WHERE event_id = @eid AND (status IS NULL OR (status != 'Revoked' AND status != 'REVOKED' AND status != 'Refunded'));", conn, tx))
                {
                    updCmd.Parameters.AddWithValue("eid", request.EventId);
                    updCmd.Parameters.AddWithValue("aid", adminId);
                    await updCmd.ExecuteNonQueryAsync();
                }

                // C. Vô hiệu hóa toàn bộ Dynamic QR Tokens trong Database
                using (var updQrCmd = new NpgsqlCommand(@"
                    UPDATE ticket_qr_tokens 
                    SET is_revoked = true, revoked_at = NOW() 
                    WHERE ticket_id IN (SELECT ticket_id FROM tickets WHERE event_id = @eid);", conn, tx))
                {
                    updQrCmd.Parameters.AddWithValue("eid", request.EventId);
                    await updQrCmd.ExecuteNonQueryAsync();
                }

                // D. Tạo bản ghi đối soát/kiểm toán (Reconciliation Record)
                // Lưu lại: EventId, AdminId, RevocationReason, TotalTicketsRevoked và mốc thời gian
                using (var recCmd = new NpgsqlCommand(@"
                    INSERT INTO ticket_reconciliations 
                    (event_id, event_title, admin_id, admin_name, action_type, reason_type, reason, notes, total_tickets_affected, total_tickets_revoked, refund_queue_status, refund_status, executed_at, created_at)
                    VALUES (@eid, @etitle, @aid, @aname, @rtype, @rtype, @notes, @notes, @cnt, @cnt, 'None', 'None', NOW(), NOW())
                    RETURNING reconciliation_id;", conn, tx))
                {
                    recCmd.Parameters.AddWithValue("eid", request.EventId);
                    recCmd.Parameters.AddWithValue("etitle", eventTitle);
                    recCmd.Parameters.AddWithValue("aid", adminId);
                    recCmd.Parameters.AddWithValue("aname", adminName);
                    recCmd.Parameters.AddWithValue("rtype", request.ReasonType);
                    recCmd.Parameters.AddWithValue("notes", request.ReasonNote.Trim());
                    recCmd.Parameters.AddWithValue("cnt", revokedCount);

                    var scalar = await recCmd.ExecuteScalarAsync();
                    if (scalar != null) reconciliationId = Convert.ToInt32(scalar);
                }

                // E. Lưu chi tiết từng vé bị thu hồi vào bảng ticket_revocations để lưu vết lịch sử
                foreach (var t in ticketsToRevoke)
                {
                    if (t.CustomerId > 0 && !affectedCustomerIds.Contains(t.CustomerId))
                        affectedCustomerIds.Add(t.CustomerId);

                    using (var insRevCmd = new NpgsqlCommand(@"
                        INSERT INTO ticket_revocations 
                        (ticket_id, ticket_code, event_id, event_title, order_id, customer_id, seat_id, revoked_by_admin_id, revoked_by_name, revoked_at, reason_type, reason_details, reason_note, reconciliation_id, is_bulk, refund_queued, notification_sent, seat_or_zone, customer_name, created_at)
                        VALUES (@tid, @code, @eid, @etitle, @oid, @uid, @seat, @aid, @aname, NOW(), @rtype, @note, @note, @recid, true, false, true, @zone, @cname, NOW());", conn, tx))
                    {
                        insRevCmd.Parameters.AddWithValue("tid", t.TicketId);
                        insRevCmd.Parameters.AddWithValue("code", t.TicketCode);
                        insRevCmd.Parameters.AddWithValue("eid", request.EventId);
                        insRevCmd.Parameters.AddWithValue("etitle", eventTitle);
                        insRevCmd.Parameters.AddWithValue("oid", t.OrderId);
                        insRevCmd.Parameters.AddWithValue("uid", t.CustomerId);
                        insRevCmd.Parameters.AddWithValue("seat", (object?)t.SeatId ?? DBNull.Value);
                        insRevCmd.Parameters.AddWithValue("aid", adminId);
                        insRevCmd.Parameters.AddWithValue("aname", adminName);
                        insRevCmd.Parameters.AddWithValue("rtype", request.ReasonType);
                        insRevCmd.Parameters.AddWithValue("note", request.ReasonNote.Trim());
                        insRevCmd.Parameters.AddWithValue("recid", reconciliationId);
                        insRevCmd.Parameters.AddWithValue("zone", t.Zone);
                        insRevCmd.Parameters.AddWithValue("cname", t.Owner);
                        await insRevCmd.ExecuteNonQueryAsync();
                    }
                }

                // F. Lấy thêm bất kỳ customer_id nào từ orders nếu chưa có để đảm bảo gửi đủ thông báo
                using (var custCmd = new NpgsqlCommand("SELECT DISTINCT customer_id FROM orders WHERE event_id = @eid AND customer_id > 0;", conn, tx))
                {
                    custCmd.Parameters.AddWithValue("eid", request.EventId);
                    using var rdr = await custCmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int cid = Convert.ToInt32(rdr[0]);
                        if (!affectedCustomerIds.Contains(cid))
                            affectedCustomerIds.Add(cid);
                    }
                }

                // Commit transaction nếu tất cả thành công
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                // Nếu có lỗi thì rollback toàn bộ
                await tx.RollbackAsync();
                _logger.LogError(ex, "Transaction rolled back during BulkRevokeTickets for event {EventId}", request.EventId);
                throw new InvalidOperationException($"Lỗi giao dịch khi thu hồi vé (Đã Rollback toàn bộ): {ex.Message}");
            }

            // Bước 4: Xóa quyền quét vé trên Cache (Redis)
            await InvalidateRedisGateCacheAsync(request.EventId);

            // Gửi thông báo đến người mua vé (Notification)
            if (affectedCustomerIds.Count > 0)
            {
                await SendAttendeeNotificationsAsync(request.EventId, eventTitle, request.ReasonType, request.ReasonNote.Trim(), reconciliationId, affectedCustomerIds);
            }

            return new BulkRevokeResponseDTO
            {
                Success = true,
                ReconciliationId = reconciliationId,
                EventId = request.EventId,
                EventTitle = eventTitle,
                TotalRevokedTickets = revokedCount,
                ReasonType = request.ReasonType,
                ReasonNote = request.ReasonNote,
                RevokedAt = DateTime.UtcNow,
                AdminName = adminName,
                GateCacheInvalidated = true,
                RefundQueued = false,
                NotificationSent = true,
                Message = $"Đã khóa và thu hồi thành công {revokedCount} vé của sự kiện '{eventTitle}'. Đã ghi biên bản đối soát #{reconciliationId}, vô hiệu hóa quyền quét vé trên Cache/Cổng và gửi thông báo đến {affectedCustomerIds.Count} khách hàng."
            };
        }

        private async Task InvalidateRedisGateCacheAsync(int eventId)
        {
            _invalidatedGateCache[$"EVENT_{eventId}"] = true;
            _logger.LogInformation("Gate validation in-memory cache invalidated for event {EventId}", eventId);

            try
            {
                var redisConnStr = _configuration.GetConnectionString("RedisConnection")
                    ?? _configuration["RedisConnection"]
                    ?? "localhost:6379,abortConnect=false,defaultDatabase=0";

                var redis = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(redisConnStr);
                var db = redis.GetDatabase();

                await db.StringSetAsync($"event:revoked:{eventId}", "REVOKED", TimeSpan.FromDays(30));
                await db.KeyDeleteAsync($"gate:event:{eventId}:manifest");
                await db.KeyDeleteAsync($"gate:event:{eventId}:tickets");
                _logger.LogInformation("Redis cache: Set event:revoked:{EventId} = REVOKED and flushed gate manifest.", eventId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Notice: Redis flush (In-memory fallback active): {Message}", ex.Message);
            }
        }

        private async Task SendAttendeeNotificationsAsync(int eventId, string eventTitle, string reasonType, string reasonNote, int reconciliationId, List<int> customerIds)
        {
            try
            {
                using var notifConn = new NpgsqlConnection(_notifDbConnection);
                await notifConn.OpenAsync();

                string notifTitle = reasonType == "EVENT_CANCELLED"
                    ? $"Thông báo hủy sự kiện: {eventTitle}"
                    : $"Thông báo dời lịch sự kiện: {eventTitle}";
                string notifContent = $"Sự kiện '{eventTitle}' đã được xác nhận {(reasonType == "EVENT_CANCELLED" ? "hủy bỏ" : "hoãn lại")}. Lý do: {reasonNote}. Toàn bộ vé của quý khách cho sự kiện này đã được khóa và thu hồi (Mã đối soát #{reconciliationId}).";

                foreach (var uid in customerIds)
                {
                    using var notifCmd = new NpgsqlCommand(@"
                        INSERT INTO notifications 
                        (user_id, title, content, message, type, category, target_url, is_read, created_at, is_deleted)
                        VALUES (@uid, @title, @msg, @msg, 'event_disruption', 'ticket_revocation', '/my-tickets', false, NOW(), false);", notifConn);
                    notifCmd.Parameters.AddWithValue("uid", uid);
                    notifCmd.Parameters.AddWithValue("title", notifTitle);
                    notifCmd.Parameters.AddWithValue("msg", notifContent);
                    await notifCmd.ExecuteNonQueryAsync();
                }
                _logger.LogInformation("Sent disruption notifications to {Count} customers for event {EventId}", customerIds.Count, eventId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send notifications to notification_db for event {EventId}", eventId);
            }
        }

        // ==========================================
        // UC_8.8: View Ticket Revocation History
        // ==========================================
        public async Task<RevocationHistoryResponseDTO> GetRevocationHistoryAsync(int? eventId, string? search, string? reasonType, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? 10 : Math.Min(100, pageSize);

            var dbItems = new List<RevocationHistoryItemDTO>();
            int dbTotalCount = 0;

            try
            {
                using var conn = new NpgsqlConnection(_ticketDbConnection);
                await conn.OpenAsync();

                var sql = "SELECT * FROM ticket_revocations WHERE 1=1";
                if (eventId.HasValue && eventId.Value > 0) sql += " AND event_id = @eid";
                if (!string.IsNullOrWhiteSpace(reasonType) && reasonType != "ALL") sql += " AND reason_type = @rtype";
                if (!string.IsNullOrWhiteSpace(search)) sql += " AND (ticket_code ILIKE @s OR event_title ILIKE @s OR revoked_by_name ILIKE @s OR reason_note ILIKE @s)";

                var countSql = "SELECT COUNT(*) FROM (" + sql + ") AS sub";
                using (var countCmd = new NpgsqlCommand(countSql, conn))
                {
                    if (eventId.HasValue && eventId.Value > 0) countCmd.Parameters.AddWithValue("eid", eventId.Value);
                    if (!string.IsNullOrWhiteSpace(reasonType) && reasonType != "ALL") countCmd.Parameters.AddWithValue("rtype", reasonType);
                    if (!string.IsNullOrWhiteSpace(search)) countCmd.Parameters.AddWithValue("s", $"%{search}%");
                    dbTotalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                }

                sql += " ORDER BY revoked_at DESC LIMIT @limit OFFSET @offset";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    if (eventId.HasValue && eventId.Value > 0) cmd.Parameters.AddWithValue("eid", eventId.Value);
                    if (!string.IsNullOrWhiteSpace(reasonType) && reasonType != "ALL") cmd.Parameters.AddWithValue("rtype", reasonType);
                    if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("s", $"%{search}%");
                    cmd.Parameters.AddWithValue("limit", pageSize);
                    cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        dbItems.Add(new RevocationHistoryItemDTO
                        {
                            RevocationId = Convert.ToInt32(reader["revocation_id"]),
                            TicketCode = reader["ticket_code"].ToString() ?? "",
                            EventId = Convert.ToInt32(reader["event_id"]),
                            EventTitle = reader["event_title"].ToString() ?? "",
                            RevokedByAdminId = Convert.ToInt32(reader["revoked_by_admin_id"]),
                            RevokedByName = reader["revoked_by_name"].ToString() ?? "",
                            RevokedAt = Convert.ToDateTime(reader["revoked_at"]),
                            ReasonType = reader["reason_type"].ToString() ?? "",
                            ReasonNote = reader["reason_note"]?.ToString() ?? "",
                            ReconciliationId = reader["reconciliation_id"] != DBNull.Value ? Convert.ToInt32(reader["reconciliation_id"]) : null,
                            IsBulkRevocation = Convert.ToBoolean(reader["is_bulk"]),
                            RefundQueued = Convert.ToBoolean(reader["refund_queued"]),
                            NotificationSent = Convert.ToBoolean(reader["notification_sent"]),
                            SeatOrZone = reader["seat_or_zone"]?.ToString() ?? "",
                            CustomerName = reader["customer_name"]?.ToString() ?? ""
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load revocation history from DB. Using fallback.");
            }

            return new RevocationHistoryResponseDTO
            {
                Items = dbItems,
                TotalCount = dbTotalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = dbTotalCount > 0 ? (int)Math.Ceiling((double)dbTotalCount / pageSize) : 1
            };

            // Removed fallback mock data - strictly database only
            /* var query = _fallbackRevocations.AsQueryable();
            if (eventId.HasValue && eventId.Value > 0) query = query.Where(x => x.EventId == eventId.Value);
            if (!string.IsNullOrWhiteSpace(reasonType) && reasonType != "ALL") query = query.Where(x => x.ReasonType.Equals(reasonType, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(x => x.TicketCode.ToLowerInvariant().Contains(s)
                                      || x.EventTitle.ToLowerInvariant().Contains(s)
                                      || x.RevokedByName.ToLowerInvariant().Contains(s)
                                      || x.ReasonNote.ToLowerInvariant().Contains(s));
            }

            var total = query.Count();
            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new RevocationHistoryResponseDTO
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize)
            };
            */
        }

        // ==========================================
        // UC_8.10: View Reissue History (Traceability 1-1)
        // ==========================================
        public async Task<ReissueHistoryResponseDTO> GetReissueHistoryAsync(int? eventId, string? search, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? 10 : Math.Min(100, pageSize);

            var dbItems = new List<ReissueHistoryItemDTO>();
            int dbTotalCount = 0;

            try
            {
                using var conn = new NpgsqlConnection(_ticketDbConnection);
                await conn.OpenAsync();

                var sql = "SELECT * FROM ticket_reissues WHERE 1=1";
                if (eventId.HasValue && eventId.Value > 0) sql += " AND event_id = @eid";
                if (!string.IsNullOrWhiteSpace(search)) sql += " AND (old_ticket_code ILIKE @s OR new_ticket_code ILIKE @s OR customer_name ILIKE @s OR customer_email ILIKE @s OR reason ILIKE @s)";

                var countSql = "SELECT COUNT(*) FROM (" + sql + ") AS sub";
                using (var countCmd = new NpgsqlCommand(countSql, conn))
                {
                    if (eventId.HasValue && eventId.Value > 0) countCmd.Parameters.AddWithValue("eid", eventId.Value);
                    if (!string.IsNullOrWhiteSpace(search)) countCmd.Parameters.AddWithValue("s", $"%{search}%");
                    dbTotalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                }

                sql += " ORDER BY reissued_at DESC LIMIT @limit OFFSET @offset";
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    if (eventId.HasValue && eventId.Value > 0) cmd.Parameters.AddWithValue("eid", eventId.Value);
                    if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("s", $"%{search}%");
                    cmd.Parameters.AddWithValue("limit", pageSize);
                    cmd.Parameters.AddWithValue("offset", (page - 1) * pageSize);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        dbItems.Add(new ReissueHistoryItemDTO
                        {
                            ReissueId = Convert.ToInt32(reader["reissue_id"]),
                            OldTicketCode = reader["old_ticket_code"].ToString() ?? "",
                            NewTicketCode = reader["new_ticket_code"].ToString() ?? "",
                            EventId = Convert.ToInt32(reader["event_id"]),
                            EventTitle = reader["event_title"].ToString() ?? "",
                            ReissuedByAdminId = Convert.ToInt32(reader["reissued_by_admin_id"]),
                            ReissuedByName = reader["reissued_by_name"].ToString() ?? "",
                            ReissuedAt = Convert.ToDateTime(reader["reissued_at"]),
                            Reason = reader["reason"].ToString() ?? "",
                            CustomerName = reader["customer_name"].ToString() ?? "",
                            CustomerEmail = reader["customer_email"].ToString() ?? "",
                            SeatOrZone = reader["seat_or_zone"]?.ToString() ?? "",
                            DynamicQrStatus = reader["dynamic_qr_status"]?.ToString() ?? "ACTIVE"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load reissue history from DB. Using fallback.");
            }

            return new ReissueHistoryResponseDTO
            {
                Items = dbItems,
                TotalCount = dbTotalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = dbTotalCount > 0 ? (int)Math.Ceiling((double)dbTotalCount / pageSize) : 1
            };
        }

        public async Task<List<ReconciliationRecordDTO>> GetReconciliationRecordsAsync(int? eventId)
        {
            var results = new List<ReconciliationRecordDTO>();
            try
            {
                using var conn = new NpgsqlConnection(_ticketDbConnection);
                await conn.OpenAsync();

                var sql = "SELECT * FROM ticket_reconciliations WHERE 1=1";
                if (eventId.HasValue && eventId.Value > 0) sql += " AND event_id = @eid";
                sql += " ORDER BY created_at DESC LIMIT 50";

                using var cmd = new NpgsqlCommand(sql, conn);
                if (eventId.HasValue && eventId.Value > 0) cmd.Parameters.AddWithValue("eid", eventId.Value);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new ReconciliationRecordDTO
                    {
                        ReconciliationId = Convert.ToInt32(reader["reconciliation_id"]),
                        EventId = Convert.ToInt32(reader["event_id"]),
                        EventTitle = reader["event_title"].ToString() ?? "",
                        AdminId = Convert.ToInt32(reader["admin_id"]),
                        AdminName = reader["admin_name"].ToString() ?? "",
                        TotalTicketsRevoked = Convert.ToInt32(reader["total_tickets_revoked"]),
                        ReasonType = reader["reason_type"].ToString() ?? "",
                        Notes = reader["notes"]?.ToString() ?? "",
                        CreatedAt = Convert.ToDateTime(reader["created_at"]),
                        RefundStatus = reader["refund_status"].ToString() ?? "QUEUED"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch reconciliation records from DB.");
            }

            return results;
        }

        public async Task<GovernanceStatsDTO> GetGovernanceStatsAsync()
        {
            int dbBulkEvents = 0;
            int dbBulkTickets = 0;
            int dbSingleRevocations = 0;
            int dbReissues = 0;
            int dbRefundQueued = 0;
            bool dbSuccess = false;

            try
            {
                using var conn = new NpgsqlConnection(_ticketDbConnection);
                await conn.OpenAsync();

                using (var cmd = new NpgsqlCommand("SELECT COUNT(*), COALESCE(SUM(total_tickets_revoked), 0) FROM ticket_reconciliations;", conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        dbBulkEvents = Convert.ToInt32(reader[0]);
                        dbBulkTickets = Convert.ToInt32(reader[1]);
                    }
                }

                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM ticket_revocations WHERE is_bulk = false;", conn))
                {
                    dbSingleRevocations = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM ticket_reissues;", conn))
                {
                    dbReissues = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                using (var cmd = new NpgsqlCommand("SELECT COALESCE(SUM(total_tickets_revoked), 0) FROM ticket_reconciliations WHERE refund_status ILIKE '%queue%' OR refund_queue_status ILIKE '%queue%';", conn))
                {
                    dbRefundQueued = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                dbSuccess = (dbBulkEvents > 0 || dbBulkTickets > 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch governance stats from DB. Using fallback calculation.");
            }

            // Pure DB calculation: directly from PostgreSQL tables
            return new GovernanceStatsDTO
            {
                TotalRevokedTickets = dbBulkTickets + dbSingleRevocations,
                TotalBulkEvents = dbBulkEvents,
                TotalReissuedTickets = dbReissues,
                TotalRefundQueued = dbRefundQueued
            };
        }

        public async Task<List<EventSelectionDTO>> GetEventsForGovernanceAsync()
        {
            var list = new List<EventSelectionDTO>();
            try
            {
                using var conn = new NpgsqlConnection(_eventDbConnection);
                await conn.OpenAsync();

                // Bước 1: Chỉ nạp sự kiện có trạng thái là Cancelled (Đã hủy) hoặc Postponed (Hoãn/dời lịch)
                using var cmd = new NpgsqlCommand(@"
                    SELECT event_id, title, status, starts_at, 
                           COALESCE(location_name, address, 'Hội trường chính') AS location_name 
                    FROM events 
                    WHERE is_deleted = false 
                      AND (status ILIKE 'Cancelled' OR status ILIKE 'Postponed')
                    ORDER BY event_id DESC 
                    LIMIT 100;", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var status = Convert.ToString(reader["status"]) ?? "Cancelled";
                    var eid = Convert.ToInt32(reader["event_id"]);

                    list.Add(new EventSelectionDTO
                    {
                        EventId = eid,
                        Title = Convert.ToString(reader["title"]) ?? "",
                        Status = status,
                        StartDate = reader["starts_at"] != DBNull.Value ? Convert.ToDateTime(reader["starts_at"]) : DateTime.UtcNow.AddDays(7),
                        Location = Convert.ToString(reader["location_name"]) ?? "Trung tâm biểu diễn",
                        ActiveTicketsCount = 0
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch events from event_db: {Message}", ex.Message);
            }

            // Bước 1: Đếm số vé vẫn còn đang hoạt động (Active / Available / Issued)
            try
            {
                using var tConn = new NpgsqlConnection(_ticketDbConnection);
                await tConn.OpenAsync();
                using var tCmd = new NpgsqlCommand(@"
                    SELECT event_id, COUNT(*) 
                    FROM tickets 
                    WHERE is_deleted = false 
                      AND (status IS NULL OR (status != 'Revoked' AND status != 'REVOKED' AND status != 'Refunded')) 
                    GROUP BY event_id;", tConn);
                using var rdr = await tCmd.ExecuteReaderAsync();
                var countMap = new Dictionary<int, int>();
                while (await rdr.ReadAsync())
                {
                    countMap[Convert.ToInt32(rdr[0])] = Convert.ToInt32(rdr[1]);
                }

                foreach (var ev in list)
                {
                    if (countMap.TryGetValue(ev.EventId, out int cnt))
                    {
                        ev.ActiveTicketsCount = cnt;
                    }
                    else
                    {
                        ev.ActiveTicketsCount = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not count tickets from ticket_db: {Message}", ex.Message);
            }

            // Bước 1: Chỉ nạp sự kiện có trạng thái là Cancelled hoặc Postponed VÀ VẪN CÒN VÉ ĐANG HOẠT ĐỘNG
            return list.Where(e => e.ActiveTicketsCount > 0).ToList();
        }

        // ==========================================
        // UC_14.5: Offline Ticket Package Preload
        // ==========================================
        public async Task<OfflinePackageResponseDTO> GetOfflinePackageAsync(int eventId)
        {
            var eventInfo = await GetEventInfoAsync(eventId);
            var tickets = new List<OfflineTicketItemDTO>();

            try
            {
                using var conn = new NpgsqlConnection(_ticketDbConnection);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
                    SELECT t.ticket_code, t.ticket_id, t.ticket_type_name, t.owner_name, t.is_checked_in, t.status, t.gate_id,
                           (SELECT qr.qr_token FROM ticket_qr_tokens qr WHERE qr.ticket_id = t.ticket_id AND qr.is_revoked = false ORDER BY qr.issued_at DESC LIMIT 1) AS qr_token
                    FROM tickets t 
                    WHERE t.event_id = @eid AND t.is_deleted = false;", conn);
                cmd.Parameters.AddWithValue("eid", eventId);

                using var reader = await cmd.ExecuteReaderAsync();
                int idx = 1;
                while (await reader.ReadAsync())
                {
                    string code = reader["ticket_code"]?.ToString() ?? $"TKT-EV{eventId}-{idx}";
                    string? qrTok = reader["qr_token"]?.ToString();
                    bool isChecked = Convert.ToBoolean(reader["is_checked_in"]);
                    string status = reader["status"]?.ToString() ?? (isChecked ? "Used" : "Active");
                    string zone = reader["ticket_type_name"]?.ToString() ?? "General Admission";
                    string owner = reader["owner_name"]?.ToString() ?? $"Attendee #{idx}";
                    string gate = reader["gate_id"]?.ToString() ?? "GATE_A";

                    tickets.Add(new OfflineTicketItemDTO
                    {
                        TicketCode = code,
                        QrToken = qrTok,
                        TicketTokenHash = ComputeSha256(code + "_CONCERT_SHIELD_SECRET"),
                        AttendeeName = owner,
                        ZoneName = zone,
                        SeatNumber = $"Seat-{idx++}",
                        AssignedGate = gate,
                        IsCheckedIn = isChecked,
                        Status = status
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch tickets for offline package from DB.");
            }

            return new OfflinePackageResponseDTO
            {
                EventId = eventId,
                EventTitle = eventInfo.Title,
                ExportedAt = DateTime.UtcNow,
                TotalTickets = tickets.Count,
                Signature = ComputeSha256($"EVENT_{eventId}_{tickets.Count}_{DateTime.UtcNow:yyyyMMdd}"),
                Tickets = tickets
            };
        }

        // ==========================================
        // UC_14.6: Sync Offline Check-in Records
        // ==========================================
        public async Task<OfflineSyncResponseDTO> SyncOfflineScansAsync(OfflineSyncRequestDTO request, int staffUserId)
        {
            if (request == null || request.Scans == null || request.Scans.Count == 0)
            {
                return new OfflineSyncResponseDTO
                {
                    Success = true,
                    TotalProcessed = 0,
                    NewlyCheckedIn = 0,
                    AlreadyUsedCount = 0,
                    SyncedAt = DateTime.UtcNow,
                    Message = "No offline scan records provided for synchronization."
                };
            }

            int newlyCheckedIn = 0;
            int alreadyUsedCount = 0;
            var conflictsList = new List<OfflineSyncConflictDTO>();

            try
            {
                using var conn = new NpgsqlConnection(_ticketDbConnection);
                await conn.OpenAsync();

                foreach (var scan in request.Scans)
                {
                    if (string.IsNullOrWhiteSpace(scan.TicketCode)) continue;
                    string code = scan.TicketCode.Trim();

                    // Check current ticket status
                    bool alreadyUsed = false;
                    string attendeeName = "Attendee";
                    string zoneName = "General Admission";
                    DateTime? prevScannedAt = null;

                    using (var checkCmd = new NpgsqlCommand("SELECT is_checked_in, status, owner_name, ticket_type_name, checked_in_at FROM tickets WHERE (ticket_code::text = @code OR ticket_id::text = @code) AND event_id = @eid;", conn))
                    {
                        checkCmd.Parameters.AddWithValue("code", code);
                        checkCmd.Parameters.AddWithValue("eid", request.EventId);
                        using var reader = await checkCmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            var isChecked = Convert.ToBoolean(reader["is_checked_in"]);
                            var status = reader["status"]?.ToString();
                            attendeeName = reader["owner_name"]?.ToString() ?? "Attendee";
                            zoneName = reader["ticket_type_name"]?.ToString() ?? "General Admission";
                            if (reader["checked_in_at"] != DBNull.Value) prevScannedAt = Convert.ToDateTime(reader["checked_in_at"]);

                            if (isChecked || status == "Used" || status == "REVOKED")
                            {
                                alreadyUsed = true;
                            }
                        }
                    }

                    if (alreadyUsed)
                    {
                        alreadyUsedCount++;
                        var conflictId = $"CONF-{DateTime.UtcNow.Ticks % 100000}-{alreadyUsedCount}";
                        var earlierTime = prevScannedAt ?? scan.ScannedAt.AddMinutes(-12);
                        var gateId = scan.GateId ?? request.GateId ?? "GATE_A";
                        var gateName = gateId == "GATE_B" ? "Gate B - North Wing" : "Gate A - Main Entrance";

                        var conflict = new OfflineSyncConflictDTO
                        {
                            ConflictId = conflictId,
                            EventId = request.EventId,
                            TicketCode = code,
                            AttendeeName = attendeeName,
                            ZoneName = zoneName,
                            SeatNumber = "Standard Area",
                            EarlierScannedAt = earlierTime,
                            EarlierGateId = "GATE_A",
                            EarlierGateName = "Gate A - Main Entrance",
                            EarlierStaffName = "Staff Online System",
                            EarlierMethod = "Online Dynamic QR",
                            DuplicateScannedAt = scan.ScannedAt,
                            DuplicateGateId = gateId,
                            DuplicateGateName = gateName,
                            DuplicateStaffName = $"Staff #{scan.StaffUserId}",
                            DuplicateMethod = scan.ScanMethod ?? "Offline QR Scan",
                            Status = "PENDING_REVIEW"
                        };

                        // Insert into offline_sync_conflicts table in database
                        using (var insConf = new NpgsqlCommand(@"
                            INSERT INTO offline_sync_conflicts 
                            (conflict_id, event_id, ticket_code, attendee_name, zone_name, seat_number, earlier_scanned_at, earlier_gate_id, earlier_gate_name, earlier_staff_name, earlier_method, duplicate_scanned_at, duplicate_gate_id, duplicate_gate_name, duplicate_staff_name, duplicate_method, status)
                            VALUES (@cid, @eid, @code, @name, @zone, @seat, @earlierTime, @earlierGate, @earlierGateName, @earlierStaff, @earlierMethod, @dupTime, @dupGate, @dupGateName, @dupStaff, @dupMethod, 'PENDING_REVIEW')
                            ON CONFLICT (conflict_id) DO NOTHING;", conn))
                        {
                            insConf.Parameters.AddWithValue("cid", conflictId);
                            insConf.Parameters.AddWithValue("eid", request.EventId);
                            insConf.Parameters.AddWithValue("code", code);
                            insConf.Parameters.AddWithValue("name", attendeeName);
                            insConf.Parameters.AddWithValue("zone", zoneName);
                            insConf.Parameters.AddWithValue("seat", "Standard Area");
                            insConf.Parameters.AddWithValue("earlierTime", earlierTime);
                            insConf.Parameters.AddWithValue("earlierGate", "GATE_A");
                            insConf.Parameters.AddWithValue("earlierGateName", "Gate A - Main Entrance");
                            insConf.Parameters.AddWithValue("earlierStaff", "Staff Online System");
                            insConf.Parameters.AddWithValue("earlierMethod", "Online Dynamic QR");
                            insConf.Parameters.AddWithValue("dupTime", scan.ScannedAt);
                            insConf.Parameters.AddWithValue("dupGate", gateId);
                            insConf.Parameters.AddWithValue("dupGateName", gateName);
                            insConf.Parameters.AddWithValue("dupStaff", $"Staff #{scan.StaffUserId}");
                            insConf.Parameters.AddWithValue("dupMethod", scan.ScanMethod ?? "Offline QR Scan");
                            await insConf.ExecuteNonQueryAsync();
                        }

                        conflictsList.Add(conflict);
                    }
                    else
                    {
                        // Update to Checked In with actual offline scan timestamp
                        using (var updCmd = new NpgsqlCommand(@"
                            UPDATE tickets 
                            SET is_checked_in = true, status = 'Used', checked_in_at = @scanned_at, checked_in_by = @staff, gate_id = @gate
                            WHERE (ticket_code::text = @code OR ticket_id::text = @code) AND event_id = @eid;", conn))
                        {
                            updCmd.Parameters.AddWithValue("code", code);
                            updCmd.Parameters.AddWithValue("eid", request.EventId);
                            updCmd.Parameters.AddWithValue("scanned_at", scan.ScannedAt);
                            updCmd.Parameters.AddWithValue("staff", staffUserId > 0 ? staffUserId : scan.StaffUserId);
                            updCmd.Parameters.AddWithValue("gate", scan.GateId ?? request.GateId ?? "GATE_A");

                            int rows = await updCmd.ExecuteNonQueryAsync();
                            if (rows > 0) newlyCheckedIn++;
                            else alreadyUsedCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database error during SyncOfflineScansAsync for event {EventId}", request.EventId);
            }

            return new OfflineSyncResponseDTO
            {
                Success = true,
                TotalProcessed = request.Scans.Count,
                NewlyCheckedIn = newlyCheckedIn,
                AlreadyUsedCount = alreadyUsedCount,
                ConflictCount = conflictsList.Count,
                Conflicts = conflictsList,
                SyncedAt = DateTime.UtcNow,
                Message = conflictsList.Count > 0
                    ? $"Successfully synced {request.Scans.Count} offline scan record(s) ({newlyCheckedIn} newly admitted, {conflictsList.Count} duplicate scan conflicts detected for review)."
                    : $"Successfully synced {request.Scans.Count} offline scan record(s) ({newlyCheckedIn} newly admitted, {alreadyUsedCount} previously verified)."
            };
        }

        private async Task<(bool Exists, int OrganizerId, string Title)> GetEventInfoAsync(int eventId)
        {
            try
            {
                using var conn = new NpgsqlConnection(_eventDbConnection);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT organizer_id, title FROM events WHERE event_id = @eid AND is_deleted = false;", conn);
                cmd.Parameters.AddWithValue("eid", eventId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (true, Convert.ToInt32(reader["organizer_id"]), Convert.ToString(reader["title"]) ?? $"Concert #{eventId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch event info from event_db for Event {EventId}", eventId);
            }

            return (true, 1, $"Concert #{eventId}");
        }

        public async Task<List<OfflineSyncConflictDTO>> GetOfflineSyncConflictsAsync(int? eventId, string? status)
        {
            var results = new List<OfflineSyncConflictDTO>();
            try
            {
                using var conn = new NpgsqlConnection(_ticketDbConnection);
                await conn.OpenAsync();

                var sql = "SELECT * FROM offline_sync_conflicts WHERE 1=1";
                if (eventId.HasValue && eventId.Value > 0) sql += " AND event_id = @eid";
                if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase)) sql += " AND status ILIKE @st";
                sql += " ORDER BY duplicate_scanned_at DESC;";

                using var cmd = new NpgsqlCommand(sql, conn);
                if (eventId.HasValue && eventId.Value > 0) cmd.Parameters.AddWithValue("eid", eventId.Value);
                if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase)) cmd.Parameters.AddWithValue("st", status);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new OfflineSyncConflictDTO
                    {
                        ConflictId = reader["conflict_id"]?.ToString() ?? "",
                        EventId = Convert.ToInt32(reader["event_id"]),
                        TicketCode = reader["ticket_code"]?.ToString() ?? "",
                        AttendeeName = reader["attendee_name"]?.ToString() ?? "Attendee",
                        ZoneName = reader["zone_name"]?.ToString() ?? "General",
                        SeatNumber = reader["seat_number"]?.ToString() ?? "Standard Area",
                        EarlierScannedAt = Convert.ToDateTime(reader["earlier_scanned_at"]),
                        EarlierGateId = reader["earlier_gate_id"]?.ToString() ?? "GATE_A",
                        EarlierGateName = reader["earlier_gate_name"]?.ToString() ?? "Gate A - Main Entrance",
                        EarlierStaffName = reader["earlier_staff_name"]?.ToString() ?? "Staff Online System",
                        EarlierMethod = reader["earlier_method"]?.ToString() ?? "Online Dynamic QR",
                        DuplicateScannedAt = Convert.ToDateTime(reader["duplicate_scanned_at"]),
                        DuplicateGateId = reader["duplicate_gate_id"]?.ToString() ?? "GATE_A",
                        DuplicateGateName = reader["duplicate_gate_name"]?.ToString() ?? "Gate A - Main Entrance",
                        DuplicateStaffName = reader["duplicate_staff_name"]?.ToString() ?? "Staff",
                        DuplicateMethod = reader["duplicate_method"]?.ToString() ?? "Offline QR Scan",
                        Status = reader["status"]?.ToString() ?? "PENDING_REVIEW",
                        ResolutionNotes = reader["resolution_notes"]?.ToString(),
                        ResolvedByName = reader["resolved_by_name"]?.ToString(),
                        ResolvedAt = reader["resolved_at"] != DBNull.Value ? Convert.ToDateTime(reader["resolved_at"]) : null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading offline sync conflicts from DB");
            }

            return results;
        }

        public async Task<OfflineSyncConflictDTO> ResolveOfflineConflictAsync(string conflictId, ResolveConflictRequestDTO request, int userId, string userName)
        {
            string newStatus = request.Decision switch
            {
                "ACCEPT_EARLIER_REJECT_LATER" => "RESOLVED_EARLIER_VALID",
                "ESCALATE_SECURITY" => "RESOLVED_SECURITY_ESCALATED",
                "REVOKE_BOTH" => "RESOLVED_REVOKED_BOTH",
                _ => "RESOLVED"
            };

            try
            {
                using var conn = new NpgsqlConnection(_ticketDbConnection);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
                    UPDATE offline_sync_conflicts 
                    SET status = @st, resolution_action = @act, resolution_notes = @notes, 
                        resolved_by_user_id = @uid, resolved_by_name = @uname, resolved_at = NOW()
                    WHERE conflict_id = @cid;", conn);
                cmd.Parameters.AddWithValue("st", newStatus);
                cmd.Parameters.AddWithValue("act", request.Decision ?? "RESOLVED");
                cmd.Parameters.AddWithValue("notes", request.Notes ?? string.Empty);
                cmd.Parameters.AddWithValue("uid", userId);
                cmd.Parameters.AddWithValue("uname", userName);
                cmd.Parameters.AddWithValue("cid", conflictId);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating offline sync conflict in DB for {ConflictId}", conflictId);
            }

            _logger.LogInformation("Conflict {ConflictId} resolved with decision {Decision} by {User}", conflictId, request.Decision, userName);

            return new OfflineSyncConflictDTO
            {
                ConflictId = conflictId,
                Status = newStatus,
                ResolutionNotes = request.Notes,
                ResolvedByName = userName,
                ResolvedAt = DateTime.UtcNow
            };
        }
    }
}
