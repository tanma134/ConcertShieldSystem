namespace AdminAPI.Services;

// Email notification contract for support case outcomes
public interface ISupportEmailService
{
    // Sends resolution outcome email to customer when a support case is resolved and closed
    Task SendCaseResolvedEmailAsync(string toEmail, string customerName, string caseNumber, string caseTitle, string resolution, string resolvedByName, int? orderId);

    // Sends assignment notification email to staff member when a support case is assigned
    Task SendCaseAssignedEmailAsync(string toEmail, string staffName, string caseNumber, string caseTitle, string priority, string category, string customerName, int? orderId, int caseId);
}
