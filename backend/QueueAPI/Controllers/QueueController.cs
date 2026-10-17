using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueAPI.DTOs;
using QueueAPI.Services;

namespace QueueAPI.Controllers;

[ApiController]
[Route("api/queue")]
public sealed class QueueController(IQueueService service) : ControllerBase
{
    private int UserId => int.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
        out var userId)
        ? userId
        : throw new UnauthorizedAccessException("UNAUTHORIZED");

    [HttpPost("{eventId:int}/prequeue/register")]
    [Authorize(Policy = "Customer")]
    public async Task<ActionResult<QueueStatusResponse>> Register(int eventId) => await Execute(() => service.RegisterAsync(eventId, UserId));

    [HttpPost("{eventId:int}/join")]
    [Authorize(Policy = "Customer")]
    public async Task<ActionResult<QueueStatusResponse>> Join(int eventId, [FromHeader(Name = "X-Queue-Token")] string? token = null) => await Execute(() => service.JoinAsync(eventId, UserId, token));

    [HttpGet("{eventId:int}/position")]
    [HttpGet("{eventId:int}/status")]
    [Authorize(Policy = "Customer")]
    public async Task<ActionResult<QueueStatusResponse>> Status(int eventId, [FromHeader(Name = "X-Queue-Token")] string? token = null) => await Execute(() => service.GetStatusAsync(eventId, UserId, token));

    [HttpDelete("{eventId:int}/leave")]
    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> Leave(int eventId, [FromHeader(Name = "X-Queue-Token")] string? token = null)
    {
        try { await service.LeaveAsync(eventId, UserId, token); return NoContent(); }
        catch (Exception ex) { return ProblemFor(ex); }
    }

    [HttpPost("{eventId:int}/prequeue/configure")]
    [Authorize(Policy = "QueueOrganizer")]
    public async Task<ActionResult<ConfigureQueueResponse>> Configure(int eventId, ConfigureQueueRequest request) => await Execute(() => service.ConfigureAsync(eventId, request, UserId));

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); } catch (Exception ex) { return ProblemFor(ex); }
    }

    private ActionResult ProblemFor(Exception ex) => ex switch
    {
        KeyNotFoundException => NotFound(new { code = ex.Message }),
        UnauthorizedAccessException => Unauthorized(new { code = ex.Message }),
        ArgumentException => BadRequest(new { code = ex.Message }),
        InvalidOperationException => Conflict(new { code = ex.Message }),
        EventApiUnavailableException => StatusCode(503, new { code = ex.Message }),
        _ => StatusCode(500, new { code = "SERVER_ERROR" })
    };
}
