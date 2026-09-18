using System.Security.Claims;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    [ApiController]
    [Route("api/organizer-requests")]
    [Authorize]
    public class OrganizerRequestController : ControllerBase
    {
        private readonly IOrganizerRequestService _service;

        public OrganizerRequestController(IOrganizerRequestService service)
        {
            _service = service;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost]
        public async Task<IActionResult> CreateRequest([FromBody] CreateOrganizerRequestDTO dto)
        {
            try
            {
                var result = await _service.CreateRequestAsync(CurrentUserId, dto);
                return CreatedAtAction(nameof(GetById), new { requestId = result.RequestId }, result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyRequests()
        {
            var result = await _service.GetMyRequestsAsync(CurrentUserId);
            return Ok(result);
        }

        [HttpGet("{requestId:int}")]
        public async Task<IActionResult> GetById(int requestId)
        {
            try
            {
                var result = await _service.GetByIdAsync(requestId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll([FromQuery] string? status)
        {
            var result = await _service.GetAllRequestsAsync(status);
            return Ok(result);
        }

        [HttpPut("{requestId:int}/review")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Review(int requestId, [FromBody] ReviewOrganizerRequestDTO dto)
        {
            try
            {
                var result = await _service.ReviewRequestAsync(requestId, CurrentUserId, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}