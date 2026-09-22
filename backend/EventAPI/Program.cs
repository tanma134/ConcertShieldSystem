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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EventDbContext>();
    await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS uq_wishlists_user_event ON wishlists(user_id, event_id);");

    // Migrations/003_seating_templates.sql — non-destructive, safe to run every startup.
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS public.seating_templates
        (
            seating_template_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
            organizer_id integer NOT NULL,
            name character varying(150) NOT NULL,
            description character varying(500),
            is_public boolean NOT NULL DEFAULT false,
            layout_json jsonb,
            zones_json jsonb NOT NULL,
            created_by integer NOT NULL,
            created_at timestamp with time zone NOT NULL DEFAULT now(),
            updated_at timestamp with time zone NOT NULL DEFAULT now(),
            is_deleted boolean NOT NULL DEFAULT false,
            deleted_at timestamp with time zone,
            CONSTRAINT seating_templates_pkey PRIMARY KEY (seating_template_id)
        );
        CREATE INDEX IF NOT EXISTS ix_seating_templates_organizer_id ON public.seating_templates(organizer_id);
        CREATE INDEX IF NOT EXISTS ix_seating_templates_is_public ON public.seating_templates(is_public);
    ");

    // Database-first deployments may predate the seating-mode contract. Keep these
    // additive upgrades idempotent so EventAPI never starts with a model/schema mismatch.
    await db.Database.ExecuteSqlRawAsync(@"
        ALTER TABLE public.events
            ADD COLUMN IF NOT EXISTS seating_mode character varying(30) NOT NULL DEFAULT 'GeneralAdmission';
        ALTER TABLE public.seat_zones
            ADD COLUMN IF NOT EXISTS zone_type character varying(20) NOT NULL DEFAULT 'Seated';
        ALTER TABLE public.seat_zones
            ADD COLUMN IF NOT EXISTS capacity integer NOT NULL DEFAULT 0;
        ALTER TABLE public.seating_templates
            ADD COLUMN IF NOT EXISTS seating_mode character varying(30) NOT NULL DEFAULT 'ReservedSeating';

        UPDATE public.seat_zones z
        SET capacity = counts.seat_count
        FROM (
            SELECT seat_zone_id, COUNT(*)::integer AS seat_count
            FROM public.seats GROUP BY seat_zone_id
        ) counts
        WHERE z.seat_zone_id = counts.seat_zone_id
          AND z.zone_type = 'Seated'
          AND z.capacity <> counts.seat_count;

        UPDATE public.seating_templates
        SET is_public = true, updated_at = NOW()
        WHERE organizer_id = 1 AND is_deleted = false AND is_public = false;

        UPDATE public.events e
        SET has_seating_chart = EXISTS (
                SELECT 1 FROM public.seat_maps sm
                WHERE sm.event_id = e.event_id AND sm.is_deleted = false
            ),
            seating_mode = CASE
                WHEN NOT EXISTS (SELECT 1 FROM public.seat_maps sm WHERE sm.event_id = e.event_id AND sm.is_deleted = false)
                    THEN 'GeneralAdmission'
                WHEN EXISTS (
                    SELECT 1 FROM public.seat_maps sm
                    JOIN public.seat_zones sz ON sz.seat_map_id = sm.seat_map_id
                    WHERE sm.event_id = e.event_id AND sm.is_deleted = false AND sz.zone_type = 'Seated'
                ) THEN 'ReservedSeating'
                ELSE 'StandingZones'
            END;
    ");
}

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
