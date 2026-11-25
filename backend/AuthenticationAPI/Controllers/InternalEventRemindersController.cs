using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Controllers;

[ApiController]
[Route("api/internal/event-reminders")]
[AllowAnonymous]
public sealed class InternalEventRemindersController(
    AuthenticationDbContext db,
    IEmailService emailService,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] EventReminderEmailDTO request, CancellationToken cancellationToken)
    {
        var expectedKey = configuration["InternalApiKey"];
        if (string.IsNullOrWhiteSpace(expectedKey) || Request.Headers["X-Internal-Api-Key"] != expectedKey)
            return Unauthorized();

        if (request.StartsAt == default)
            return BadRequest(new { message = "A valid event start time is required." });

        var user = await db.Users
            .AsNoTracking()
            .Where(x => x.UserId == request.UserId && x.IsActive)
            .Select(x => new { x.Email, x.FullName })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
            return NoContent();

        await emailService.SendEventReminderAsync(
            user.Email,
            user.FullName,
            request.EventTitle,
            request.EventSlug,
            request.StartsAt,
            request.Timezone,
            cancellationToken);

        return Ok(new { success = true });
    }
}
