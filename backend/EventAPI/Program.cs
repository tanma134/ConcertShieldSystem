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

// Services
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<ITicketTypeService, TicketTypeService>();
builder.Services.AddScoped<IEventImageService, EventImageService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<IRefundPolicyService, RefundPolicyService>();
builder.Services.AddScoped<ISeatingService, SeatingService>();
builder.Services.AddScoped<IEventSubmissionValidator, EventSubmissionValidator>();

// Calls AuthenticationAPI to grant the Organizer role once a concert is approved.
var authApiBaseUrl = builder.Configuration["Services:AuthenticationApi"] ?? "http://localhost:5010";
builder.Services.AddHttpClient<IIdentityRoleClient, IdentityRoleClient>(client =>
{
    client.BaseAddress = new Uri(authApiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// FluentValidation — validators are resolved explicitly in controllers (no auto-validation magic)
builder.Services.AddScoped<IValidator<EventAPI.DTOs.CreateEventDTO>, CreateEventValidator>();
builder.Services.AddScoped<IValidator<EventAPI.DTOs.UpdateEventDTO>, UpdateEventValidator>();
builder.Services.AddScoped<IValidator<EventAPI.DTOs.CreateTicketTypeDTO>, CreateTicketTypeValidator>();
builder.Services.AddScoped<IValidator<EventAPI.DTOs.CreateRefundPolicyDTO>, CreateRefundPolicyValidator>();

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
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure())
);

// JWT Authentication
var jwtSecretKey = builder.Configuration["JwtSettings:SecretKey"] ?? "SuperSecretKey_MustBe32CharsOrMore!@#";
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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
