using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    [Route("api/seating-templates")]
    public class SeatingTemplatesController : BaseApiController
    {
        private readonly ISeatingTemplateService _templateService;

        public SeatingTemplatesController(ISeatingTemplateService templateService)
        {
            _templateService = templateService;
        }

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
