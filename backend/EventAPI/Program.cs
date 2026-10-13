using System.Text;
using EventAPI.Data;
using EventAPI.Repositories;
using EventAPI.Services;
using EventAPI.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Repositories
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IEventImageRepository, EventImageRepository>();
builder.Services.AddScoped<ITicketTypeRepository, TicketTypeRepository>();
builder.Services.AddScoped<IRefundPolicyRepository, RefundPolicyRepository>();
builder.Services.AddScoped<ISeatingRepository, SeatingRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();
builder.Services.AddScoped<IPricingRuleRepository, PricingRuleRepository>();
builder.Services.AddScoped<ISeatingTemplateRepository, SeatingTemplateRepository>();

// Governance dùng API nội bộ, không truy cập DB của service khác.
builder.Services.AddScoped<GovernanceService>();
// Upload phải hết hạn trước Gateway để EventAPI còn thời gian trả lỗi có nội dung.
builder.Services.AddHttpClient<IComplianceAssetStore, ComplianceAssetStore>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
    client.DefaultRequestVersion = System.Net.HttpVersion.Version11;
    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
    client.DefaultRequestHeaders.ExpectContinue = false;
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    ConnectTimeout = TimeSpan.FromSeconds(15),
    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
});
builder.Services.AddHostedService<GovernanceOutboxWorker>();
foreach (var service in new[] { "Ticket", "Notification" })
{
    builder.Services.AddHttpClient("Governance" + service, client =>
    {
        var url = builder.Configuration["Governance:" + service + "Api"] ?? (service == "Ticket" ? "https://localhost:7268/" : "https://localhost:7197/");
        client.BaseAddress = new Uri(url);
        client.Timeout = TimeSpan.FromSeconds(20);
        var key = builder.Configuration["Governance:InternalApiKey"];
        if (!string.IsNullOrEmpty(key)) client.DefaultRequestHeaders.Add("X-Internal-Api-Key", key);
    });
}

// Services
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<ITicketTypeService, TicketTypeService>();
builder.Services.AddScoped<IEventImageService, EventImageService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<IRefundPolicyService, RefundPolicyService>();
builder.Services.AddScoped<ISeatingService, SeatingService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<IEventSubmissionValidator, EventSubmissionValidator>();
builder.Services.AddScoped<IPricingRuleService, PricingRuleService>();
builder.Services.AddScoped<ISeatingTemplateService, SeatingTemplateService>();
builder.Services.AddScoped<IEventAccessService, EventAccessService>();

var authApiBaseUrl = builder.Configuration["Services:AuthenticationApi"] ?? "http://localhost:5010";
builder.Services.AddHttpClient<IStaffDirectoryClient, StaffDirectoryClient>(client =>
{
    client.BaseAddress = new Uri(authApiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<IEventStaffService, EventStaffService>();
builder.Services.AddHttpClient<IIdentityRoleClient, IdentityRoleClient>(client =>
{
    client.BaseAddress = new Uri(authApiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// FluentValidation — validators are resolved explicitly in controllers (no auto-validation magic)
builder.Services.AddScoped<IValidator<EventAPI.DTOs.CreateEventDTO>, CreateEventValidator>();
builder.Services.AddScoped<IValidator<EventAPI.DTOs.UpdateEventDTO>, UpdateEventValidator>();
builder.Services.AddScoped<IValidator<EventAPI.DTOs.CreateTicketTypeDTO>, CreateTicketTypeValidator>();
builder.Services.AddScoped<IValidator<EventAPI.DTOs.UpdateTicketTypeDTO>, UpdateTicketTypeValidator>();
builder.Services.AddScoped<IValidator<EventAPI.DTOs.CreateRefundPolicyDTO>, CreateRefundPolicyValidator>();
// NOTE: CreatePricingRuleValidator already existed in Validators/TicketTypeValidators.cs
// but was never wired into DI, so PricingRulesController could not have started up
// (constructor asks for IValidator<CreatePricingRuleDTO>). Fixed here.
builder.Services.AddScoped<IValidator<EventAPI.DTOs.CreatePricingRuleDTO>, CreatePricingRuleValidator>();

// Swagger with JWT Bearer
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ConcertShield EventAPI - Music Concerts",
        Version = "v1",
        Description = "Concert and Ticketing Management Microservice for ConcertShieldSystem"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// PostgreSQL DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
    ?? "Host=localhost;Port=5432;Database=event_db;Username=postgres;Password=123456";

builder.Services.AddDbContext<EventDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// JWT Authentication
var jwtSecretKey = builder.Configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("Configure JwtSettings:SecretKey");
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "AuthenticationAPI";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "AuthenticationAPIUsers";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireCustomer", policy => policy.RequireRole("Customer", "Organizer", "Admin"));
    options.AddPolicy("RequireOrganizer", policy => policy.RequireRole("Organizer", "Admin"));
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:7164")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Database schema is provisioned by database/event_db.sql.
// Do not execute DDL during API startup; this avoids startup failures when PostgreSQL reconnects.

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EventAPI v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseMiddleware<GovernanceMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
