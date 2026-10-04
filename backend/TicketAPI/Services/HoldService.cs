using System.Text.Json;
using BookingAPI.DTOS;
using StackExchange.Redis;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Models;

namespace TicketAPI.Services
{
    public class HoldService : IHoldService
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            };

        private readonly IDatabase _db;
        private readonly IEventApiClient _eventApiClient;
        private readonly ILogger<HoldService> _logger;

        public HoldService(
            IConnectionMultiplexer redis,
            ILogger<HoldService> logger,
            IEventApiClient eventApiClient)
        {
            _db = redis.GetDatabase();
            _logger = logger;
            _eventApiClient = eventApiClient;
        }

        public async Task<bool> TryHoldSeatAsync(
            int eventId,
            int seatId,
            int userId,
            int durationSeconds = 600)
        {
            ValidatePositive(eventId, nameof(eventId));
            ValidatePositive(seatId, nameof(seatId));
            ValidatePositive(userId, nameof(userId));
            ValidateDuration(durationSeconds);

            return await _db.StringSetAsync(
                SeatKey(eventId, seatId),
                userId,
                TimeSpan.FromSeconds(durationSeconds),
                When.NotExists);
        }

        public async Task<bool> TryHoldTicketsWithCapacityAsync(
            int ticketTypeId,
            int userId,
            int quantity,
            int availableCapacity,
            int durationSeconds)
        {
            ValidatePositive(ticketTypeId, nameof(ticketTypeId));
            ValidatePositive(userId, nameof(userId));
            ValidatePositive(quantity, nameof(quantity));
            ValidateDuration(durationSeconds);

            if (availableCapacity < quantity)
                return false;

            var userKey = TicketUserKey(ticketTypeId, userId);
            var totalKey = TicketTotalKey(ticketTypeId);
            var activeKey = TicketActiveKey(ticketTypeId);
            var quantitiesKey = TicketQuantitiesKey(ticketTypeId);

            const string script = """
                local now = tonumber(ARGV[1])
                local userId = ARGV[2]
                local quantity = tonumber(ARGV[3])
                local capacity = tonumber(ARGV[4])
                local duration = tonumber(ARGV[5])
                local userKeyPrefix = ARGV[6]

                local expiredUsers =
                    redis.call('ZRANGEBYSCORE', KEYS[3], '-inf', now)

                for _, expiredUserId in ipairs(expiredUsers) do
                    redis.call('DEL', userKeyPrefix .. expiredUserId)
                    redis.call('HDEL', KEYS[4], expiredUserId)
                    redis.call('ZREM', KEYS[3], expiredUserId)
                end

                if redis.call('ZSCORE', KEYS[3], userId) then
                    return 0
                end

                if redis.call('EXISTS', userKeyPrefix .. userId) == 1 then
                    return 0
                end

                local quantities = redis.call('HVALS', KEYS[4])
                local total = 0

                for _, heldQuantity in ipairs(quantities) do
                    total = total + tonumber(heldQuantity)
                end

                if total + quantity > capacity then
                    redis.call('SET', KEYS[2], total)
                    return 0
                end

                local expiresAt = now + duration

                redis.call(
                    'SET',
                    userKeyPrefix .. userId,
                    quantity,
                    'EX',
                    duration
                )

                redis.call('HSET', KEYS[4], userId, quantity)
                redis.call('ZADD', KEYS[3], expiresAt, userId)
                redis.call('SET', KEYS[2], total + quantity, 'EX', duration + 60)

                redis.call('EXPIRE', KEYS[3], duration + 60)
                redis.call('EXPIRE', KEYS[4], duration + 60)

                return 1
                """;

            var result = (long)await _db.ScriptEvaluateAsync(
                script,
                new RedisKey[]
                {
                    userKey,
                    totalKey,
                    activeKey,
                    quantitiesKey
                },
                new RedisValue[]
                {
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    userId,
                    quantity,
                    availableCapacity,
                    durationSeconds,
                    $"hold:ticket:{ticketTypeId}:user:"
                });

            return result == 1;
        }

        public async Task<bool> ReleaseTicketsAsync(
            int ticketTypeId,
            int userId,
            int quantity)
        {
            var userKey = TicketUserKey(ticketTypeId, userId);
            var totalKey = TicketTotalKey(ticketTypeId);
            var activeKey = TicketActiveKey(ticketTypeId);
            var quantitiesKey = TicketQuantitiesKey(ticketTypeId);

            const string script = """
                local held = redis.call('GET', KEYS[1])

                if not held then
                    redis.call('HDEL', KEYS[4], ARGV[1])
                    redis.call('ZREM', KEYS[3], ARGV[1])
                    return 0
                end

                redis.call('DEL', KEYS[1])
                redis.call('HDEL', KEYS[4], ARGV[1])
                redis.call('ZREM', KEYS[3], ARGV[1])

                local quantities = redis.call('HVALS', KEYS[4])
                local total = 0

                for _, heldQuantity in ipairs(quantities) do
                    total = total + tonumber(heldQuantity)
                end

                if total <= 0 then
                    redis.call('DEL', KEYS[2])
                    return 1
                end

                local ttl = redis.call('TTL', KEYS[3])

                if ttl > 0 then
                    redis.call('SET', KEYS[2], total, 'EX', ttl)
                else
                    redis.call('SET', KEYS[2], total, 'EX', 60)
                end

                return 1
                """;

            var result = (long)await _db.ScriptEvaluateAsync(
                script,
                new RedisKey[]
                {
                    userKey,
                    totalKey,
                    activeKey,
                    quantitiesKey
                },
                new RedisValue[] { userId });

            return result == 1;
        }

        public async Task<SeatHoldInfo?> GetHoldInfoAsync(int eventId, int seatId)
        {
            var value = await _db.StringGetAsync(SeatKey(eventId, seatId));

            if (value.IsNull)
                return null;

            return new SeatHoldInfo
            {
                UserId = (int)value
            };
        }

        public async Task<TicketHoldInfo?> GetTicketHoldInfoAsync(
            int ticketTypeId,
            int userId)
        {
            var value = await _db.StringGetAsync(
                TicketUserKey(ticketTypeId, userId));

            if (value.IsNull)
                return null;

            return new TicketHoldInfo
            {
                Quantity = (int)value
            };
        }

        public async Task<HoldSessionResponse> CreateHoldSessionAsync(
            int userId,
            CreateHoldSessionRequest request,
            int durationSeconds = 600)
        {
            ValidatePositive(userId, nameof(userId));
            ValidateDuration(durationSeconds);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            ValidatePositive(request.EventId, nameof(request.EventId));

            var tickets = request.Tickets ?? new List<TicketHoldItem>();
            var seatIds = request.SeatIds ?? new List<int>();

            if (tickets.Count == 0 && seatIds.Count == 0)
                throw new ArgumentException("Bạn chưa chọn vé hoặc ghế.");

            if (tickets.Any(ticket =>
                    ticket.TicketTypeId <= 0 || ticket.Quantity <= 0))
            {
                throw new ArgumentException(
                    "TicketTypeId và Quantity phải lớn hơn 0.");
            }

            if (tickets.GroupBy(ticket => ticket.TicketTypeId)
                       .Any(group => group.Count() > 1))
            {
                throw new ArgumentException(
                    "Một loại vé chỉ được xuất hiện một lần trong request.");
            }

            if (seatIds.Any(seatId => seatId <= 0) ||
                seatIds.Distinct().Count() != seatIds.Count)
            {
                throw new ArgumentException(
                    "Danh sách SeatIds không hợp lệ hoặc có ghế bị lặp.");
            }

            var eventInfo =
                await _eventApiClient.GetEventByIdAsync(request.EventId);

            if (eventInfo?.TicketTypes == null)
            {
                throw new InvalidOperationException(
                    $"Không lấy được thông tin vé của sự kiện {request.EventId}.");
            }

            var ticketCapacities = new Dictionary<int, int>();

            foreach (var ticket in tickets)
            {
                var ticketInfo = eventInfo.TicketTypes.FirstOrDefault(
                    item => item.TicketTypeId == ticket.TicketTypeId);

                if (ticketInfo == null)
                {
                    throw new ArgumentException(
                        $"Loại vé {ticket.TicketTypeId} không thuộc sự kiện này.");
                }

                ticketCapacities[ticket.TicketTypeId] =
                    Math.Max(0, ticketInfo.Quantity - ticketInfo.SoldQuantity);
            }

            var holdId = Guid.NewGuid().ToString("N");
            var userLockKey = $"hold:user:{userId}:active";

            var acquired = await _db.StringSetAsync(
                userLockKey,
                holdId,
                TimeSpan.FromSeconds(durationSeconds),
                When.NotExists);

            if (!acquired)
            {
                throw new InvalidOperationException(
                    "Bạn đang có phiên giữ vé. Vui lòng tiếp tục thanh toán hoặc chờ phiên hết hạn.");
            }

            var heldTickets = new List<TicketHoldItem>();
            var heldSeats = new List<int>();
            string? sessionKey = null;

            try
            {
                foreach (var ticket in tickets)
                {
                    var held = await TryHoldTicketsWithCapacityAsync(
                        ticket.TicketTypeId,
                        userId,
                        ticket.Quantity,
                        ticketCapacities[ticket.TicketTypeId],
                        durationSeconds);

                    if (!held)
                    {
                        throw new InvalidOperationException(
                            $"Không giữ được vé loại {ticket.TicketTypeId}. " +
                            "Vé có thể đã hết hoặc bạn đang giữ loại vé này.");
                    }

                    heldTickets.Add(ticket);
                }

                foreach (var seatId in seatIds)
                {
                    var held = await TryHoldSeatAsync(
                        request.EventId,
                        seatId,
                        userId,
                        durationSeconds);

                    if (!held)
                    {
                        throw new InvalidOperationException(
                            $"Ghế {seatId} vừa được người khác chọn.");
                    }

                    heldSeats.Add(seatId);
                }

                var session = new HoldSession
                {
                    HoldId = holdId,
                    UserId = userId,
                    EventId = request.EventId,
                    ExpiresAtUtc = DateTime.UtcNow.AddSeconds(durationSeconds),
                    Tickets = tickets,
                    SeatIds = seatIds
                };

                sessionKey = SessionKey(session.HoldId);

                var saved = await _db.StringSetAsync(
                    sessionKey,
                    JsonSerializer.Serialize(session, JsonOptions),
                    TimeSpan.FromSeconds(durationSeconds),
                    When.NotExists);

                if (!saved)
                    throw new InvalidOperationException(
                        "Không lưu được hold session.");

                _logger.LogInformation(
                    "Tạo hold session {HoldId} cho user {UserId}, event {EventId}",
                    session.HoldId,
                    userId,
                    request.EventId);

                return ToResponse(session);
            }
            catch
            {
                if (sessionKey != null)
                    await _db.KeyDeleteAsync(sessionKey);

                foreach (var ticket in heldTickets)
                {
                    await ReleaseTicketsAsync(
                        ticket.TicketTypeId,
                        userId,
                        ticket.Quantity);
                }

                foreach (var seatId in heldSeats)
                {
                    await ReleaseSeatAsync(
                        request.EventId,
                        seatId,
                        userId);
                }

                await ReleaseUserHoldLockAsync(userId, holdId);

                throw;
            }
        }

        private async Task ReleaseUserHoldLockAsync(int userId, string holdId)
        {
            const string script = """
            if redis.call('GET', KEYS[1]) == ARGV[1] then
                return redis.call('DEL', KEYS[1])
            end
            return 0
            """;

            await _db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { $"hold:user:{userId}:active" },
                new RedisValue[] { holdId });
        }

        public async Task<HoldSessionResponse> GetHoldDetailsAsync(
            string holdId,
            int userId)
        {
            if (string.IsNullOrWhiteSpace(holdId))
                throw new ArgumentException("HoldId không hợp lệ.");

            ValidatePositive(userId, nameof(userId));

            var key = SessionKey(holdId);
            var json = await _db.StringGetAsync(key);

            if (json.IsNull)
            {
                throw new KeyNotFoundException(
                    "Phiên giữ vé không tồn tại hoặc đã hết hạn.");
            }

            var session = JsonSerializer.Deserialize<HoldSession>(
                json.ToString(),
                JsonOptions);

            if (session == null)
                throw new InvalidOperationException(
                    "Dữ liệu hold session không hợp lệ.");

            if (session.UserId != userId)
                throw new UnauthorizedAccessException(
                    "Bạn không có quyền xem phiên giữ vé này.");

            var remainingSeconds = (int)Math.Ceiling(
                (session.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds);

            if (remainingSeconds <= 0)
            {
                await _db.KeyDeleteAsync(key);

                throw new KeyNotFoundException(
                    "Phiên giữ vé đã hết hạn.");
            }

            return ToResponse(session, remainingSeconds);
        }

        public async Task<bool> ReleaseSeatAsync(
            int eventId,
            int seatId,
            int userId)
        {
            var key = SeatKey(eventId, seatId);

            const string script = """
                local owner = redis.call('GET', KEYS[1])

                if owner and owner == ARGV[1] then
                    return redis.call('DEL', KEYS[1])
                end

                return 0
                """;

            var result = (long)await _db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { key },
                new RedisValue[] { userId });

            return result == 1;
        }

        private static HoldSessionResponse ToResponse(
            HoldSession session,
            int? remainingSeconds = null)
        {
            var seconds = remainingSeconds ??
                (int)Math.Ceiling(
                    (session.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds);

            return new HoldSessionResponse
            {
                HoldId = session.HoldId,
                EventId = session.EventId,
                ExpiresAtUtc = session.ExpiresAtUtc,
                RemainingSeconds = Math.Max(0, seconds),
                Tickets = session.Tickets,
                SeatIds = session.SeatIds,
                Attendees = session.Attendees
            };
        }

        private static string SeatKey(int eventId, int seatId) =>
            $"hold:seat:{eventId}:{seatId}";

        private static string TicketUserKey(int ticketTypeId, int userId) =>
            $"hold:ticket:{ticketTypeId}:user:{userId}";

        private static string TicketTotalKey(int ticketTypeId) =>
            $"hold:ticket:{ticketTypeId}:total";

        private static string TicketActiveKey(int ticketTypeId) =>
            $"hold:ticket:{ticketTypeId}:active";

        private static string TicketQuantitiesKey(int ticketTypeId) =>
            $"hold:ticket:{ticketTypeId}:quantities";

        private static string SessionKey(string holdId) =>
            $"hold:session:{holdId}";

        private static void ValidatePositive(int value, string name)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    "Giá trị phải lớn hơn 0.");
            }
        }

        private static void ValidateDuration(int durationSeconds)
        {
            if (durationSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    "Thời hạn giữ vé phải lớn hơn 0.");
            }
        }

        public async Task<List<int>> GetHeldSeatIdsAsync(
            int eventId,
            IReadOnlyCollection<int> seatIds)
        {
            var ids = seatIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            var checks = ids.Select(async seatId =>
            {
                var value = await _db.StringGetAsync(SeatKey(eventId, seatId));
                return (SeatId: seatId, IsHeld: !value.IsNull);
            });

            var results = await Task.WhenAll(checks);

            return results
                .Where(result => result.IsHeld)
                .Select(result => result.SeatId)
                .ToList();
        }

        public async Task<HoldSessionResponse> SaveAttendeesAsync(string holdId, int userId, List<HoldAttendeeDto> attendees)
        {
            if (string.IsNullOrWhiteSpace(holdId))
                throw new ArgumentException("HoldId không hợp lệ.");

            ValidatePositive(userId, nameof(userId));

            if (attendees == null || attendees.Count == 0)
                throw new ArgumentException("Danh sách attendee không được để trống.");

            var key = SessionKey(holdId);
            var json = await _db.StringGetAsync(key);

            if (json.IsNull)
                throw new KeyNotFoundException(
                    "Phiên giữ vé không tồn tại hoặc đã hết hạn.");

            var session = JsonSerializer.Deserialize<HoldSession>(
                json.ToString(),
                JsonOptions);

            if (session == null)
                throw new InvalidOperationException(
                    "Dữ liệu hold session không hợp lệ.");

            if (session.UserId != userId)
                throw new UnauthorizedAccessException(
                    "Bạn không có quyền cập nhật phiên giữ vé này.");

            var remainingSeconds = (int)Math.Ceiling(
                (session.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds);

            var redisTtl = await _db.KeyTimeToLiveAsync(key);

            if (remainingSeconds <= 0 || redisTtl == null || redisTtl <= TimeSpan.Zero)
                throw new KeyNotFoundException("Phiên giữ vé đã hết hạn.");

            var expectedCount = session.SeatIds.Count > 0
                ? session.SeatIds.Count
                : session.Tickets.Sum(ticket => ticket.Quantity);

            if (attendees.Count != expectedCount)
            {
                throw new ArgumentException(
                    $"Cần nhập thông tin cho {expectedCount} attendee.");
            }

            session.Attendees = attendees;

            var ttlToKeep = redisTtl.Value < TimeSpan.FromSeconds(remainingSeconds)
                ? redisTtl.Value
                : TimeSpan.FromSeconds(remainingSeconds);

            var saved = await _db.StringSetAsync(
                key,
                JsonSerializer.Serialize(session, JsonOptions),
                ttlToKeep,
                When.Exists);

            if (!saved)
                throw new KeyNotFoundException("Phiên giữ vé đã hết hạn.");

            return ToResponse(session, remainingSeconds);
        }
    }
}