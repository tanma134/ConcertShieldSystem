using EventAPI.DTOs;
using EventAPI.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventAPI.Common;
using static EventAPI.DTOs.ConfirmEventSaleDTO;

namespace EventAPI.Controllers
{
    [Route("api/events")]
    public class EventsController : BaseApiController
    {
        private readonly IEventService _eventService;
        private readonly IEventStaffService _eventStaffService;
        private readonly IValidator<CreateEventDTO> _createValidator;
        private readonly IValidator<UpdateEventDTO> _updateValidator;
        private readonly ILogger<EventsController> _logger;
        private readonly IAuditLogClient _audit;

        public EventsController(
            IEventService eventService,
            IEventStaffService eventStaffService,
            IValidator<CreateEventDTO> createValidator,
            IValidator<UpdateEventDTO> updateValidator,
            ILogger<EventsController> logger,
            IAuditLogClient audit)
        {
            _eventService = eventService;
            _eventStaffService = eventStaffService;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _logger = logger;
            _audit = audit;
        }

        [HttpGet("staff/{staffId:int}")]
        [Authorize]
        public async Task<IActionResult> GetEventsByStaff(int staffId)
        {
            try
            {
                // This lists which events a staff member is posted to, so it must not be
                // readable anonymously. A staff account may read its own list; admins may
                // read anyone's; nobody else may read another account's postings.
                var callerId = CurrentUserIdOrNull ?? 0;
                if (!IsAdmin && staffId != callerId)
                    return Forbid();

                var targetId = staffId > 0 ? staffId : callerId;
                var result = await _eventStaffService.GetAssignmentsByStaffAsync(targetId);
                return Ok(ApiResponseDTO<List<StaffEventAssignmentDTO>>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetList([FromQuery] EventFilterDTO filter)
        {
            filter.PublishedOnly = true;
            var result = await _eventService.GetFilteredAsync(filter);
            return Ok(ApiResponseDTO<PagedResultDTO<EventListDTO>>.SuccessResponse(result));
        }

        [HttpGet("filter")]
        [AllowAnonymous]
        public async Task<IActionResult> Filter([FromQuery] EventFilterDTO filter)
            => await GetList(filter);

        [HttpGet("featured")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFeatured([FromQuery] int count = 10)
        {
            var result = await _eventService.GetFeaturedAsync(count);
            return Ok(ApiResponseDTO<List<EventListDTO>>.SuccessResponse(result));
        }

        [HttpGet("slug-availability")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> CheckSlugAvailability(
            [FromQuery] string slug, [FromQuery] int? excludeEventId = null)
        {
            if (excludeEventId.HasValue)
            {
                try { await _eventService.GetMineByIdAsync(excludeEventId.Value, CurrentUserId, IsAdmin); }
                catch (Exception ex) { return HandleException(ex); }
            }

            var result = await _eventService.CheckSlugAvailabilityAsync(slug, excludeEventId);
            return Ok(ApiResponseDTO<SlugAvailabilityDTO>.SuccessResponse(result));
        }

        [HttpGet("city/{city}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByCity(string city, [FromQuery] EventFilterDTO filter)
        {
            filter.City = city;
            return await GetList(filter);
        }

        [HttpGet("slug/{slug}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            try
            {
                var result = await _eventService.GetBySlugAsync(slug, incrementView: true, publicOnly: true);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var result = await _eventService.GetByIdAsync(id, incrementView: true, publicOnly: true);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result));
            }
            catch (KeyNotFoundException) when ((CurrentUserIdOrNull ?? 0) > 0)
            {
                // Not public: only the organizer who owns it (or an admin) may read it.
                // Anyone else still gets the same "not found" answer as an anonymous caller.
                try
                {
                    var mine = await _eventService.GetMineByIdAsync(id, CurrentUserId, IsAdmin);
                    return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(mine));
                }
                catch (Exception ex) { return HandleException(ex); }
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpGet("mine")]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> GetMine()
        {
            var result = await _eventService.GetByOrganizerIdAsync(CurrentUserId);
            return Ok(ApiResponseDTO<List<EventListDTO>>.SuccessResponse(result));
        }

        [HttpGet("mine/dashboard")]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> GetMyDashboard()
        {
            var result = await _eventService.GetOrganizerDashboardAsync(CurrentUserId);
            return Ok(ApiResponseDTO<OrganizerDashboardSummaryDTO>.SuccessResponse(result));
        }

        [HttpGet("mine/{id:int}")]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> GetMineById(int id)
        {
            try
            {
                var result = await _eventService.GetMineByIdAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Create([FromBody] CreateEventDTO dto)
        {
            var validation = await _createValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponseDTO<object>.FailResponse("Validation failed", validation.Errors.Select(e => e.ErrorMessage).ToList()));

            try
            {
                var result = await _eventService.CreateAsync(dto, CurrentUserId);
                return CreatedAtAction(nameof(GetMineById), new { id = result.EventId },
                    ApiResponseDTO<EventResponseDTO>.SuccessResponse(result, "Concert created as Draft"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateEventDTO dto)
        {
            var validation = await _updateValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(ApiResponseDTO<object>.FailResponse("Validation failed", validation.Errors.Select(e => e.ErrorMessage).ToList()));

            try
            {
                var result = await _eventService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result, "Concert updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _eventService.SoftDeleteAsync(id, CurrentUserId, IsAdmin);
                await AuditAsync(_audit, "EVENT_DELETED", "Event", id);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Concert deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("{id:int}/restore")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> Restore(int id)
        {
            await _eventService.RestoreAsync(id);
            await AuditAsync(_audit, "EVENT_RESTORED", "Event", id);
            return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Concert restored"));
        }

        [HttpGet("{id:int}/validate")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> ValidateForSubmission(int id)
        {
            try
            {
                var result = await _eventService.ValidateForSubmissionAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<SubmitValidationResultDTO>.SuccessResponse(
                    result,
                    result.IsValid ? "Ready to submit" : "Publication data is incomplete"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("{id:int}/submit")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Submit(int id)
        {
            try
            {
                var result = await _eventService.SubmitAsync(id, CurrentUserId);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result, "Concert submitted for approval (now Pending)"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("{id:int}/cancel")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Cancel(
            int id,
            [FromForm] string? reason,
            [FromForm] string? evidenceUrl,
            IFormFile? evidenceFile,
            [FromServices] ICloudinaryService cloudinary)
        {
            try
            {
                // Admin cancellations require an official reason; a supporting document is
                // uploaded to storage first so its URL can be stored with the decision.
                string? uploadedUrl = evidenceUrl?.Trim();
                if (evidenceFile is { Length: > 0 })
                {
                    await _eventService.GetOwnedEntityAsync(id, CurrentUserId, IsAdmin);
                    await using var stream = evidenceFile.OpenReadStream();
                    var upload = await cloudinary.UploadImageAsync(
                        stream, evidenceFile.FileName, $"events/{id}/cancellation");
                    uploadedUrl = upload.SecureUrl;
                }

                var result = await _eventService.CancelAsync(id, CurrentUserId, IsAdmin, reason, uploadedUrl);
                await AuditAsync(_audit, "EVENT_CANCELLED", "Event", id, newValue: new { status = result.Status });
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result, "Concert cancelled"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpGet("pending")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> GetPending([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
        {
            var result = await _eventService.GetPendingAsync(page, pageSize);
            return Ok(ApiResponseDTO<PagedResultDTO<PendingEventDTO>>.SuccessResponse(result));
        }

        [HttpGet("admin")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> AdminGetAll([FromQuery] EventFilterDTO filter)
        {
            filter.PublishedOnly = false;
            filter.ExcludeDrafts = true;
            var result = await _eventService.GetFilteredAsync(filter);
            return Ok(ApiResponseDTO<PagedResultDTO<EventListDTO>>.SuccessResponse(result));
        }

        [HttpGet("moderation/queue")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> ModerationQueue([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
            => await GetPending(page, pageSize);

        [HttpGet("admin/{id:int}")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> AdminGetById(int id)
        {
            try
            {
                var result = await _eventService.GetByIdAsync(id, incrementView: false, publicOnly: false);
                if (result.Status == EventStatus.Draft)
                {
                    return NotFound(ApiResponseDTO<object>.FailResponse("Event not found or is still in organizer draft."));
                }
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("{id:int}/approve")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                var (result, roleGrant) = await _eventService.ApproveAsync(
                    id, CurrentUserId, BearerToken);

                await AuditAsync(_audit, "EVENT_APPROVED", "Event", id, newValue: new { status = result.Status });

                var message = roleGrant.Success
                    ? "Concert approved and published. Owner now also has the Organizer role."
                    : "Concert approved, but Organizer role could not be granted: " + roleGrant.Message;

                return Ok(new ApiResponseDTO<EventResponseDTO>
                {
                    Success = true,
                    Message = message,
                    Data = result,
                    Errors = roleGrant.Success ? null : new List<string> { roleGrant.Message }
                });
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("{id:int}/reject")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> Reject(int id, [FromBody] RejectEventDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Reason))
                return BadRequest(ApiResponseDTO<object>.FailResponse("A rejection reason is required."));

            try
            {
                var result = await _eventService.RejectAsync(id, dto.Reason, CurrentUserId);
                await AuditAsync(_audit, "EVENT_REJECTED", "Event", id, newValue: new { status = result.Status, reason = dto.Reason });
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result, "Concert rejected"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("{id:int}/poster")]
        [Authorize(Policy = "RequireOrganizer")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UploadPoster(int id, IFormFile file, [FromServices] ICloudinaryService cloudinary)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponseDTO<object>.FailResponse("No file uploaded"));

            try
            {
                // Ownership check before we spend an upload.
                await _eventService.GetOwnedEntityAsync(id, CurrentUserId, IsAdmin);
                await using var stream = file.OpenReadStream();
                var upload = await cloudinary.UploadImageAsync(stream, file.FileName, $"events/{id}/poster");
                var result = await _eventService.SetPosterAsync(id, upload.SecureUrl, upload.PublicId, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result, "Poster uploaded"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{id:int}/poster")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> DeletePoster(int id)
        {
            try
            {
                var result = await _eventService.DeletePosterAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result, "Poster deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("{id:int}/banner")]
        [Authorize(Policy = "RequireOrganizer")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UploadBanner(int id, IFormFile file, [FromServices] ICloudinaryService cloudinary)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponseDTO<object>.FailResponse("No file uploaded"));

            try
            {
                await _eventService.GetOwnedEntityAsync(id, CurrentUserId, IsAdmin);
                await using var stream = file.OpenReadStream();
                var upload = await cloudinary.UploadImageAsync(stream, file.FileName, $"events/{id}/banner");
                var result = await _eventService.SetBannerAsync(id, upload.SecureUrl, upload.PublicId, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result, "Banner uploaded"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{id:int}/banner")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> DeleteBanner(int id)
        {
            try
            {
                var result = await _eventService.DeleteBannerAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result, "Banner deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPut("{eventId}/orders/{orderId}/confirm-sale")]
        public async Task<IActionResult> ConfirmPaidOrderSale(
            int eventId,
            int orderId,
            [FromBody] ConfirmEventSaleRequestDTO request,
            [FromHeader(Name = "X-Internal-Api-Key")] string apiKey,
            [FromServices] IConfiguration configuration)
        {
            var expectedKey = configuration["EventApi:InternalApiKey"];

            if (string.IsNullOrWhiteSpace(expectedKey) ||
                !string.Equals(apiKey, expectedKey, StringComparison.Ordinal))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid service credentials."
                });
            }

            if (request.OrderId != orderId)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "OrderId in the URL does not match the request."
                });
            }

            try
            {
                await _eventService.ConfirmPaidOrderSaleAsync(eventId, request);

                return Ok(new
                {
                    success = true,
                    message = "Order sale recorded."
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }
        [HttpPut("{eventId}/ticket-types/{ticketTypeId}/release-returned")]
        public async Task<IActionResult> ReleaseReturnedTicket(
            int eventId, int ticketTypeId,
            [FromQuery] int quantity,
            [FromHeader(Name = "X-Internal-Api-Key")] string apiKey,
            [FromServices] IConfiguration configuration)
        {
            var expectedKey = configuration["EventApi:InternalApiKey"];
            if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(apiKey, expectedKey, StringComparison.Ordinal))
                return Unauthorized(new { success = false, message = "Invalid service credentials." });
            if (quantity <= 0) return BadRequest(new { success = false, message = "Quantity must be positive." });

            var released = await _eventService.ReleaseReturnedTicketAsync(eventId, ticketTypeId, quantity);
            return released
                ? Ok(new { success = true, message = "Returned ticket released back to inventory." })
                : NotFound(new { success = false, message = "Event ticket type not found." });
        }

    }
}
