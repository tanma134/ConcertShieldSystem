namespace IncidentAPI.Services;

// Exception dùng để trả về HTTP status code cụ thể từ Service layer
// Pattern này nhất quán với RiskServiceException trong AdminAPI
public sealed class IncidentServiceException : Exception
{
    public int StatusCode { get; }

    public IncidentServiceException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
