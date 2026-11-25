using AuthenticationAPI.Models;

namespace AuthenticationAPI.Repositories
{
    /// <summary>Search criteria, already normalised by the service (UTC dates, trimmed text).</summary>
    public class KycAccessLogFilter
    {
        public int? SubjectUserId { get; set; }
        public int? ActorUserId { get; set; }
        public string? ActorType { get; set; }
        public string? Action { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
    }

    public interface IKycAccessLogRepository
    {
        Task CreateAsync(KycAccessLog log);

        Task<(List<KycAccessLog> Items, int Total)> SearchAsync(KycAccessLogFilter filter, int page, int pageSize);
    }
}
