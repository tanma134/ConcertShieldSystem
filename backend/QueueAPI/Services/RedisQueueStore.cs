using System.Globalization;
using StackExchange.Redis;
using QueueAPI.Time;

namespace QueueAPI.Services;

public sealed class RedisQueueStore(IConfiguration configuration) : IRedisQueueStore
{
    private readonly Lazy<ConnectionMultiplexer> _connection = new(() =>
        ConnectionMultiplexer.Connect(configuration["Redis:ConnectionString"] ?? "localhost:6379,abortConnect=false"));

    private IDatabase Db => _connection.Value.GetDatabase();
    private static string Root(int eventId) => $"queue:event:{eventId}";
    private static string Waiting(int eventId) => $"{Root(eventId)}:waiting";
    private static string Order(int eventId) => $"{Root(eventId)}:order";
    private static string Sessions(int eventId, int userId) => $"{Root(eventId)}:session:{userId}";
    private static string Joined(int eventId, int userId) => $"{Root(eventId)}:joined:{userId}";
    private static string Token(int eventId, string token) => $"{Root(eventId)}:token:{token}";
    private static string Admission(int eventId, int userId) => $"{Root(eventId)}:admission:{userId}";
    private static string AdmissionWindow(int eventId) => $"{Root(eventId)}:admission-window";
    private static string Registrations(int eventId, string cycleKey) => $"{Root(eventId)}:registrations:{cycleKey}";
    private static string Randomization(int eventId, string cycleKey) => $"{Root(eventId)}:randomized:{cycleKey}";

    public async Task<bool> RegisterAsync(int eventId, int userId, string token, string cycleKey, CancellationToken cancellationToken)
    {
        await Db.KeyDeleteAsync(Joined(eventId, userId));
        var sessionKey = Sessions(eventId, userId);
        var userValue = userId.ToString(CultureInfo.InvariantCulture);
        const string script = """
            local current = redis.call('GET', KEYS[1])
            if current and current ~= ARGV[1] then return 0 end
            if not current then redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[2]) end
            redis.call('SET', KEYS[2], ARGV[3], 'EX', ARGV[2])
            redis.call('SADD', KEYS[3], ARGV[3])
            redis.call('EXPIRE', KEYS[3], ARGV[2])
            return 1
            """;
        var result = await Db.ScriptEvaluateAsync(
            script,
            new RedisKey[] { sessionKey, Token(eventId, token), Registrations(eventId, cycleKey) },
            new RedisValue[] { token, (int)TimeSpan.FromDays(1).TotalSeconds, userValue });
        return (int)result == 1;
    }

    public async Task ResetUserAsync(int eventId, int userId, string cycleKey, CancellationToken cancellationToken)
    {
        await Db.KeyDeleteAsync(Sessions(eventId, userId));
        await Db.KeyDeleteAsync(Joined(eventId, userId));
        await Db.KeyDeleteAsync(Admission(eventId, userId));
        await Db.SortedSetRemoveAsync(Waiting(eventId), userId.ToString(CultureInfo.InvariantCulture));
        await Db.SortedSetRemoveAsync(Order(eventId), userId.ToString(CultureInfo.InvariantCulture));
        await Db.SetRemoveAsync(Registrations(eventId, cycleKey), userId.ToString(CultureInfo.InvariantCulture));
    }

    public async Task FinalizeRandomizationAsync(int eventId, string cycleKey, CancellationToken cancellationToken)
    {
        const string script = """
            if redis.call('EXISTS', KEYS[1]) == 1 then return 0 end
            local users = redis.call('SMEMBERS', KEYS[2])
            for _, user in ipairs(users) do
                if redis.call('ZSCORE', KEYS[3], user) == false then
                    local score = math.random(1, 2147483647)
                    redis.call('ZADD', KEYS[3], score, user)
                    redis.call('ZADD', KEYS[4], score, user)
                end
            end
            redis.call('SET', KEYS[1], '1')
            return #users
            """;
        await Db.ScriptEvaluateAsync(
            script,
            new RedisKey[] { Randomization(eventId, cycleKey), Registrations(eventId, cycleKey), Order(eventId), Waiting(eventId) });
    }

