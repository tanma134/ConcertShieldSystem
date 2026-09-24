using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    /// <summary>
    /// Reusable venue layouts an organizer/admin can draw once and apply to many
    /// concerts — the "canvas + template library" workflow requested for the
    /// seating chart builder.
    ///
    ///   UC_26.3 Save Seating Chart as Template — snapshot an already-built concert
    ///           layout into the reusable library.
    ///   UC_26.2 Apply Seating Template        — load a template into a (Draft or
    ///           Rejected) concert, mapping each template zone onto one of that
    ///           concert's ticket types.
    ///
    /// A template is visible to its owner and to any Admin, plus everyone once it is
    /// marked IsPublic (admin-provided starter templates such as "Standard theater").
    /// </summary>
    [Route("api/seating-templates")]
    public class SeatingTemplatesController : BaseApiController
    {
        private readonly ISeatingTemplateService _templateService;

        public SeatingTemplatesController(ISeatingTemplateService templateService)
        {
            _templateService = templateService;
        }

        /// <summary>Templates I can use: my own plus every public (admin-provided) one.</summary>
        [HttpGet]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> GetVisible()
        {
            var result = await _templateService.GetVisibleAsync(CurrentUserId, IsAdmin);
            return Ok(ApiResponseDTO<List<SeatingTemplateListDTO>>.SuccessResponse(result));
        }

        [HttpGet("{id:int}")]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var result = await _templateService.GetByIdAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<SeatingTemplateResponseDTO>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        /// <summary>UC_26.3 — snapshot the current, already-built layout of one of my concerts.</summary>
        [HttpPost("from-event")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> SaveFromEvent([FromBody] SaveSeatingTemplateDTO dto)
        {
            try
            {
                var result = await _templateService.SaveFromEventAsync(dto, CurrentUserId, IsAdmin);
                return CreatedAtAction(nameof(GetById), new { id = result.SeatingTemplateId },
                    ApiResponseDTO<SeatingTemplateResponseDTO>.SuccessResponse(result, "Layout saved as a reusable template"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        /// <summary>Draw-from-scratch template, not tied to any concert.</summary>
        [HttpPost]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Create([FromBody] CreateSeatingTemplateDTO dto)
        {
            try
            {
                var result = await _templateService.CreateAsync(dto, CurrentUserId, IsAdmin);
                return CreatedAtAction(nameof(GetById), new { id = result.SeatingTemplateId },
                    ApiResponseDTO<SeatingTemplateResponseDTO>.SuccessResponse(result, "Template created"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSeatingTemplateDTO dto)
        {
            try
            {
                var result = await _templateService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<SeatingTemplateResponseDTO>.SuccessResponse(result, "Template updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _templateService.DeleteAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Template deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        /// <summary>
        /// UC_26.2 — builds the concert's seating chart from the template, after the
        /// organizer maps each template zone to one of the concert's ticket types.
        /// </summary>
        [HttpPost("{templateId:int}/apply/{eventId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Apply(int templateId, int eventId, [FromBody] ApplySeatingTemplateDTO dto)
        {
            try
            {
                var result = await _templateService.ApplyToEventAsync(templateId, eventId, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<SeatingChartResponseDTO>.SuccessResponse(
                    result, $"Template applied: {result.Zones.Count} zone(s) built."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
