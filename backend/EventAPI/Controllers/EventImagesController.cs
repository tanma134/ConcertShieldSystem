using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    [Route("api/eventimages")]
    public class EventImagesController : BaseApiController
    {
        private readonly IEventImageService _imageService;

        public EventImagesController(IEventImageService imageService)
        {
            _imageService = imageService;
        }

        [HttpGet("event/{eventId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            try
            {
                var result = await _imageService.GetByEventIdAsync(eventId, CurrentUserIdOrNull, IsAdmin);
                return Ok(ApiResponseDTO<List<EventImageResponseDTO>>.SuccessResponse(result));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("event/{eventId:int}/upload")]
        [Authorize(Policy = "RequireOrganizer")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Upload(int eventId, IFormFile file)
        {
            try
            {
                var result = await _imageService.UploadAsync(eventId, file, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<EventImageResponseDTO>.SuccessResponse(result, "Image uploaded"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("event/{eventId:int}/upload-multiple")]
        [Authorize(Policy = "RequireOrganizer")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> UploadMultiple(int eventId, List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest(ApiResponseDTO<object>.FailResponse("No files uploaded"));

            try
            {
                var result = await _imageService.UploadMultipleAsync(eventId, files, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<List<EventImageResponseDTO>>.SuccessResponse(result, $"{result.Count} image(s) uploaded"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateImageDTO dto)
        {
            try
            {
                var result = await _imageService.UpdateAsync(id, dto, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<EventImageResponseDTO>.SuccessResponse(result, "Image updated"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPost("{id:int}/set-main")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> SetMain(int id)
        {
            try
            {
                await _imageService.SetMainAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Main image set"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpPut("reorder")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Reorder([FromQuery] int eventId, [FromBody] List<ReorderImageDTO> orders)
        {
            try
            {
                await _imageService.ReorderAsync(eventId, orders, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Images reordered"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "RequireOrganizer")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _imageService.DeleteAsync(id, CurrentUserId, IsAdmin);
                return Ok(ApiResponseDTO<object>.SuccessResponse(null!, "Image deleted"));
            }
            catch (Exception ex) { return HandleException(ex); }
        }
    }
}
