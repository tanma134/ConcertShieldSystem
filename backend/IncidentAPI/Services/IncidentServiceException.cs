namespace IncidentAPI.Services;

public sealed class IncidentServiceException : Exception
{
    public int StatusCode { get; }

    public IncidentServiceException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
