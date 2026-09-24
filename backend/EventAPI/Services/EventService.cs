using EventAPI.Common;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;

namespace EventAPI.Services
{
    public class EventService : IEventService
    {
        private readonly IEventRepository _eventRepository;
        private readonly ITicketTypeRepository _ticketTypeRepository;
        private readonly ISeatingRepository _seatingRepository;
        private readonly IEventSubmissionValidator _submissionValidator;
        private readonly IIdentityRoleClient _roleClient;
        private readonly ICloudinaryService _cloudinary;
        private readonly EventDbContext _context; // only used for unique-slug lookup (reuses existing SlugHelper)
        private readonly ILogger<EventService> _logger;

        /// <summary>CategoryId 1 == Music. Concerts are always Music in this system.</summary>
        public const int MusicCategoryId = 1;
        public const string MusicCategoryName = "Music";

        public EventService(
            IEventRepository eventRepository,
            ITicketTypeRepository ticketTypeRepository,
            ISeatingRepository seatingRepository,
            IEventSubmissionValidator submissionValidator,
            IIdentityRoleClient roleClient,
            ICloudinaryService cloudinary,
            EventDbContext context,
            ILogger<EventService> logger)
        {
            _eventRepository = eventRepository;
            _ticketTypeRepository = ticketTypeRepository;
            _seatingRepository = seatingRepository;
            _submissionValidator = submissionValidator;
            _roleClient = roleClient;
            _cloudinary = cloudinary;
            _context = context;
            _logger = logger;
        }

