using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
// Quan ly va ap dung cac mau so do ghe ngoi tai su dung danh cho Organizer va Admin
    [Route("api/seating-templates")]
    public class SeatingTemplatesController : BaseApiController
    {
        private readonly ISeatingTemplateService _templateService;

        public SeatingTemplatesController(ISeatingTemplateService templateService)
        {
            _templateService = templateService;
        }

        // Lay danh sach cac mau so do ghe co the su dung gom mau ca nhan va mau cong khai
        [HttpGet]
        [Authorize(Policy = "RequireCustomer")]
        public async Task<IActionResult> GetVisible()
        {
            var result = await _templateService.GetVisibleAsync(CurrentUserId, IsAdmin);
            return Ok(ApiResponseDTO<List<SeatingTemplateListDTO>>.SuccessResponse(result));
        }

        // Lay thong tin chi tiet mau so do ghe theo id danh cho Organizer va Admin
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

        // Luu so do ghe hien tai cua concert thanh mau mau de tai su dung danh cho Organizer
        [HttpPost("from-event")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> SaveFromEvent([FromBody] SaveSeatingTemplateDTO dto)
        {
            try
            {
                // Luu y: Chup snapshot toan bo khu vuc va layout cua concert thanh template moi
                var result = await _templateService.SaveFromEventAsync(dto, CurrentUserId, IsAdmin);
                return CreatedAtAction(nameof(GetById), new { id = result.SeatingTemplateId },
                    ApiResponseDTO<SeatingTemplateResponseDTO>.SuccessResponse(result, "Layout saved as a reusable template"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Tao mot mau so do ghe moi truc tiep tu canvas danh cho Organizer hoac Admin
        [HttpPost]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Create([FromBody] CreateSeatingTemplateDTO dto)
        {
            try
            {
                // Luu y: Chi Admin moi co quyen danh dau template la IsPublic
                var result = await _templateService.CreateAsync(dto, CurrentUserId, IsAdmin);
                return CreatedAtAction(nameof(GetById), new { id = result.SeatingTemplateId },
                    ApiResponseDTO<SeatingTemplateResponseDTO>.SuccessResponse(result, "Template created"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        // Cap nhat thong tin mau so do ghe danh cho chu so huu hoac Admin
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

        // Xoa mau so do ghe danh cho chu so huu hoac Admin
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

        // Ap dung mau so do ghe co san vao concert danh cho Organizer
        [HttpPost("{templateId:int}/apply/{eventId:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Apply(int templateId, int eventId, [FromBody] ApplySeatingTemplateDTO dto)
        {
            try
            {
                // Luu y: Anh xa tung zone trong template voi mot hang ve tuong ung cua concert
                var result = await _templateService.ApplyToEventAsync(templateId, eventId, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<SeatingChartResponseDTO>.SuccessResponse(
                    result, $"Template applied: {result.Zones.Count} zone(s) built."));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
