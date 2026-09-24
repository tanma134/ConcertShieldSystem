using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;
using Microsoft.AspNetCore.Http;

namespace EventAPI.Services
{
    public class EventImageService : IEventImageService
    {
        private readonly IEventImageRepository _imageRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ICloudinaryService _cloudinary;
        private readonly IEventAccessService _eventAccessService;

        private const int MaxImagesPerEvent = 20;

        public EventImageService(
            IEventImageRepository imageRepository,
            IEventRepository eventRepository,
            ICloudinaryService cloudinary,
            IEventAccessService eventAccessService)
        {
            _imageRepository = imageRepository;
            _eventRepository = eventRepository;
            _cloudinary = cloudinary;
            _eventAccessService = eventAccessService;
        }

        public async Task<List<EventImageResponseDTO>> GetByEventIdAsync(int eventId, int? callerId, bool isAdmin)
        {
            await _eventAccessService.EnsureVisibleAsync(eventId, callerId, isAdmin);
            var items = await _imageRepository.GetByEventIdAsync(eventId);
            return items.Select(Map).ToList();
        }

        public async Task<EventImageResponseDTO> UploadAsync(int eventId, IFormFile file, int callerId, bool isAdmin)
        {
            await GetOwnedEventAsync(eventId, callerId, isAdmin);

            var existing = await _imageRepository.GetByEventIdAsync(eventId);
            if (existing.Count >= MaxImagesPerEvent)
                throw new InvalidOperationException($"An event can have at most {MaxImagesPerEvent} gallery images.");

            if (file == null || file.Length == 0)
                throw new InvalidOperationException("No file was uploaded.");

            await using var stream = file.OpenReadStream();
            var uploadResult = await _cloudinary.UploadImageAsync(stream, file.FileName, $"events/{eventId}");

            var entity = new EventImage
            {
                EventId = eventId,
                ImageUrl = uploadResult.SecureUrl,
                PublicId = uploadResult.PublicId,
                SortOrder = existing.Count,
                IsMain = existing.Count == 0, // first image becomes main automatically
                CreatedAt = DateTime.UtcNow
            };

            var created = await _imageRepository.CreateAsync(entity);
            return Map(created);
        }

        public async Task<List<EventImageResponseDTO>> UploadMultipleAsync(int eventId, List<IFormFile> files, int callerId, bool isAdmin)
        {
            await GetOwnedEventAsync(eventId, callerId, isAdmin);

            var existing = await _imageRepository.GetByEventIdAsync(eventId);
            if (existing.Count + files.Count > MaxImagesPerEvent)
                throw new InvalidOperationException($"An event can have at most {MaxImagesPerEvent} gallery images.");

            var results = new List<EventImageResponseDTO>();
            int sortOrder = existing.Count;

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                await using var stream = file.OpenReadStream();
                var uploadResult = await _cloudinary.UploadImageAsync(stream, file.FileName, $"events/{eventId}");

                var entity = new EventImage
                {
                    EventId = eventId,
                    ImageUrl = uploadResult.SecureUrl,
                    PublicId = uploadResult.PublicId,
                    SortOrder = sortOrder++,
                    IsMain = existing.Count == 0 && results.Count == 0,
                    CreatedAt = DateTime.UtcNow
                };

                var created = await _imageRepository.CreateAsync(entity);
                results.Add(Map(created));
            }

            return results;
        }

        public async Task<EventImageResponseDTO> UpdateAsync(int imageId, UpdateImageDTO dto, int callerId, bool isAdmin)
        {
            var image = await _imageRepository.GetByIdAsync(imageId)
                ?? throw new KeyNotFoundException($"EventImage {imageId} not found.");

            await GetOwnedEventAsync(image.EventId, callerId, isAdmin);

            if (dto.SortOrder.HasValue) image.SortOrder = dto.SortOrder.Value;
            if (dto.IsMain.HasValue && dto.IsMain.Value)
            {
                await _imageRepository.SetMainAsync(image.EventId, imageId);
                image.IsMain = true;
            }
            else
            {
                await _imageRepository.UpdateAsync(image);
            }

            return Map(image);
        }

        public async Task SetMainAsync(int imageId, int callerId, bool isAdmin)
        {
            var image = await _imageRepository.GetByIdAsync(imageId)
                ?? throw new KeyNotFoundException($"EventImage {imageId} not found.");

            await GetOwnedEventAsync(image.EventId, callerId, isAdmin);
            await _imageRepository.SetMainAsync(image.EventId, imageId);
        }

        public async Task ReorderAsync(int eventId, List<ReorderImageDTO> orders, int callerId, bool isAdmin)
        {
            await GetOwnedEventAsync(eventId, callerId, isAdmin);
            var tuples = orders.Select(o => (o.ImageId, o.SortOrder)).ToList();
            await _imageRepository.ReorderAsync(eventId, tuples);
        }

        public async Task DeleteAsync(int imageId, int callerId, bool isAdmin)
        {
            var image = await _imageRepository.GetByIdAsync(imageId)
                ?? throw new KeyNotFoundException($"EventImage {imageId} not found.");

            await GetOwnedEventAsync(image.EventId, callerId, isAdmin);

            await _imageRepository.SoftDeleteAsync(imageId, callerId);
            await _cloudinary.DeleteImageAsync(image.PublicId);
        }

        private async Task<Event> GetOwnedEventAsync(int eventId, int callerId, bool isAdmin)
        {
            var ev = await _eventRepository.GetByIdAsync(eventId)
                ?? throw new KeyNotFoundException($"Event {eventId} not found.");

            if (!isAdmin && ev.OrganizerId != callerId)
                throw new UnauthorizedAccessException("You do not own this event.");

            return ev;
        }

        private static EventImageResponseDTO Map(EventImage i) => new()
        {
            ImageId = i.ImageId,
            EventId = i.EventId,
            ImageUrl = i.ImageUrl,
            SortOrder = i.SortOrder,
            IsMain = i.IsMain,
            CreatedAt = i.CreatedAt
        };
    }
}
