using EventAPI.DTOs;
using Microsoft.AspNetCore.Http;

namespace EventAPI.Services
{
    public interface IEventImageService
    {
        Task<List<EventImageResponseDTO>> GetByEventIdAsync(int eventId);
        Task<EventImageResponseDTO> UploadAsync(int eventId, IFormFile file, int callerId, bool isAdmin);
        Task<List<EventImageResponseDTO>> UploadMultipleAsync(int eventId, List<IFormFile> files, int callerId, bool isAdmin);
        Task<EventImageResponseDTO> UpdateAsync(int imageId, UpdateImageDTO dto, int callerId, bool isAdmin);
        Task SetMainAsync(int imageId, int callerId, bool isAdmin);
        Task ReorderAsync(int eventId, List<ReorderImageDTO> orders, int callerId, bool isAdmin);
        Task DeleteAsync(int imageId, int callerId, bool isAdmin);
    }
}
