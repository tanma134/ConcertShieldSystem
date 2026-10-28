using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers;

[Route("api/wishlist")]
[Authorize(Policy = "RequireCustomer")]
public sealed class WishlistController(IWishlistService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetMine() => Ok(ApiResponseDTO<List<MyWishlistDTO>>.SuccessResponse(await service.GetMineAsync(CurrentUserId)));

    [HttpGet("events/{eventId:int}/exists")]
    public async Task<IActionResult> GetStatus(int eventId) => Ok(ApiResponseDTO<WishlistStatusDTO>.SuccessResponse(await service.GetStatusAsync(CurrentUserId, eventId)));

    [HttpPost("events/{eventId:int}")]
    public async Task<IActionResult> Add(int eventId)
    {
        try { await service.AddAsync(CurrentUserId, eventId); return Ok(ApiResponseDTO<object>.SuccessResponse(new { eventId }, "Event added to your wishlist.")); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpDelete("events/{eventId:int}")]
    public async Task<IActionResult> Remove(int eventId)
    {
        try { await service.RemoveAsync(CurrentUserId, eventId); return Ok(ApiResponseDTO<object>.SuccessResponse(new { eventId }, "Event removed from your wishlist.")); }
        catch (Exception ex) { return HandleException(ex); }
    }

    [HttpGet("admin/summary")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> AdminSummary([FromQuery] string? search, [FromQuery] string? sort)
        => Ok(ApiResponseDTO<List<AdminWishlistSummaryDTO>>.SuccessResponse(await service.GetAdminSummaryAsync(search, sort)));
}
