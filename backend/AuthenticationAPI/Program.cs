using AuthenticationAPI.Infrastructure;
using AuthenticationAPI.Models;
using AuthenticationAPI.Providers;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Risk;
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
builder.Services.AddScoped<IEkycRepository, EkycRepository>();
builder.Services.AddScoped<IKycAccessLogRepository, KycAccessLogRepository>();
builder.Services.AddScoped<IKycSettingRepository, KycSettingRepository>();
builder.Services.AddScoped<IKycConsentRepository, KycConsentRepository>();
builder.Services.AddScoped<IKycDeletionRepository, KycDeletionRepository>();
builder.Services.AddScoped<IKycAccessLogService, KycAccessLogService>();
builder.Services.AddScoped<IKycSettingService, KycSettingService>();
builder.Services.AddScoped<IKycConsentService, KycConsentService>();
builder.Services.AddScoped<IKycDataPurger, KycDataPurger>();
builder.Services.AddScoped<IKycDeletionService, KycDeletionService>();
builder.Services.AddScoped<IKycReviewService, KycReviewService>();

// eKYC VNPT: đăng ký HttpClient kèm header Token-id / Token-key / Authorization / mac-address
// (đọc từ section "Vnpt" trong appsettings). Thay cho AddHttpClient<IEkycProvider, VnptEkycProvider>() cũ.
builder.Services.AddVnptEkyc(builder.Configuration);

builder.Services.AddScoped<IKycService, KycService>();
builder.Services.AddScoped<IObjectStorage, EncryptedFileObjectStorage>();
builder.Services.AddScoped<ICccdDataProtector, AesDataProtector>();
builder.Services.AddHostedService<KycRetentionService>();
builder.Services.AddScoped<IAvatarStorageService, AvatarStorageService>();
builder.Services.AddScoped<IDocumentStorageService, DocumentStorageService>();

builder.Services.AddHttpClient<IAuditLogClient, AuditLogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AdminApi:BaseUrl"]
        ?? "https://localhost:7076");
    client.DefaultRequestHeaders.Add("X-Internal-Api-Key", builder.Configuration["InternalApi:Key"]);
});

builder.Services.AddRiskClient(builder.Configuration);
builder.Services.AddScoped<IAuthRiskService, AuthRiskService>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

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
