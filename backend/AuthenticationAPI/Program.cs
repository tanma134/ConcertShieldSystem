
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập JWT token (không cần thêm 'Bearer ' phía trước)"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
var pgConn = builder.Configuration.GetConnectionString("MyConnection");
builder.Services.AddDbContext<AuthenticationDbContext>(options =>
    options.UseNpgsql(pgConn, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure())
);

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = ctx =>
        {
            Console.WriteLine("Jwt OnAuthenticationFailed: " + ctx.Exception?.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            Console.WriteLine("Jwt OnTokenValidated: " + ctx.Principal?.Identity?.Name);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", policy => policy.RequireRole("Customer"));
    options.AddPolicy("User", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("CanViewUsers", policy => policy.RequireRole("Admin", "Staff"));
    options.AddPolicy("CanManageUsers", policy => policy.RequireRole("Admin"));
});

//Them service
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IOrganizerRequestRepository, OrganizerRequestRepository>();
builder.Services.AddScoped<IOrganizerRequestService, OrganizerRequestService>();
builder.Services.AddScoped<IAvatarStorageService, AvatarStorageService>();



var app = builder.Build();

using (var schemaScope = app.Services.CreateScope())
{
    var db = schemaScope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE users ADD COLUMN IF NOT EXISTS avatar_public_id text;");
}
using (var scope = app.Services.CreateScope())
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
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Seed error: {ex.Message}");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