    public async Task<(bool Added, int Position, int Total, string Status)> JoinAsync(int eventId, int userId, string token, CancellationToken cancellationToken)
    {
        var sessionKey = Sessions(eventId, userId);
        if (!string.Equals(await Db.StringGetAsync(sessionKey), token, StringComparison.Ordinal))
            return (false, 0, 0, "TOKEN_INVALID");
        if (await Db.KeyExistsAsync(Admission(eventId, userId)))
            return (true, 0, (int)await Db.SortedSetLengthAsync(Waiting(eventId)), "ADMITTED");
        var rank = await Db.SortedSetRankAsync(Order(eventId), userId.ToString(CultureInfo.InvariantCulture));
        var total = await Db.SortedSetLengthAsync(Waiting(eventId));
        await Db.StringSetAsync(Joined(eventId, userId), "1", TimeSpan.FromDays(1));
        return (rank.HasValue, (int)(rank ?? 0) + 1, (int)total, rank.HasValue ? "WAITING" : "REGISTERED");
    }

    public async Task<bool> HasAdmissionAsync(int eventId, int userId, CancellationToken cancellationToken) =>
        await Db.KeyExistsAsync(Admission(eventId, userId));

    public async Task<(int Position, int Total, string Status, DateTimeOffset? AdmissionExpiresAt)> GetStatusAsync(int eventId, int userId, string token, CancellationToken cancellationToken)
    {
        var sessionKey = Sessions(eventId, userId);
        if (!string.Equals(await Db.StringGetAsync(sessionKey), token, StringComparison.Ordinal))
            return (0, 0, "TOKEN_INVALID", null);
        var admissionKey = Admission(eventId, userId);
        var admissionTtl = await Db.KeyTimeToLiveAsync(admissionKey);
        if (admissionTtl.HasValue)
            return (0, (int)await Db.SortedSetLengthAsync(Waiting(eventId)), "ADMITTED", QueueTimeZone.NowOffset.Add(admissionTtl.Value));
        var rank = await Db.SortedSetRankAsync(Order(eventId), userId.ToString(CultureInfo.InvariantCulture));
        var total = await Db.SortedSetLengthAsync(Waiting(eventId));
        if (!rank.HasValue && await Db.KeyExistsAsync(Joined(eventId, userId)))
            return (0, (int)total, "EXPIRED", null);
        return rank.HasValue ? ((int)rank.Value + 1, (int)total, "WAITING", null) : (0, (int)total, "REGISTERED", null);
    }

    public async Task LeaveAsync(int eventId, int userId, string token, string cycleKey, CancellationToken cancellationToken)
    {
        await Db.SortedSetRemoveAsync(Waiting(eventId), userId.ToString(CultureInfo.InvariantCulture));
        await Db.SortedSetRemoveAsync(Order(eventId), userId.ToString(CultureInfo.InvariantCulture));
        await Db.KeyDeleteAsync(Admission(eventId, userId));
        await Db.KeyDeleteAsync(Token(eventId, token));
        await Db.KeyDeleteAsync(Sessions(eventId, userId));
        await Db.SetRemoveAsync(Registrations(eventId, cycleKey), userId.ToString(CultureInfo.InvariantCulture));
        await Db.KeyDeleteAsync(Joined(eventId, userId));
    }

    public async Task RevokeAsync(int eventId, int userId, string token, string cycleKey, CancellationToken cancellationToken)
    {
        await LeaveAsync(eventId, userId, token, cycleKey, cancellationToken);
        await Db.StringSetAsync($"{Root(eventId)}:revoked:{userId}", "1", TimeSpan.FromDays(1));
    }

    public async Task<int> AdmitNextAsync(int eventId, int admissionRate, CancellationToken cancellationToken)
    {
        if (!await Db.StringSetAsync(AdmissionWindow(eventId), "1", TimeSpan.FromMinutes(1), When.NotExists)) return 0;
        var script = "local items = redis.call('ZPOPMIN', KEYS[1], ARGV[1]); for i = 1, #items, 2 do redis.call('ZREM', KEYS[3], items[i]); redis.call('SET', KEYS[2] .. items[i], '1', 'EX', ARGV[2]); end return #items / 2";
        var result = await Db.ScriptEvaluateAsync(script, new[] { (RedisKey)Waiting(eventId), (RedisKey)$"{Root(eventId)}:admission:", (RedisKey)Order(eventId) }, new RedisValue[] { admissionRate, 300 });
        return (int)result;
    }
}
