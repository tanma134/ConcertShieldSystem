using TicketAPI.DTOs;

namespace TicketAPI.Services
{
    public interface ICheckinService
    {
        Task<CheckinScanResponseDto> ProcessScanAsync(CheckinScanRequestDto request, int staffUserId);
    }
}
