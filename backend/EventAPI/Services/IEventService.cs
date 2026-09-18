using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface IEventService
    {
        Task<EventResponseDTO> CreateAsync(CreateEventDTO dto, int organizerId);
        Task<EventResponseDTO> GetByIdAsync(int id, bool incrementView = false, bool publicOnly = true);
        Task<EventResponseDTO> GetBySlugAsync(string slug, bool incrementView = false, bool publicOnly = true);
        Task<PagedResultDTO<EventListDTO>> GetFilteredAsync(EventFilterDTO filter);
        Task<List<EventListDTO>> GetFeaturedAsync(int count);
        Task<List<EventListDTO>> GetByOrganizerIdAsync(int organizerId);
        Task<PagedResultDTO<EventListDTO>> GetModerationQueueAsync(int page, int pageSize);

        Task<EventResponseDTO> UpdateAsync(int id, UpdateEventDTO dto, int callerId, bool isAdmin);
        Task SoftDeleteAsync(int id, int callerId, bool isAdmin);
        Task RestoreAsync(int id);

        /// <summary>Full concert incl. seating chart, for the owner (any status).</summary>
        Task<EventResponseDTO> GetMineByIdAsync(int id, int callerId, bool isAdmin);

        /// <summary>Runs the pre-submit publication checks without changing anything.</summary>
        Task<SubmitValidationResultDTO> ValidateForSubmissionAsync(int id, int callerId, bool isAdmin);

        /// <summary>Draft/Rejected -> Pending. Owner only. Validates publication data first.</summary>
        Task<EventResponseDTO> SubmitAsync(int id, int callerId);

        /// <summary>Admin moderation queue with ticket/pricing summary.</summary>
        Task<PagedResultDTO<PendingEventDTO>> GetPendingAsync(int page, int pageSize);

        /// <summary>
        /// Pending -> Published. Admin only. Also grants the owner the "Organizer"
        /// role additively (they keep "Customer").
        /// </summary>
        Task<(EventResponseDTO Event, GrantRoleResult RoleGrant)> ApproveAsync(int id, int adminId, string? adminBearerToken);

        /// <summary>Pending -> Rejected (with reason). Admin only.</summary>
        Task<EventResponseDTO> RejectAsync(int id, string reason, int adminId);

        /// <summary>Approved/Pending -> Cancelled. Owner or Admin.</summary>
        Task<EventResponseDTO> CancelAsync(int id, int callerId, bool isAdmin);

        Task<EventResponseDTO> SetPosterAsync(int id, string url, string publicId, int callerId, bool isAdmin);
        Task<EventResponseDTO> SetBannerAsync(int id, string url, string publicId, int callerId, bool isAdmin);

        /// <summary>Removes the poster from the concert and from Cloudinary.</summary>
        Task<EventResponseDTO> DeletePosterAsync(int id, int callerId, bool isAdmin);

        /// <summary>Removes the banner from the concert and from Cloudinary.</summary>
        Task<EventResponseDTO> DeleteBannerAsync(int id, int callerId, bool isAdmin);

        /// <summary>Throws if the event doesn't exist or the caller doesn't own it (unless isAdmin).</summary>
        Task<Models.Event> GetOwnedEntityAsync(int id, int callerId, bool isAdmin);
    }
}
