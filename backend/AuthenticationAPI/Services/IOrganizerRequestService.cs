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

        Task<OrganizerRequestResponseDTO> SubmitVerificationAsync(
            int userId,
            SubmitOrganizerVerificationDTO dto,
            string callerRole = "Organizer");

        Task<List<OrganizerRequestResponseDTO>> GetVerificationRequestsAsync(
            string? status = null,
            int page = 1,
            int limit = 10,
            string callerRole = "Admin");

        Task<OrganizerRequestResponseDTO> ReviewVerificationAsync(
            int requestId,
            int reviewerId,
            ReviewOrganizerRequestDTO dto,
            string callerRole = "Admin");
    }
}