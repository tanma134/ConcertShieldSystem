namespace AdminAPI.Services;

// Fallback no-op email service for testing or when email is disabled
public class NullSupportEmailService : ISupportEmailService
{
    public Task SendCaseResolvedEmailAsync(string toEmail, string customerName, string caseNumber, string caseTitle, string resolution, string resolvedByName, int? orderId)
    {
        return Task.CompletedTask;
    }

    public Task SendCaseAssignedEmailAsync(string toEmail, string staffName, string caseNumber, string caseTitle, string priority, string category, string customerName, int? orderId, int caseId)
    {
        return Task.CompletedTask;
    }
}
