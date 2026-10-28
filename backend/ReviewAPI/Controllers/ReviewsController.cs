using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReviewAPI.DTOs;
using ReviewAPI.Services;

namespace ReviewAPI.Controllers;

[ApiController]
[Route("api/reviews")]
public sealed class ReviewsController(IReviewService service) : ControllerBase
{
    [HttpGet("event/{eventId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByEvent(int eventId) => Ok(await service.GetByEventAsync(eventId));

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetForAdmin([FromQuery] string? eventName, [FromQuery] string? status, [FromQuery] int? rating, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await service.GetForAdminAsync(eventName, status, rating, page, pageSize));

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(CreateReviewRequest request)
    {
        try { return CreatedAtAction(nameof(GetByEvent), new { eventId = request.EventId }, await service.CreateAsync(request, CurrentUserId, CurrentRoles)); }
        catch (Exception ex) { return Handle(ex); }
    }

    [HttpPut("{reviewId:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int reviewId, UpdateReviewRequest request)
    {
        try { return Ok(await service.UpdateAsync(reviewId, request, CurrentUserId, CurrentRoles)); }
        catch (Exception ex) { return Handle(ex); }
    }

    [HttpDelete("{reviewId:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int reviewId)
    {
        try { await service.DeleteAsync(reviewId, CurrentUserId, CurrentRoles); return Ok(new { message = "Review deleted successfully." }); }
        catch (Exception ex) { return Handle(ex); }
    }

    [HttpPut("admin/{reviewId:int}/visibility")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetVisibility(int reviewId, [FromBody] VisibilityRequest request)
    {
        try { await service.ModerateAsync(reviewId, request.Hidden, CurrentUserId); return Ok(new { message = request.Hidden ? "Review hidden successfully." : "Review shown successfully." }); }
        catch (Exception ex) { return Handle(ex); }
    }

    [HttpPost("{reviewId:int}/replies")]
    [Authorize]
    public async Task<IActionResult> Reply(int reviewId, CreateReplyRequest request)
    {
        try { return Ok(await service.ReplyAsync(reviewId, request, CurrentUserId, CurrentRoles)); }
        catch (Exception ex) { return Handle(ex); }
    }

    private int CurrentUserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return int.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("User id claim is missing.");
        }
    }

    private IReadOnlyCollection<string> CurrentRoles => User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();

    private IActionResult Handle(Exception exception) => exception switch
    {
        ArgumentException => BadRequest(new { message = exception.Message }),
        KeyNotFoundException => NotFound(new { message = exception.Message }),
        UnauthorizedAccessException => StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message }),
        InvalidOperationException => Conflict(new { message = exception.Message }),
        _ => StatusCode(500, new { message = "Review operation failed." })
    };
}

public sealed class VisibilityRequest
{
    public bool Hidden { get; set; }
}
