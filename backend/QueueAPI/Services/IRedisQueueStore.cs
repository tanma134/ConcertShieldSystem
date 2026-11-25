namespace QueueAPI.Services;

public interface IRedisQueueStore
{
    Task<bool> RegisterAsync(int eventId, int userId, string token, string cycleKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> RecordRegistrationRequestAsync(int eventId, int userId, long timestampMilliseconds, CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> RecordRepeatedRegistrationAttemptAsync(int eventId, int userId, long timestampMilliseconds, CancellationToken cancellationToken);
    Task<RevokedTokenReplaySnapshot?> RecordRevokedTokenReuseAsync(int eventId, string token, long timestampMilliseconds, CancellationToken cancellationToken);
    Task ResetUserAsync(int eventId, int userId, string cycleKey, CancellationToken cancellationToken);
    Task FinalizeRandomizationAsync(int eventId, string cycleKey, CancellationToken cancellationToken);
    Task<(bool Added, int Position, int Total, string Status)> JoinAsync(int eventId, int userId, string token, CancellationToken cancellationToken);
    Task<(int Position, int Total, string Status, DateTimeOffset? AdmissionExpiresAt)> GetStatusAsync(int eventId, int userId, string token, CancellationToken cancellationToken);
    Task<bool> HasAdmissionAsync(int eventId, int userId, CancellationToken cancellationToken);
    Task LeaveAsync(int eventId, int userId, string token, string cycleKey, CancellationToken cancellationToken);
    Task RevokeAsync(int eventId, int userId, string sessionId, string token, string cycleKey, CancellationToken cancellationToken);
    Task<int> AdmitNextAsync(int eventId, int admissionRate, CancellationToken cancellationToken);
}

public sealed record RevokedTokenReplaySnapshot(
    int UserId,
    string SessionId,
    string TokenFingerprint,
    IReadOnlyList<long> AttemptTimestamps);
