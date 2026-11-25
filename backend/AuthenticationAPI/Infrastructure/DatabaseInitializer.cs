using AuthenticationAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Infrastructure
{
    /// <summary>
    /// Start-up database work (idempotent schema patches + development seed data).
    /// Lives outside Program.cs so the composition root stays free of data access code.
    /// </summary>
    public static class DatabaseInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services)
        {
            using (var schemaScope = services.CreateScope())
            {
                var db = schemaScope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE users ADD COLUMN IF NOT EXISTS avatar_public_id text;");
                await db.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS tax_code VARCHAR(50);
                    ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS representative_name VARCHAR(200);

                    CREATE TABLE IF NOT EXISTS organizer_request_documents (
                        document_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        request_id INT NOT NULL REFERENCES organizer_requests(request_id) ON DELETE CASCADE,
                        document_type VARCHAR(50) NOT NULL,
                        file_name VARCHAR(255) NOT NULL,
                        file_url TEXT NOT NULL,
                        preview_url TEXT,
                        public_id VARCHAR(255),
                        is_image BOOLEAN NOT NULL DEFAULT TRUE,
                        uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                    );

                    ALTER TABLE organizer_request_documents ADD COLUMN IF NOT EXISTS public_id VARCHAR(255);
                    CREATE INDEX IF NOT EXISTS idx_organizer_request_documents_request_id ON organizer_request_documents(request_id);
                ");
            }

            using (var scope = services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();

                try
                {
                    if (!context.Roles.Any(r => r.RoleName == "Admin"))
                    {
                        context.Roles.Add(new Role { RoleName = "Admin" });
                        context.SaveChanges();
                        Console.WriteLine("Created role: Admin");
                    }

                    if (!context.Roles.Any(r => r.RoleName == "Customer"))
                    {
                        context.Roles.Add(new Role { RoleName = "Customer" });
                        context.SaveChanges();
                        Console.WriteLine("Created role: Customer");
                    }

                    // "Organizer" is granted additively (customer keeps Customer + gains Organizer)
                    // when an organizer request is approved.
                    if (!context.Roles.Any(r => r.RoleName == "Organizer"))
                    {
                        context.Roles.Add(new Role { RoleName = "Organizer" });
                        context.SaveChanges();
                        Console.WriteLine("Created role: Organizer");
                    }

                    var adminRole = context.Roles.First(r => r.RoleName == "Admin");
                    var adminEmail = "admin@ticketbox.com";

                    if (!context.Users.Any(u => u.Email == adminEmail))
                    {
                        var admin = new User
                        {
                            Email = adminEmail,
                            FullName = "System Administrator",
                            PhoneNumber = "0123456789",
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                            IsVerified = true,
                            IsActive = true,
                            EkycStatus = "NotSubmitted",
                            CreatedAt = DateTime.UtcNow,
                            AuthProvider = "local",
                            HasPassword = true
                        };

                        context.Users.Add(admin);
                        context.SaveChanges();

                        context.UserRoles.Add(new UserRole
                        {
                            UserId = admin.UserId,
                            RoleId = adminRole.RoleId,
                            AssignedAt = DateTime.UtcNow
                        });
                        context.SaveChanges();

                        Console.WriteLine("========================================");
                        Console.WriteLine("ADMIN ACCOUNT CREATED:");
                        Console.WriteLine($"    Email:    {adminEmail}");
                        Console.WriteLine($"    Password: Admin@123");
                        Console.WriteLine("========================================");
                    }
                    else
                    {
                        Console.WriteLine("Admin account already exists");
                    }

                    // Dev/test convenience account so the Create-Concert flow can be exercised
                    // end-to-end via Swagger without needing a live SMTP server for email OTP.
                    var customerRole = context.Roles.First(r => r.RoleName == "Customer");
                    var legacyUserRole = context.Roles.FirstOrDefault(r => r.RoleName == "User");
                    if (legacyUserRole != null)
                    {
                        var legacyCustomerIds = context.UserRoles
                            .Where(ur => ur.RoleId == legacyUserRole.RoleId)
                            .Select(ur => ur.UserId)
                            .ToList();
                        var alreadyCustomerIds = context.UserRoles
                            .Where(ur => ur.RoleId == customerRole.RoleId)
                            .Select(ur => ur.UserId)
                            .ToHashSet();
                        context.UserRoles.AddRange(legacyCustomerIds
                            .Where(id => !alreadyCustomerIds.Contains(id))
                            .Select(id => new UserRole
                            {
                                UserId = id,
                                RoleId = customerRole.RoleId,
                                AssignedAt = DateTime.UtcNow
                            }));
                        context.SaveChanges();
                    }
                    var testCustomerEmail = "customer@ticketbox.com";

                    if (!context.Users.Any(u => u.Email == testCustomerEmail))
                    {
                        var customer = new User
                        {
                            Email = testCustomerEmail,
                            FullName = "Test Customer",
                            PhoneNumber = "0987654321",
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                            IsVerified = true,
                            IsActive = true,
                            EkycStatus = "NotSubmitted",
                            CreatedAt = DateTime.UtcNow,
                            AuthProvider = "local",
                            HasPassword = true
                        };

                        context.Users.Add(customer);
                        context.SaveChanges();

                        context.UserRoles.Add(new UserRole
                        {
                            UserId = customer.UserId,
                            RoleId = customerRole.RoleId,
                            AssignedAt = DateTime.UtcNow
                        });
                        context.SaveChanges();

                        Console.WriteLine("========================================");
                        Console.WriteLine("TEST CUSTOMER ACCOUNT CREATED:");
                        Console.WriteLine($"    Email:    {testCustomerEmail}");
                        Console.WriteLine($"    Password: Customer@123");
                        Console.WriteLine("========================================");
                    }

                    var organizerRole = context.Roles.First(r => r.RoleName == "Organizer");
                    var testOrganizerEmail = "organizer@ticketbox.com";
                    if (!context.Users.Any(u => u.Email == testOrganizerEmail))
                    {
                        var organizer = new User
                        {
                            Email = testOrganizerEmail,
                            FullName = "Test Organizer",
                            PhoneNumber = "0977777777",
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Organizer@123"),
                            IsVerified = true,
                            IsActive = true,
                            EkycStatus = "NotSubmitted",
                            CreatedAt = DateTime.UtcNow,
                            AuthProvider = "local",
                            HasPassword = true
                        };

                        context.Users.Add(organizer);
                        context.SaveChanges();
                        context.UserRoles.AddRange(
                            new UserRole { UserId = organizer.UserId, RoleId = customerRole.RoleId, AssignedAt = DateTime.UtcNow },
                            new UserRole { UserId = organizer.UserId, RoleId = organizerRole.RoleId, AssignedAt = DateTime.UtcNow });
                        context.SaveChanges();

                        Console.WriteLine("TEST ORGANIZER ACCOUNT CREATED: organizer@ticketbox.com / Organizer@123");
                    }

                    // Additional Customers
                    var extraCustomers = new[]
                    {
                        ("customer1@ticketbox.com", "Customer One (Nguyen Van An)", "0981111111"),
                        ("customer2@ticketbox.com", "Customer Two (Tran Thi Binh)", "0982222222"),
                        ("customer3@ticketbox.com", "Customer Three (Le Van Cuong)", "0983333333"),
                        ("customer4@ticketbox.com", "Customer Four (Pham Thi Dung)", "0984444444"),
                        ("customer5@ticketbox.com", "Customer Five (Hoang Van Em)", "0985555555"),
                        ("customer6@ticketbox.com", "Customer Six (Vu Thi Giang)", "0986666666"),
                        ("customer7@ticketbox.com", "Customer Seven (Dang Van Hai)", "0987777777")
                    };
                    foreach (var (cEmail, cName, cPhone) in extraCustomers)
                    {
                        if (!context.Users.Any(u => u.Email == cEmail))
                        {
                            var cust = new User
                            {
                                Email = cEmail,
                                FullName = cName,
                                PhoneNumber = cPhone,
                                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                                IsVerified = true,
                                IsActive = true,
                                EkycStatus = "NotSubmitted",
                                CreatedAt = DateTime.UtcNow,
                                AuthProvider = "local",
                                HasPassword = true
                            };
                            context.Users.Add(cust);
                            context.SaveChanges();
                            context.UserRoles.Add(new UserRole { UserId = cust.UserId, RoleId = customerRole.RoleId, AssignedAt = DateTime.UtcNow });
                            context.SaveChanges();
                            Console.WriteLine($"CUSTOMER CREATED: {cEmail} / Customer@123");
                        }
                    }

                    // Additional Organizers
                    var extraOrganizers = new[]
                    {
                        ("organizer1@ticketbox.com", "Organizer One (Saigon Sound Entertainment)", "0911111111"),
                        ("organizer2@ticketbox.com", "Organizer Two (Hanoi Music Production)", "0912222222")
                    };
                    foreach (var (oEmail, oName, oPhone) in extraOrganizers)
                    {
                        if (!context.Users.Any(u => u.Email == oEmail))
                        {
                            var org = new User
                            {
                                Email = oEmail,
                                FullName = oName,
                                PhoneNumber = oPhone,
                                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Organizer@123"),
                                IsVerified = true,
                                IsActive = true,
                                EkycStatus = "NotSubmitted",
                                CreatedAt = DateTime.UtcNow,
                                AuthProvider = "local",
                                HasPassword = true
                            };
                            context.Users.Add(org);
                            context.SaveChanges();
                            context.UserRoles.AddRange(
                                new UserRole { UserId = org.UserId, RoleId = customerRole.RoleId, AssignedAt = DateTime.UtcNow },
                                new UserRole { UserId = org.UserId, RoleId = organizerRole.RoleId, AssignedAt = DateTime.UtcNow });
                            context.SaveChanges();
                            Console.WriteLine($"ORGANIZER CREATED: {oEmail} / Organizer@123");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Seed error: {ex.Message}");
                }
            }
        }
    }
}
