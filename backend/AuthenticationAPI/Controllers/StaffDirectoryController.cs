using AuthenticationAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Controllers
{
    // Lets an Organizer (or Admin) find accounts that hold the Staff role,
    // so they can be assigned to an event ( / ).
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

        // GET api/users/staff?q=an&ids=3,5&organizerId=14
        // q           : optional text matched against name or email.
        // ids         : optional comma separated user ids; only those accounts are returned.
        // organizerId : optional organizer ID to limit candidates to staff created/managed by this organizer.
        [HttpGet]
        public async Task<IActionResult> GetStaff([FromQuery] string? q, [FromQuery] string? ids, [FromQuery] int? organizerId)
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

            if (organizerId.HasValue && organizerId.Value > 0)
            {
                var orgStaffIds = new List<int>();
                try
                {
                    var conn = _context.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open)
                        await conn.OpenAsync();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT staff_user_id FROM public.organizer_staff_members WHERE organizer_id = @orgId";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@orgId";
                    p.Value = organizerId.Value;
                    cmd.Parameters.Add(p);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        orgStaffIds.Add(reader.GetInt32(0));
                    }
                }
                catch { }

                if (orgStaffIds.Count == 0)
                {
                    return Ok(new List<object>());
                }

                query = query.Where(u => orgStaffIds.Contains(u.UserId));
            }

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

        // GET api/users/staff/accounts?ids=3,5
        // Internal lookup used by AdminAPI before blocking an account: answers
        // "do these user ids exist, and which are active?" Only id/active/roles
        // are returned so no personal data leaves AuthenticationAPI.
        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts([FromQuery] string? ids)
        {
            var expectedKey = _configuration["InternalApiKey"];
            var suppliedKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
            var authenticatedCaller = User?.Identity?.IsAuthenticated == true;
            var trustedService = !string.IsNullOrWhiteSpace(expectedKey) && suppliedKey == expectedKey;
            if (!authenticatedCaller && !trustedService)
                return Unauthorized();

            var wantedIds = ParseIds(ids);
            if (wantedIds.Count == 0)
                return Ok(Array.Empty<object>());

            var accounts = await _context.Users
                .AsNoTracking()
                .Where(u => wantedIds.Contains(u.UserId))
                .Select(u => new
                {
                    userId = u.UserId,
                    isActive = u.IsActive,
                    roles = u.UserRoles.Select(ur => ur.Role.RoleName).ToList()
                })
                .ToListAsync();

            return Ok(accounts);
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