        public async Task<EventResponseDTO> CreateAsync(CreateEventDTO dto, int organizerId)
        {
            var requestedSlug = SlugHelper.NormalizeSlug(dto.Slug ?? dto.Title);
            if (!SlugHelper.IsValidSlug(requestedSlug, out var slugError))
                throw new ArgumentException(slugError);
            var availability = await SlugHelper.CheckAvailabilityAsync(_context, requestedSlug);
            if (!availability.Available)
                throw new ArgumentException($"Slug '{requestedSlug}' is already in use. Try '{availability.Suggestion}'.");
            var slug = requestedSlug;

            var entity = new Event
            {
                OrganizerId = organizerId,
                CategoryId = MusicCategoryId, // Music — fixed, customers cannot pick another category
                Title = dto.Title,
                Slug = slug,
                ShortDescription = dto.ShortDescription,
                Description = dto.Description,
                LocationName = dto.LocationName,
                Address = dto.Address,
                City = dto.City,
                Longitude = dto.Longitude,
                Latitude = dto.Latitude,
                StartsAt = dto.StartsAt,
                EndsAt = dto.EndsAt,
                Timezone = string.IsNullOrWhiteSpace(dto.Timezone) ? "SE Asia Standard Time" : dto.Timezone,
                HasSeatingChart = false,
                SeatingMode = SeatingMode.GeneralAdmission,
                RequiresVirtualQueue = dto.RequiresVirtualQueue,
                MinTicketsPerAccount = dto.MinTicketsPerAccount,
                MaxTicketsPerAccount = dto.MaxTicketsPerAccount,
                MetaTitle = dto.MetaTitle,
                MetaDescription = dto.MetaDescription,
                Status = EventStatus.Draft, // Creating a concert never grants the Organizer role by itself
                CreatedBy = organizerId,
                UpdatedBy = organizerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await _eventRepository.CreateAsync(entity);
            _logger.LogInformation("Event {EventId} created by user {UserId} as Draft", created.EventId, organizerId);
            return MapToResponse(created);
        }

        public async Task<SlugAvailabilityDTO> CheckSlugAvailabilityAsync(string slug, int? excludeEventId = null)
        {
            var normalized = SlugHelper.NormalizeSlug(slug);
            if (!SlugHelper.IsValidSlug(normalized, out _))
                return new SlugAvailabilityDTO { Slug = normalized, Available = false, Suggestion = normalized };

            var result = await SlugHelper.CheckAvailabilityAsync(_context, normalized, excludeEventId);
            return new SlugAvailabilityDTO
            {
                Slug = normalized,
                Available = result.Available,
                Suggestion = result.Suggestion
            };
        }

        /// <param name="publicOnly">
        /// True for anonymous/public endpoints: a concert that isn't Published is
        /// reported as "not found" so drafts can't be discovered by guessing ids.
        /// </param>
        public async Task<EventResponseDTO> GetByIdAsync(int id, bool incrementView = false, bool publicOnly = true)
        {
            var entity = await _eventRepository.GetByIdAsync(id, includeChildren: true)
                ?? throw new KeyNotFoundException($"Event {id} not found.");

            if (publicOnly && !EventStatus.IsPublic(entity.Status))
                throw new KeyNotFoundException($"Event {id} not found.");

            if (incrementView)
                await _eventRepository.IncrementViewCountAsync(id);

            return await MapToResponseWithSeatingAsync(entity);
        }

        public async Task<EventResponseDTO> GetBySlugAsync(string slug, bool incrementView = false, bool publicOnly = true)
        {
            var entity = await _eventRepository.GetBySlugAsync(slug, includeChildren: true)
                ?? throw new KeyNotFoundException($"Event with slug '{slug}' not found.");

            if (publicOnly && !EventStatus.IsPublic(entity.Status))
                throw new KeyNotFoundException($"Event with slug '{slug}' not found.");

            if (incrementView)
                await _eventRepository.IncrementViewCountAsync(entity.EventId);

            return await MapToResponseWithSeatingAsync(entity);
        }

        public async Task<PagedResultDTO<EventListDTO>> GetFilteredAsync(EventFilterDTO filter)
        {
            var (items, totalCount) = await _eventRepository.GetFilteredAsync(filter);
            return new PagedResultDTO<EventListDTO>
            {
                Items = items.Select(MapToListDto).ToList(),
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<List<EventListDTO>> GetFeaturedAsync(int count)
        {
            var items = await _eventRepository.GetFeaturedAsync(count);
            return items.Select(MapToListDto).ToList();
        }

        public async Task<List<EventListDTO>> GetByOrganizerIdAsync(int organizerId)
        {
            var items = await _eventRepository.GetByOrganizerIdAsync(organizerId);
            return items.Select(MapToListDto).ToList();
        }

        public async Task<OrganizerDashboardSummaryDTO> GetOrganizerDashboardAsync(int organizerId)
        {
            // Reuses the same query as "my events" (already includes non-deleted
            // TicketTypes), so revenue/sold counts cost nothing extra to compute -
            // just a sum over data that's already in memory.
            var events = await _eventRepository.GetByOrganizerIdAsync(organizerId);

            var summary = new OrganizerDashboardSummaryDTO
            {
                TotalEvents = events.Count,
                DraftEvents = events.Count(e => EventStatus.Normalize(e.Status) == EventStatus.Draft),
                PendingEvents = events.Count(e => EventStatus.Normalize(e.Status) == EventStatus.Pending),
                PublishedEvents = events.Count(e => EventStatus.Normalize(e.Status) == EventStatus.Published),
                RejectedEvents = events.Count(e => EventStatus.Normalize(e.Status) == EventStatus.Rejected),
                CancelledEvents = events.Count(e => EventStatus.Normalize(e.Status) == EventStatus.Cancelled),
            };

            foreach (var e in events)
            {
                var ticketTypes = e.TicketTypes?.Where(t => !t.IsDeleted).ToList() ?? new List<TicketType>();
                var sold = ticketTypes.Sum(t => t.SoldQuantity);
                var revenue = ticketTypes.Sum(t => (long)t.SoldQuantity * t.Price);

                summary.TotalTicketsSold += sold;
                summary.TotalRevenue += revenue;

                summary.Events.Add(new OrganizerDashboardEventDTO
                {
                    EventId = e.EventId,
                    Title = e.Title,
                    Slug = e.Slug,
                    PosterUrl = e.PosterUrl,
                    Status = EventStatus.Normalize(e.Status),
                    StartsAt = e.StartsAt,
                    TotalTickets = ticketTypes.Sum(t => t.Quantity),
                    SoldTickets = sold,
                    Revenue = revenue,
                });
            }

            return summary;
        }

        public async Task<PagedResultDTO<EventListDTO>> GetModerationQueueAsync(int page, int pageSize)
        {
            var (items, totalCount) = await _eventRepository.GetModerationQueueAsync(page, pageSize);
            return new PagedResultDTO<EventListDTO>
            {
                Items = items.Select(MapToListDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<Event> GetOwnedEntityAsync(int id, int callerId, bool isAdmin)
        {
            var entity = await _eventRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Event {id} not found.");

            if (!isAdmin && entity.OrganizerId != callerId)
                throw new UnauthorizedAccessException("You do not own this event.");

            return entity;
        }

        public async Task<EventResponseDTO> UpdateAsync(int id, UpdateEventDTO dto, int callerId, bool isAdmin)
        {
            var entity = await GetOwnedEntityAsync(id, callerId, isAdmin);

            var currentStatus = EventStatus.Normalize(entity.Status);
            if (!isAdmin && !EventStatus.Editable.Contains(currentStatus))
                throw new InvalidOperationException(
                    $"Concert cannot be edited while in status '{currentStatus}'. Only Draft or Rejected concerts can be edited.");

            if (dto.Title != null) entity.Title = dto.Title;
            if (dto.Slug != null)
            {
                var normalizedSlug = SlugHelper.NormalizeSlug(dto.Slug);
                if (!SlugHelper.IsValidSlug(normalizedSlug, out var slugError))
                    throw new ArgumentException(slugError);
                var availability = await SlugHelper.CheckAvailabilityAsync(_context, normalizedSlug, id);
                if (!availability.Available)
                    throw new ArgumentException($"Slug '{normalizedSlug}' is already in use. Try '{availability.Suggestion}'.");
                entity.Slug = normalizedSlug;
            }

            if (dto.ShortDescription != null) entity.ShortDescription = dto.ShortDescription;
            if (dto.Description != null) entity.Description = dto.Description;
            if (dto.LocationName != null) entity.LocationName = dto.LocationName;
            if (dto.Address != null) entity.Address = dto.Address;
            if (dto.City != null) entity.City = dto.City;
            if (dto.Longitude.HasValue) entity.Longitude = dto.Longitude;
            if (dto.Latitude.HasValue) entity.Latitude = dto.Latitude;
            if (dto.StartsAt.HasValue) entity.StartsAt = dto.StartsAt.Value;
            if (dto.EndsAt.HasValue) entity.EndsAt = dto.EndsAt.Value;
            if (dto.Timezone != null) entity.Timezone = dto.Timezone;
            // HasSeatingChart is not settable via Update — see the comment on
            // UpdateEventDTO.HasSeatingChart (removed 21/09/2026). SeatingService is
            // the single owner of this flag.
            if (dto.RequiresVirtualQueue.HasValue) entity.RequiresVirtualQueue = dto.RequiresVirtualQueue.Value;
            if (dto.MinTicketsPerAccount.HasValue) entity.MinTicketsPerAccount = dto.MinTicketsPerAccount;
            if (dto.MaxTicketsPerAccount.HasValue) entity.MaxTicketsPerAccount = dto.MaxTicketsPerAccount;
            if (dto.MetaTitle != null) entity.MetaTitle = dto.MetaTitle;
            if (dto.MetaDescription != null) entity.MetaDescription = dto.MetaDescription;

            if (entity.EndsAt <= entity.StartsAt)
                throw new InvalidOperationException("EndsAt must be after StartsAt.");

            entity.UpdatedBy = callerId;
            await _eventRepository.UpdateAsync(entity);

            var reloaded = await _eventRepository.GetByIdAsync(id, includeChildren: true);
            return MapToResponse(reloaded!);
        }

        public async Task SoftDeleteAsync(int id, int callerId, bool isAdmin)
        {
            var entity = await GetOwnedEntityAsync(id, callerId, isAdmin);
            if (!isAdmin && !EventStatus.Editable.Contains(EventStatus.Normalize(entity.Status)))
                throw new InvalidOperationException(
                    $"Only Draft or Rejected concerts can be deleted. Current status: '{EventStatus.Normalize(entity.Status)}'.");

            await _eventRepository.SoftDeleteAsync(id, callerId);
            _logger.LogInformation("Event {EventId} soft-deleted by user {UserId}", id, callerId);
        }

        public async Task RestoreAsync(int id)
        {
            await _eventRepository.RestoreAsync(id);
        }

        public async Task<EventResponseDTO> GetMineByIdAsync(int id, int callerId, bool isAdmin)
        {
            // Ownership first, so a non-owner can't probe other people's drafts.
            await GetOwnedEntityAsync(id, callerId, isAdmin);

            var entity = await _eventRepository.GetByIdAsync(id, includeChildren: true)
                ?? throw new KeyNotFoundException($"Event {id} not found.");

            return await MapToResponseWithSeatingAsync(entity);
        }

        public async Task<SubmitValidationResultDTO> ValidateForSubmissionAsync(int id, int callerId, bool isAdmin)
        {
            await GetOwnedEntityAsync(id, callerId, isAdmin);
            return await _submissionValidator.ValidateAsync(id);
        }

        public async Task<EventResponseDTO> SubmitAsync(int id, int callerId)
        {
            var entity = await GetOwnedEntityAsync(id, callerId, isAdmin: false);

            var status = EventStatus.Normalize(entity.Status);
            if (!EventStatus.Submittable.Contains(status))
                throw new InvalidOperationException(
                    $"Only Draft or Rejected concerts can be submitted. Current status: '{status}'.");

            // Drafts may be incomplete; a submission may not be.
            var validation = await _submissionValidator.ValidateAsync(id);
            if (!validation.IsValid)
                throw new SubmissionValidationException(validation);

            entity.Status = EventStatus.Pending;
            entity.RejectedReason = null;
            entity.RejectedAt = null;
            entity.SubmittedAt = DateTime.UtcNow;
            entity.UpdatedBy = callerId;
            await _eventRepository.UpdateAsync(entity);

            _logger.LogInformation("Concert {EventId} submitted for approval by user {UserId}", id, callerId);

            var reloaded = await _eventRepository.GetByIdAsync(id, includeChildren: true);
            return await MapToResponseWithSeatingAsync(reloaded!);
        }

        public async Task<PagedResultDTO<PendingEventDTO>> GetPendingAsync(int page, int pageSize)
        {
            var (items, totalCount) = await _eventRepository.GetModerationQueueAsync(page, pageSize);

            return new PagedResultDTO<PendingEventDTO>
            {
                Items = items.Select(e => new PendingEventDTO
                {
                    EventId = e.EventId,
                    OrganizerId = e.OrganizerId,
                    Title = e.Title,
                    Slug = e.Slug,
                    ShortDescription = e.ShortDescription,
                    PosterUrl = e.PosterUrl,
                    BannerUrl = e.BannerUrl,
                    LocationName = e.LocationName,
                    City = e.City,
                    StartsAt = e.StartsAt,
                    EndsAt = e.EndsAt,
                    Status = EventStatus.Normalize(e.Status),
                    HasSeatingChart = e.HasSeatingChart,
                    TicketTypeCount = e.TicketTypes?.Count(t => !t.IsDeleted) ?? 0,
                    TotalTickets = e.TicketTypes?.Where(t => !t.IsDeleted).Sum(t => t.Quantity) ?? 0,
                    MinPrice = e.TicketTypes?.Where(t => !t.IsDeleted).Select(t => (long?)t.Price).DefaultIfEmpty(null).Min(),
                    SubmittedAt = e.SubmittedAt,
                    CreatedAt = e.CreatedAt
                }).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<(EventResponseDTO Event, GrantRoleResult RoleGrant)> ApproveAsync(
            int id, int adminId, string? adminBearerToken)
        {
            var entity = await _eventRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Event {id} not found.");

            var status = EventStatus.Normalize(entity.Status);
            if (status != EventStatus.Pending)
                throw new InvalidOperationException($"Only Pending concerts can be approved. Current status: '{status}'.");

            // Re-run the publication checks: the concert has been sitting in the queue
            // and something (e.g. a deleted ticket type) may have changed since submit.
            var validation = await _submissionValidator.ValidateAsync(id);
            if (!validation.IsValid)
                throw new SubmissionValidationException(validation);

            var now = DateTime.UtcNow;
            entity.Status = EventStatus.Published;
            entity.RejectedReason = null;
            entity.RejectedAt = null;
            entity.ApprovedAt = now;
            entity.PublishedAt = now;
            entity.ReviewedBy = adminId;
            entity.UpdatedBy = adminId;
            await _eventRepository.UpdateAsync(entity);

            _logger.LogInformation("Concert {EventId} approved and published by admin {AdminId}", id, adminId);

            var roleGrant = await _roleClient.GrantRoleAsync(
                entity.OrganizerId, "Organizer", adminBearerToken);

            if (!roleGrant.Success)
            {
                _logger.LogWarning(
                    "Concert {EventId} was published but Organizer role grant failed for user {UserId}: {Message}",
                    id, entity.OrganizerId, roleGrant.Message);
            }

            var reloaded = await _eventRepository.GetByIdAsync(id, includeChildren: true);
            return (await MapToResponseWithSeatingAsync(reloaded!), roleGrant);
        }

        public async Task<EventResponseDTO> RejectAsync(int id, string reason, int adminId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("A rejection reason is required.");

            var entity = await _eventRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Event {id} not found.");

            var status = EventStatus.Normalize(entity.Status);
            if (status != EventStatus.Pending)
                throw new InvalidOperationException($"Only Pending concerts can be rejected. Current status: '{status}'.");

            entity.Status = EventStatus.Rejected;
            entity.RejectedReason = reason;
            entity.RejectedAt = DateTime.UtcNow;
            entity.ReviewedBy = adminId;
            entity.UpdatedBy = adminId;
            await _eventRepository.UpdateAsync(entity);

            _logger.LogInformation("Concert {EventId} rejected by admin {AdminId}: {Reason}", id, adminId, reason);

            var reloaded = await _eventRepository.GetByIdAsync(id, includeChildren: true);
            return await MapToResponseWithSeatingAsync(reloaded!);
        }

        public async Task<EventResponseDTO> CancelAsync(int id, int callerId, bool isAdmin)
        {
            var entity = await GetOwnedEntityAsync(id, callerId, isAdmin);

            var status = EventStatus.Normalize(entity.Status);
            if (status != EventStatus.Published && status != EventStatus.Pending)
                throw new InvalidOperationException($"Only Pending or Published concerts can be cancelled. Current status: '{status}'.");

            entity.Status = EventStatus.Cancelled;
            entity.UpdatedBy = callerId;
            await _eventRepository.UpdateAsync(entity);

            var reloaded = await _eventRepository.GetByIdAsync(id, includeChildren: true);
            return MapToResponse(reloaded!);
        }

        public async Task<EventResponseDTO> SetPosterAsync(int id, string url, string publicId, int callerId, bool isAdmin)
        {
            var entity = await GetOwnedEntityAsync(id, callerId, isAdmin);

            // Replacing: drop the previous Cloudinary asset so we don't leak storage.
            var previousPublicId = entity.PosterPublicId;

            entity.PosterUrl = url;
            entity.PosterPublicId = publicId;
            entity.UpdatedBy = callerId;
            await _eventRepository.UpdateAsync(entity);

            if (!string.IsNullOrWhiteSpace(previousPublicId) && previousPublicId != publicId)
                await _cloudinary.DeleteImageAsync(previousPublicId);

            var reloaded = await _eventRepository.GetByIdAsync(id, includeChildren: true);
            return await MapToResponseWithSeatingAsync(reloaded!);
        }

        public async Task<EventResponseDTO> SetBannerAsync(int id, string url, string publicId, int callerId, bool isAdmin)
        {
            var entity = await GetOwnedEntityAsync(id, callerId, isAdmin);

            var previousPublicId = entity.BannerPublicId;

            entity.BannerUrl = url;
            entity.BannerPublicId = publicId;
            entity.UpdatedBy = callerId;
            await _eventRepository.UpdateAsync(entity);

            if (!string.IsNullOrWhiteSpace(previousPublicId) && previousPublicId != publicId)
                await _cloudinary.DeleteImageAsync(previousPublicId);

            var reloaded = await _eventRepository.GetByIdAsync(id, includeChildren: true);
            return await MapToResponseWithSeatingAsync(reloaded!);
        }

        public async Task<EventResponseDTO> DeletePosterAsync(int id, int callerId, bool isAdmin)
        {
            var entity = await GetOwnedEntityAsync(id, callerId, isAdmin);

            if (string.IsNullOrWhiteSpace(entity.PosterUrl))
                throw new KeyNotFoundException("This concert has no poster to delete.");

            var publicId = entity.PosterPublicId;

            entity.PosterUrl = null;
            entity.PosterPublicId = null;
            entity.UpdatedBy = callerId;
            await _eventRepository.UpdateAsync(entity);

            await _cloudinary.DeleteImageAsync(publicId);

            var reloaded = await _eventRepository.GetByIdAsync(id, includeChildren: true);
            return await MapToResponseWithSeatingAsync(reloaded!);
        }

        public async Task<EventResponseDTO> DeleteBannerAsync(int id, int callerId, bool isAdmin)
        {
            var entity = await GetOwnedEntityAsync(id, callerId, isAdmin);

            if (string.IsNullOrWhiteSpace(entity.BannerUrl))
                throw new KeyNotFoundException("This concert has no banner to delete.");

            var publicId = entity.BannerPublicId;

            entity.BannerUrl = null;
            entity.BannerPublicId = null;
            entity.UpdatedBy = callerId;
            await _eventRepository.UpdateAsync(entity);

            await _cloudinary.DeleteImageAsync(publicId);

            var reloaded = await _eventRepository.GetByIdAsync(id, includeChildren: true);
            return await MapToResponseWithSeatingAsync(reloaded!);
        }

        // ---- Mapping ----

        public static EventResponseDTO MapToResponse(Event e) => new()
        {
            EventId = e.EventId,
            OrganizerId = e.OrganizerId,
            CategoryId = e.CategoryId,
            Title = e.Title,
            Slug = e.Slug,
            ShortDescription = e.ShortDescription,
            Description = e.Description,
            PosterUrl = e.PosterUrl,
            BannerUrl = e.BannerUrl,
            LocationName = e.LocationName,
            Address = e.Address,
            City = e.City,
            Longitude = e.Longitude,
            Latitude = e.Latitude,
            StartsAt = e.StartsAt,
            EndsAt = e.EndsAt,
            Timezone = e.Timezone,
            HasSeatingChart = e.HasSeatingChart,
            SeatingMode = SeatingMode.Normalize(e.SeatingMode),
            RequiresVirtualQueue = e.RequiresVirtualQueue,
            Status = EventStatus.Normalize(e.Status),
            RejectedReason = e.RejectedReason,
            IsFeatured = e.IsFeatured,
            ViewCount = e.ViewCount,
            TotalTickets = e.TicketTypes?.Where(t => !t.IsDeleted).Sum(t => t.Quantity) ?? e.TotalTickets,
            SoldTickets = e.TicketTypes?.Where(t => !t.IsDeleted).Sum(t => t.SoldQuantity) ?? e.SoldTickets,
            MinTicketsPerAccount = e.MinTicketsPerAccount,
            MaxTicketsPerAccount = e.MaxTicketsPerAccount,
            MetaTitle = e.MetaTitle,
            MetaDescription = e.MetaDescription,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            PublishedAt = e.PublishedAt,
            SubmittedAt = e.SubmittedAt,
            ApprovedAt = e.ApprovedAt,
            RejectedAt = e.RejectedAt,
            ReviewedBy = e.ReviewedBy,
            Category = MusicCategoryName,
            IsPublic = EventStatus.IsPublic(e.Status),
            TicketTypes = e.TicketTypes?.Where(t => !t.IsDeleted).OrderBy(t => t.SortOrder).Select(MapTicketType).ToList() ?? new(),
            Images = e.EventImages?.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder).Select(MapImage).ToList() ?? new(),
            RefundPolicies = e.RefundPolicies?.Where(r => r.IsActive).Select(MapRefundPolicy).ToList() ?? new()
        };

        private static EventListDTO MapToListDto(Event e) => new()
        {
            EventId = e.EventId,
            OrganizerId = e.OrganizerId,
            Title = e.Title,
            Slug = e.Slug,
            ShortDescription = e.ShortDescription,
            PosterUrl = e.PosterUrl,
            LocationName = e.LocationName,
            City = e.City,
            StartsAt = e.StartsAt,
            EndsAt = e.EndsAt,
            Status = EventStatus.Normalize(e.Status),
            HasSeatingChart = e.HasSeatingChart,
            SeatingMode = SeatingMode.Normalize(e.SeatingMode),
            IsFeatured = e.IsFeatured,
            ViewCount = e.ViewCount,
            TotalTickets = e.TicketTypes?.Where(t => !t.IsDeleted).Sum(t => t.Quantity) ?? e.TotalTickets,
            SoldTickets = e.TicketTypes?.Where(t => !t.IsDeleted).Sum(t => t.SoldQuantity) ?? e.SoldTickets,
            MinPrice = e.TicketTypes?.Where(t => !t.IsDeleted).Select(t => (long?)t.Price).DefaultIfEmpty(null).Min(),
            Category = MusicCategoryName,
            CreatedAt = e.CreatedAt,
            SubmittedAt = e.SubmittedAt,
            ApprovedAt = e.ApprovedAt,
            RejectedAt = e.RejectedAt,
            ReviewedBy = e.ReviewedBy,
            RejectedReason = e.RejectedReason
        };

        /// <summary>
        /// MapToResponse plus the seating chart, which lives in its own aggregate and
        /// therefore isn't part of the Event's Include graph.
        /// </summary>
        private async Task<EventResponseDTO> MapToResponseWithSeatingAsync(Event e)
        {
            var dto = MapToResponse(e);

            if (e.HasSeatingChart)
            {
                var map = await _seatingRepository.GetByEventIdAsync(e.EventId, includeSeats: true);
                if (map != null)
                {
                    dto.SeatingChart = SeatingService.MapChart(map, includeSeats: false);
                }
            }

            return dto;
        }

        private static TicketTypeResponseDTO MapTicketType(TicketType t) => new()
        {
            TicketTypeId = t.TicketTypeId,
            EventId = t.EventId,
            TypeName = t.TypeName,
            Description = t.Description,
            Price = t.Price,
            OriginalPrice = t.OriginalPrice,
            Quantity = t.Quantity,
            SoldQuantity = t.SoldQuantity,
            MinPerOrder = t.MinPerOrder,
            MaxPerOrder = t.MaxPerOrder,
            ColorCode = t.ColorCode,
            SortOrder = t.SortOrder,
            Status = t.Status,
            SalesStartsAt = t.SalesStartsAt,
            SalesEndsAt = t.SalesEndsAt,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };

        private static EventImageResponseDTO MapImage(EventImage i) => new()
        {
            ImageId = i.ImageId,
            EventId = i.EventId,
            ImageUrl = i.ImageUrl,
            SortOrder = i.SortOrder,
            IsMain = i.IsMain,
            CreatedAt = i.CreatedAt
        };

        private static RefundPolicyResponseDTO MapRefundPolicy(RefundPolicy r) => new()
        {
            RefundPolicyId = r.RefundPolicyId,
            EventId = r.EventId,
            PolicyName = r.PolicyName,
            Description = r.Description,
            DeadlineBeforeEventHours = r.DeadlineBeforeEventHours,
            RefundPercent = r.RefundPercent,
            RequiresOrganizerApproval = r.RequiresOrganizerApproval,
            IsActive = r.IsActive,
            CreatedAt = r.CreatedAt
        };
    }
}
