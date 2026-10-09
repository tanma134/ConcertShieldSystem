using TicketAPI.DTOs;

namespace TicketAPI.Services
{
    public interface ITicketReturnService
    {
        Task<List<MyTicketDto>> ListMyTicketsAsync(int userId);

        Task<TicketReturnDto> SubmitAsync(int userId, SubmitTicketReturnDto dto);

        Task<List<TicketReturnDto>> ListMineAsync(int userId, string? status);

        Task<TicketReturnDto> GetMineAsync(int userId, int requestId);

        Task<TicketReturnDto> CancelAsync(int userId, int requestId);

        // eventScope: null = every event (Admin); otherwise only these event ids (assigned Staff).
        Task<List<TicketReturnDto>> ListForReviewAsync(string? status, IReadOnlyCollection<int>? eventScope = null);

        Task<TicketReturnDto> ReviewAsync(int reviewerUserId, int requestId, ReviewTicketReturnDto dto, IReadOnlyCollection<int>? eventScope = null);

        // Retries paying the money back for an approved return whose refund failed.
        Task<TicketReturnDto> RetryRefundAsync(int reviewerUserId, int requestId, IReadOnlyCollection<int>? eventScope = null);
    }
}
