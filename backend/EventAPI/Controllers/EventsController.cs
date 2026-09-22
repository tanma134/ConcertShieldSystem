using EventAPI.DTOs;
using EventAPI.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventAPI.Common;

namespace EventAPI.Controllers
{
    /// <summary>
    /// Concert lifecycle: Draft -> Pending -> Published, plus Pending -> Rejected
    /// and Rejected -> edit -> Pending.
    ///
    /// Every concert is a Music concert (CategoryId = 1); the category cannot be chosen.
    /// Only an approved Organizer may create and configure a concert.
    /// </summary>
    [Route("api/events")]
    public class EventsController : BaseApiController
    {
        private readonly IEventService _eventService;
        private readonly IValidator<CreateEventDTO> _createValidator;
        private readonly IValidator<UpdateEventDTO> _updateValidator;
        private readonly ILogger<EventsController> _logger;

        public EventsController(
            IEventService eventService,
            IValidator<CreateEventDTO> createValidator,
            IValidator<UpdateEventDTO> updateValidator,
            ILogger<EventsController> logger)
        {
            _eventService = eventService;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _logger = logger;
        }

        // =====================================================================
        // PUBLIC READS — Published concerts only
        // =====================================================================

        /// <summary>Public list/search/filter. Only Published concerts are returned.</summary>
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

        /// <summary>Normalizes a slug and checks uniqueness while the user is typing.</summary>
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

        /// <summary>Public concert detail by slug. 404 unless Published.</summary>
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

        /// <summary>Public concert detail by id. 404 unless Published.</summary>
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var result = await _eventService.GetByIdAsync(id, incrementView: true, publicOnly: true);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // =====================================================================
        // ORGANIZER — my concerts
        // =====================================================================

        /// <summary>Every concert I own, in any status (Draft included).</summary>
        [HttpGet("mine")]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> GetMine()
        {
            var result = await _eventService.GetByOrganizerIdAsync(CurrentUserId);
            return Ok(ApiResponseDTO<List<EventListDTO>>.SuccessResponse(result));
        }

        /// <summary>
        /// Organizer dashboard summary: event counts by status, tickets sold, and
        /// gross ticket revenue (price * soldQuantity, not adjusted for refunds).
        /// </summary>
        [HttpGet("mine/dashboard")]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> GetMyDashboard()
        {
            var result = await _eventService.GetOrganizerDashboardAsync(CurrentUserId);
            return Ok(ApiResponseDTO<OrganizerDashboardSummaryDTO>.SuccessResponse(result));
        }

        /// <summary>
        /// One of my concerts in full (incl. seating chart) regardless of status.
        /// Use this instead of GET /{id} while the concert is still a Draft.
        /// </summary>
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

        // =====================================================================
        // ORGANIZER — create / update / delete draft
        // =====================================================================

        /// <summary>
        /// Creates the concert as a Draft for an approved Organizer.
        /// </summary>
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

        /// <summary>Updates a Draft or Rejected concert. Partial — send only what changes.</summary>
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
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Concert deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("{id:int}/restore")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> Restore(int id)
        {
            await _eventService.RestoreAsync(id);
            return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Concert restored"));
        }

        // =====================================================================
        // SUBMIT FOR APPROVAL
        // =====================================================================

        /// <summary>
        /// Dry-run of the publication checks. Returns the same error list Submit
        /// would produce, so the UI can show what's still missing.
        /// </summary>
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

        /// <summary>
        /// Draft/Rejected -> Pending. Validates all publication data first; on failure
        /// returns 400 with every missing field listed. Also used to RESUBMIT a
        /// rejected concert after editing.
        /// </summary>
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
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                var result = await _eventService.CancelAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result, "Concert cancelled"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // =====================================================================
        // ADMIN — moderation
        // =====================================================================

        /// <summary>Concerts waiting for approval, oldest submission first.</summary>
        [HttpGet("pending")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> GetPending([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
        {
            var result = await _eventService.GetPendingAsync(page, pageSize);
            return Ok(ApiResponseDTO<PagedResultDTO<PendingEventDTO>>.SuccessResponse(result));
        }

        /// <summary>
        /// All concerts regardless of status, for the Admin "All Events" page.
        /// Supports the same search/filter/sort/pagination as the public list
        /// (status, creator/OrganizerId, date range, city), but is never limited
        /// to Published — the pending queue alone isn't enough for moderation.
        /// </summary>
        [HttpGet("admin")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> AdminGetAll([FromQuery] EventFilterDTO filter)
        {
            filter.PublishedOnly = false;
            var result = await _eventService.GetFilteredAsync(filter);
            return Ok(ApiResponseDTO<PagedResultDTO<EventListDTO>>.SuccessResponse(result));
        }

        /// <summary>Alias kept for the existing admin UI.</summary>
        [HttpGet("moderation/queue")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> ModerationQueue([FromQuery] int page = 1, [FromQuery] int pageSize = 12)
            => await GetPending(page, pageSize);

        /// <summary>
        /// Full detail of any concert regardless of status, so the Admin can review
        /// a Pending submission before deciding.
        /// </summary>
        [HttpGet("admin/{id:int}")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> AdminGetById(int id)
        {
            try
            {
                var result = await _eventService.GetByIdAsync(id, incrementView: false, publicOnly: false);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        /// <summary>
        /// Pending -> Published. Organizer role approval is handled separately by
        /// the Organizer Request workflow in AuthenticationAPI.
        /// </summary>
        [HttpPost("{id:int}/approve")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                var (result, roleGrant) = await _eventService.ApproveAsync(
                    id, CurrentUserId, BearerToken);

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

        /// <summary>Pending -> Rejected. A reason is mandatory.</summary>
        [HttpPost("{id:int}/reject")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> Reject(int id, [FromBody] RejectEventDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Reason))
                return BadRequest(ApiResponseDTO<object>.FailResponse("A rejection reason is required."));

            try
            {
                var result = await _eventService.RejectAsync(id, dto.Reason, CurrentUserId);
                return Ok(ApiResponseDTO<EventResponseDTO>.SuccessResponse(result, "Concert rejected"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // =====================================================================
        // POSTER / BANNER (Cloudinary)
        // =====================================================================

        /// <summary>Uploads or replaces the poster. Replacing removes the old Cloudinary asset.</summary>
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

        /// <summary>Uploads or replaces the banner. Replacing removes the old Cloudinary asset.</summary>
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
    }
}
