using AuthenticationAPI.Models;

namespace AuthenticationAPI.UnitTests;

internal static class TestData
{
    internal static User CreateUser(int userId, params string[] roleNames)
    {
        var user = new User
        {
            UserId = userId,
            Email = $"user{userId}@example.com",
            PasswordHash = "test-hash",
            FullName = $"User {userId}",
            PhoneNumber = $"555000{userId:D4}",
            AvatarUrl = $"https://example.com/avatar/{userId}.png",
            IsVerified = true,
            EkycStatus = "Verified",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            AuthProvider = "Local",
            HasPassword = true
        };

        foreach (var roleName in roleNames)
        {
            var roleId = roleName switch
            {
                "Admin" => 1,
                "Customer" => 2,
                "Organizer" => 3,
                "Staff" => 4,
                _ => 99
            };
            user.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                Role = new Role { RoleId = roleId, RoleName = roleName },
                User = user
            });
        }

        return user;
    }

    internal static OrganizerRequest CreateOrganizerRequest(
        int requestId,
        string status,
        User user,
        int? reviewedBy = null,
        User? reviewer = null)
    {
        var timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(requestId);
        return new OrganizerRequest
        {
            RequestId = requestId,
            UserId = user.UserId,
            User = user,
            Reason = $"Reason {requestId}",
            CompanyName = $"Company {requestId}",
            Website = $"https://company{requestId}.example.com",
            PhoneNumber = "5551234567",
            Experience = $"Experience {requestId}",
            Status = status,
            ReviewedBy = reviewedBy,
            ReviewedByNavigation = reviewer,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }
}
