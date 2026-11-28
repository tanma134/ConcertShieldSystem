using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Controllers
{
    [ApiController]
    [Route("api/organizer/staff")]
    [Authorize]
    public class OrganizerStaffController : ControllerBase
    {
        private const string StaffRoleName = "Staff";
        private readonly AuthenticationDbContext _context;
        private readonly AuthenticationAPI.Services.IAuditLogClient _auditLog;

        public OrganizerStaffController(AuthenticationDbContext context, AuthenticationAPI.Services.IAuditLogClient auditLog)
        {
            _context = context;
            _auditLog = auditLog;
        }

        private Task AuditAsync(string action, int staffUserId, object? newValue = null) =>
            _auditLog.LogAsync(GetCurrentUserId() is var id and > 0 ? id : null,
                User.IsInRole("Admin") || User.IsInRole("admin") ? "admin" : "organizer",
                action, "User", staffUserId.ToString(), newValue: newValue,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

        private int GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? User.FindFirstValue("userId");
            return int.TryParse(value, out var id) ? id : 0;
        }

        private static bool UserHasRole(User user, string roleName) =>
            user.UserRoles.Any(ur =>
                string.Equals(ur.Role?.RoleName, roleName, StringComparison.OrdinalIgnoreCase));

        private bool IsAuthorizedOrganizerOrAdmin()
        {
            return User.IsInRole("Organizer") || User.IsInRole("organizer")
                || User.IsInRole("Admin") || User.IsInRole("admin")
                || User.Claims.Any(c => c.Type == ClaimTypes.Role && (c.Value.Equals("Organizer", StringComparison.OrdinalIgnoreCase) || c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase)));
        }

        private async Task EnsureOrganizerStaffTableAsync()
        {
            try
            {
                var sql = @"
                    CREATE TABLE IF NOT EXISTS public.organizer_staff_members (
                        organizer_id integer NOT NULL,
                        staff_user_id integer NOT NULL,
                        created_at timestamp with time zone DEFAULT CURRENT_TIMESTAMP,
                        CONSTRAINT pk_organizer_staff_members PRIMARY KEY (organizer_id, staff_user_id)
                    );
                    CREATE INDEX IF NOT EXISTS ix_organizer_staff_organizer ON public.organizer_staff_members(organizer_id);
                    CREATE INDEX IF NOT EXISTS ix_organizer_staff_staff ON public.organizer_staff_members(staff_user_id);
                ";
                await _context.Database.ExecuteSqlRawAsync(sql);
            }
            catch
            {
                // Ignore if already created or permission issue
            }
        }

        /// <summary>
        /// GET api/organizer/staff
        /// Lấy danh sách tài khoản Staff do Organizer quản lý (hoặc tất cả nếu là Admin)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStaffList([FromQuery] string? q)
        {
            if (!IsAuthorizedOrganizerOrAdmin())
                return StatusCode(403, new { success = false, message = "Access denied: Only Organizers or Administrators can manage staff." });

            var organizerId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("admin")
                       || User.Claims.Any(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            await EnsureOrganizerStaffTableAsync();

            List<int> managedStaffIds = new();
            if (!isAdmin && organizerId > 0)
            {
                try
                {
                    var conn = _context.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open)
                        await conn.OpenAsync();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT staff_user_id FROM public.organizer_staff_members WHERE organizer_id = @orgId";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@orgId";
                    p.Value = organizerId;
                    cmd.Parameters.Add(p);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        managedStaffIds.Add(reader.GetInt32(0));
                    }
                }
                catch
                {
                    managedStaffIds = new List<int>();
                }
            }

            // Nếu không phải Admin và Organizer chưa tạo bất kỳ staff nào -> trả về danh sách rỗng, KHÔNG lấy staff của người khác
            if (!isAdmin && managedStaffIds.Count == 0)
            {
                return Ok(new { success = true, data = new List<OrganizerStaffItemDTO>() });
            }

            var query = _context.Users
                .AsNoTracking()
                .Where(u =>
                    u.UserRoles.Any(ur => ur.Role.RoleName == StaffRoleName) &&
                    !u.UserRoles.Any(ur => ur.Role.RoleName.ToLower() == "admin"));

            if (!isAdmin)
            {
                query = query.Where(u => managedStaffIds.Contains(u.UserId));
            }

            var search = q?.Trim().ToLower();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.FullName.ToLower().Contains(search) || u.Email.ToLower().Contains(search) || (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
            }

            var staffList = await query
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new OrganizerStaffItemDTO
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    AuthProvider = u.AuthProvider
                })
                .ToListAsync();

            return Ok(new { success = true, data = staffList });
        }

        /// <summary>
        /// POST api/organizer/staff
        /// Organizer tạo tài khoản nhân viên Staff mới
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateStaff([FromBody] CreateOrganizerStaffDTO dto)
        {
            if (!IsAuthorizedOrganizerOrAdmin())
                return StatusCode(403, new { success = false, message = "Access denied: Only Organizers or Administrators can create staff accounts." });

            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid input data.", errors = ModelState });

            var organizerId = GetCurrentUserId();
            var emailNormalized = dto.Email.Trim().ToLower();

            var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == emailNormalized);
            if (emailExists)
                return BadRequest(new { success = false, message = "An account with this email already exists in the system." });

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                var phoneClean = dto.PhoneNumber.Trim();
                var phoneExists = await _context.Users.AnyAsync(u => u.PhoneNumber == phoneClean);
                if (phoneExists)
                    return BadRequest(new { success = false, message = "This phone number is already registered to another account." });
            }

            // Tìm hoặc tạo Role Staff
            var staffRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == StaffRoleName);
            if (staffRole == null)
            {
                staffRole = new Role { RoleName = StaffRoleName };
                _context.Roles.Add(staffRole);
                await _context.SaveChangesAsync();
            }

            var newUser = new User
            {
                Email = emailNormalized,
                FullName = dto.FullName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsVerified = true,
                IsActive = true,
                HasPassword = true,
                AuthProvider = "local",
                CreatedAt = DateTime.UtcNow,
                EkycStatus = "None"
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Gán role Staff
            var userRole = new UserRole
            {
                UserId = newUser.UserId,
                RoleId = staffRole.RoleId,
                AssignedAt = DateTime.UtcNow
            };
            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();

            // Lưu liên kết Organizer -> Staff
            await EnsureOrganizerStaffTableAsync();
            if (organizerId > 0)
            {
                try
                {
                    var conn = _context.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open)
                        await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO public.organizer_staff_members (organizer_id, staff_user_id, created_at) VALUES (@orgId, @staffId, NOW()) ON CONFLICT DO NOTHING;";
                    var p1 = cmd.CreateParameter(); p1.ParameterName = "@orgId"; p1.Value = organizerId; cmd.Parameters.Add(p1);
                    var p2 = cmd.CreateParameter(); p2.ParameterName = "@staffId"; p2.Value = newUser.UserId; cmd.Parameters.Add(p2);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch
                {
                    // Ignore mapping insert error
                }
            }

            var result = new OrganizerStaffItemDTO
            {
                UserId = newUser.UserId,
                FullName = newUser.FullName,
                Email = newUser.Email,
                PhoneNumber = newUser.PhoneNumber,
                IsActive = newUser.IsActive,
                CreatedAt = newUser.CreatedAt,
                AuthProvider = newUser.AuthProvider
            };

            await AuditAsync("ORG_STAFF_CREATED", newUser.UserId, new { email = newUser.Email });

            return CreatedAtAction(nameof(GetStaffList), new { id = newUser.UserId }, new { success = true, data = result, message = "Staff account created successfully." });
        }

        /// <summary>
        /// PUT api/organizer/staff/{staffUserId}
        /// Chỉnh sửa thông tin, đặt lại mật khẩu hoặc cập nhật trạng thái hoạt động của Staff
        /// </summary>
        [HttpPut("{staffUserId:int}")]
        public async Task<IActionResult> UpdateStaff(int staffUserId, [FromBody] UpdateOrganizerStaffDTO dto)
        {
            if (!IsAuthorizedOrganizerOrAdmin())
                return StatusCode(403, new { success = false, message = "Access denied: Only Organizers or Administrators can modify staff accounts." });

            var organizerId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("admin");

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserId == staffUserId);

            if (user == null)
                return NotFound(new { success = false, message = "Staff user not found." });

            if (UserHasRole(user, "Admin"))
                return Conflict(new { success = false, code = "ADMIN_ACCOUNT_PROTECTED", message = "Admin accounts cannot be modified." });

            if (!UserHasRole(user, StaffRoleName))
                return NotFound(new { success = false, message = "Staff user not found." });

            await EnsureOrganizerStaffTableAsync();

            // Nếu không phải Admin, kiểm tra quyền sở hữu staff
            if (!isAdmin && organizerId > 0)
            {
                try
                {
                    var conn = _context.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open)
                        await conn.OpenAsync();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM public.organizer_staff_members WHERE organizer_id = @orgId AND staff_user_id = @staffId";
                    var p1 = cmd.CreateParameter(); p1.ParameterName = "@orgId"; p1.Value = organizerId; cmd.Parameters.Add(p1);
                    var p2 = cmd.CreateParameter(); p2.ParameterName = "@staffId"; p2.Value = staffUserId; cmd.Parameters.Add(p2);
                    var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    if (count == 0)
                    {
                        using var insertCmd = conn.CreateCommand();
                        insertCmd.CommandText = "INSERT INTO public.organizer_staff_members (organizer_id, staff_user_id, created_at) VALUES (@orgId, @staffId, NOW()) ON CONFLICT DO NOTHING;";
                        var ip1 = insertCmd.CreateParameter(); ip1.ParameterName = "@orgId"; ip1.Value = organizerId; insertCmd.Parameters.Add(ip1);
                        var ip2 = insertCmd.CreateParameter(); ip2.ParameterName = "@staffId"; ip2.Value = staffUserId; insertCmd.Parameters.Add(ip2);
                        await insertCmd.ExecuteNonQueryAsync();
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName.Trim();

            if (dto.PhoneNumber != null)
            {
                var newPhone = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
                if (newPhone != null && newPhone != user.PhoneNumber)
                {
                    var phoneExists = await _context.Users.AnyAsync(u => u.PhoneNumber == newPhone && u.UserId != staffUserId);
                    if (phoneExists)
                        return BadRequest(new { success = false, message = "This phone number is already registered to another account." });
                }
                user.PhoneNumber = newPhone;
            }

            if (!string.IsNullOrWhiteSpace(dto.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            if (dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            await _context.SaveChangesAsync();
            await AuditAsync("ORG_STAFF_UPDATED", staffUserId, new
            {
                fullName = dto.FullName,
                phoneChanged = dto.PhoneNumber != null,
                passwordReset = !string.IsNullOrWhiteSpace(dto.Password),
                isActive = dto.IsActive
            });

            return Ok(new
            {
                success = true,
                data = new OrganizerStaffItemDTO
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    AuthProvider = user.AuthProvider
                },
                message = "Staff account updated successfully."
            });
        }

        /// <summary>
        /// DELETE api/organizer/staff/{staffUserId}
        /// Vô hiệu hoá / xoá tài khoản staff
        /// </summary>
        [HttpDelete("{staffUserId:int}")]
        public async Task<IActionResult> DeleteStaff(int staffUserId)
        {
            if (!IsAuthorizedOrganizerOrAdmin())
                return StatusCode(403, new { success = false, message = "Access denied: Only Organizers or Administrators can delete staff accounts." });

            var organizerId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("admin");

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.UserId == staffUserId);

            if (user == null)
                return NotFound(new { success = false, message = "Staff user not found." });

            if (UserHasRole(user, "Admin"))
                return Conflict(new { success = false, code = "ADMIN_ACCOUNT_PROTECTED", message = "Admin accounts cannot be deleted." });

            if (!UserHasRole(user, StaffRoleName))
                return NotFound(new { success = false, message = "Staff user not found." });

            // Đặt IsActive = false (soft-delete / khoá tài khoản)
            user.IsActive = false;
            await _context.SaveChangesAsync();

            await EnsureOrganizerStaffTableAsync();
            if (organizerId > 0)
            {
                try
                {
                    var conn = _context.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open)
                        await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM public.organizer_staff_members WHERE organizer_id = @orgId AND staff_user_id = @staffId";
                    var p1 = cmd.CreateParameter(); p1.ParameterName = "@orgId"; p1.Value = organizerId; cmd.Parameters.Add(p1);
                    var p2 = cmd.CreateParameter(); p2.ParameterName = "@staffId"; p2.Value = staffUserId; cmd.Parameters.Add(p2);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch { }
            }

            await AuditAsync("ORG_STAFF_REMOVED", staffUserId);
            return Ok(new { success = true, message = "Staff account deactivated successfully." });
        }
    }
}
