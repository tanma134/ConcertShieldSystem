using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface IEventService
    {
        Task<SlugAvailabilityDTO> CheckSlugAvailabilityAsync(string slug, int? excludeEventId = null);
        Task<EventResponseDTO> CreateAsync(CreateEventDTO dto, int organizerId);
        Task<EventResponseDTO> GetByIdAsync(int id, bool incrementView = false, bool publicOnly = true);
        Task<EventResponseDTO> GetBySlugAsync(string slug, bool incrementView = false, bool publicOnly = true);
        Task<PagedResultDTO<EventListDTO>> GetFilteredAsync(EventFilterDTO filter);
        Task<List<EventListDTO>> GetFeaturedAsync(int count);
        Task<List<EventListDTO>> GetByOrganizerIdAsync(int organizerId);
        Task<OrganizerDashboardSummaryDTO> GetOrganizerDashboardAsync(int organizerId);
        Task<PagedResultDTO<EventListDTO>> GetModerationQueueAsync(int page, int pageSize);

        Task<EventResponseDTO> UpdateAsync(int id, UpdateEventDTO dto, int callerId, bool isAdmin);
        Task SoftDeleteAsync(int id, int callerId, bool isAdmin);
        Task RestoreAsync(int id);

        // Full concert incl. seating chart, for the owner (any status).
        Task<EventResponseDTO> GetMineByIdAsync(int id, int callerId, bool isAdmin);

        // Runs the pre-submit publication checks without changing anything.
        Task<SubmitValidationResultDTO> ValidateForSubmissionAsync(int id, int callerId, bool isAdmin);

        // Draft/Rejected -> Pending. Owner only. Validates publication data first.
        Task<EventResponseDTO> SubmitAsync(int id, int callerId);

        // Admin moderation queue with ticket/pricing summary.
        Task<PagedResultDTO<PendingEventDTO>> GetPendingAsync(int page, int pageSize);

        // Pending -> Published and grants Organizer additively.
        Task<(EventResponseDTO Event, GrantRoleResult RoleGrant)> ApproveAsync(
            int id, int adminId, string? adminBearerToken);

        // Pending -> Rejected (with reason). Admin only.
        Task<EventResponseDTO> RejectAsync(int id, string reason, int adminId);

        // Approved/Pending -> Cancelled. Owner or Admin.
        Task<EventResponseDTO> CancelAsync(int id, int callerId, bool isAdmin);

        Task<EventResponseDTO> SetPosterAsync(int id, string url, string publicId, int callerId, bool isAdmin);
        Task<EventResponseDTO> SetBannerAsync(int id, string url, string publicId, int callerId, bool isAdmin);

        // Removes the poster from the concert and from Cloudinary.
        Task<EventResponseDTO> DeletePosterAsync(int id, int callerId, bool isAdmin);

        // Removes the banner from the concert and from Cloudinary.
        Task<EventResponseDTO> DeleteBannerAsync(int id, int callerId, bool isAdmin);

        // Throws if the event doesn't exist or the caller doesn't own it (unless isAdmin).
        Task<Models.Event> GetOwnedEntityAsync(int id, int callerId, bool isAdmin);
    }
}
