using AuthenticationAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Controllers
{
    // Lets an Organizer (or Admin) find accounts that hold the Staff role,
    // so they can be assigned to an event (UC_14.3 / UC_14.4).
    // Only id, name and email are returned; phone numbers and other data stay private.
    [ApiController]
    [Route("api/users/staff")]
    [AllowAnonymous]
    public class StaffDirectoryController : ControllerBase
    {
        private const string StaffRoleName = "Staff";
        private const int MaxResults = 50;

        private readonly AuthenticationDbContext _context;
        private readonly IConfiguration _configuration;

        public StaffDirectoryController(AuthenticationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET api/users/staff?q=an&ids=3,5
        // q   : optional text matched against name or email.
        // ids : optional comma separated user ids; only those accounts are returned.
        [HttpGet]
        public async Task<IActionResult> GetStaff([FromQuery] string? q, [FromQuery] string? ids)
        {
            var expectedKey = _configuration["InternalApiKey"];
            var suppliedKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
            var authenticatedCaller = User?.Identity?.IsAuthenticated == true;
            var trustedService = !string.IsNullOrWhiteSpace(expectedKey) && suppliedKey == expectedKey;
            if (!authenticatedCaller && !trustedService)
                return Unauthorized();

            var query = _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.RoleName == StaffRoleName));

            var wantedIds = ParseIds(ids);
            if (wantedIds.Count > 0)
                query = query.Where(u => wantedIds.Contains(u.UserId));

            var text = q?.Trim().ToLower();
            if (!string.IsNullOrEmpty(text))
                query = query.Where(u => u.FullName.ToLower().Contains(text) || u.Email.ToLower().Contains(text));

            var staff = await query
                .OrderBy(u => u.FullName)
                .Take(MaxResults)
                .Select(u => new { userId = u.UserId, fullName = u.FullName, email = u.Email })
                .ToListAsync();

            return Ok(staff);
        }

        // Turns "3,5,x,7" into [3, 5, 7]; anything that is not a number is ignored.
        private static List<int> ParseIds(string? ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
                return new List<int>();

            return ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(part, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .Take(MaxResults)
                .ToList();
        }
    }
}
