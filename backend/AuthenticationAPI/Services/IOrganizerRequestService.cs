using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IOrganizerRequestService
    {
        Task<OrganizerRequestResponseDTO> CreateRequestAsync(int userId, CreateOrganizerRequestDTO dto);

        Task<List<OrganizerRequestResponseDTO>> GetMyRequestsAsync(int userId);

        Task<List<OrganizerRequestResponseDTO>> GetAllRequestsAsync(string? status);

        Task<OrganizerRequestResponseDTO> GetByIdAsync(int requestId);

        Task<OrganizerRequestResponseDTO> ReviewRequestAsync(int requestId, int reviewerId, ReviewOrganizerRequestDTO dto);
    }
}