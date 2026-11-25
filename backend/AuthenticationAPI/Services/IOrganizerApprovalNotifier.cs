namespace AuthenticationAPI.Services;

public interface IOrganizerApprovalNotifier
{
    Task NotifyApprovedAsync(int requestId, int userId);
}
