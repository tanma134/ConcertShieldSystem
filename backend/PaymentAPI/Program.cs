using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PaymentAPI.API;
using PaymentAPI.Data;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Risk;
using PaymentAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers & JSON
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// PostgreSQL DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=04_payment_db;Username=postgres;Password=123456";

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure())
);

builder.Services.AddHttpClient<ITicketApiClient, TicketApiClient>(client =>
{
    var ticketApiUrl = builder.Configuration["ApiSettings:TicketApiUrl"]
        ?? throw new InvalidOperationException("TicketApiUrl is missing.");

    var internalApiKey = builder.Configuration["TicketApi:InternalApiKey"]
        ?? throw new InvalidOperationException("TicketApi internal API key is missing.");

    client.BaseAddress = new Uri(ticketApiUrl);
    client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
});

builder.Services.AddHttpClient<IEventApiClient, EventApiClient>(client =>
{
    var eventApiUrl = builder.Configuration["ApiSettings:EventApiUrl"]
        ?? throw new InvalidOperationException("EventApiUrl is missing.");

    client.BaseAddress = new Uri(eventApiUrl);
});

// Repositories & Services
builder.Services.AddScoped<IVoucherRepository, VoucherRepository>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.Configure<VnPayConfig>(
    builder.Configuration.GetSection("VnPay"));

builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IPaymentTransactionService, PaymentTransactionService>();
builder.Services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
builder.Services.AddScoped<IRefundService, RefundService>();

// Risk Engine
builder.Services.AddRiskClient(builder.Configuration);
builder.Services.AddScoped<IPaymentRiskService, PaymentRiskService>();

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
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireOrganizer", policy => policy.RequireRole("Organizer", "Admin"));
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:7164", "http://localhost:5134")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Swagger with JWT Bearer
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ConcertShield PaymentAPI - Voucher & Payment Management",
        Version = "v1",
        Description = "Voucher and Payment Microservice for ConcertShieldSystem"
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

var app = builder.Build();

// Thêm schema cho Risk Engine, Refund và Đối soát nếu chưa có
using (var schemaScope = app.Services.CreateScope())
{
    var paymentDb = schemaScope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await paymentDb.Database.ExecuteSqlRawAsync(@"
        -- Cột Risk Engine
        ALTER TABLE payment_transactions ADD COLUMN IF NOT EXISTS user_id integer;
        ALTER TABLE payment_transactions ADD COLUMN IF NOT EXISTS client_ip varchar(64);
        ALTER TABLE payment_transactions ADD COLUMN IF NOT EXISTS device_fp varchar(128);
        ALTER TABLE payment_transactions ADD COLUMN IF NOT EXISTS session_id varchar(128);
        CREATE INDEX IF NOT EXISTS ix_payment_transactions_user_status ON payment_transactions (user_id, status);

        -- Bảng payment_refunds cơ bản
        CREATE TABLE IF NOT EXISTS payment_refunds (
            payment_refund_id integer GENERATED ALWAYS AS IDENTITY,
            return_request_id integer NULL,
            order_id integer NOT NULL,
            amount bigint NOT NULL,
            gateway varchar(30) NOT NULL DEFAULT 'VNPay',
            gateway_refund_ref varchar(100),
            status varchar(20) NOT NULL DEFAULT 'Pending',
            error_message varchar(500),
            created_at timestamp with time zone NOT NULL DEFAULT now(),
            completed_at timestamp with time zone,
            CONSTRAINT payment_refunds_pkey PRIMARY KEY (payment_refund_id)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS uq_payment_refunds_return_request ON payment_refunds (return_request_id);
        CREATE INDEX IF NOT EXISTS ix_payment_refunds_order_id ON payment_refunds (order_id);

        -- Cột mở rộng cho Payment Refund (Support Case, Retry, Reconciliation)
        ALTER TABLE payment_refunds ALTER COLUMN return_request_id DROP NOT NULL;
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS refund_type varchar(20) DEFAULT 'Full';
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS reason varchar(255);
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS processed_by_user_id integer;
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS processed_by_user_name varchar(100);
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS customer_name varchar(150);
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS gateway_error_code varchar(50);
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS retry_count integer NOT NULL DEFAULT 0;
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS last_retry_at timestamp with time zone;
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS gateway_amount bigint;
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS reconciliation_status varchar(30) DEFAULT 'Matched';
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS event_id integer;
        ALTER TABLE payment_refunds ADD COLUMN IF NOT EXISTS event_name varchar(255);

        -- Bảng payment_reconciliation_resolutions
        CREATE TABLE IF NOT EXISTS payment_reconciliation_resolutions (
            payment_reconciliation_resolution_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            payment_transaction_id integer NOT NULL,
            resolution_action varchar(50) NOT NULL,
            resolution_note text NOT NULL,
            resolution_status varchar(20) NOT NULL,
            resolved_by_user_id integer NOT NULL,
            resolved_at timestamp with time zone NOT NULL DEFAULT now()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS uq_reconciliation_resolution_transaction 
            ON payment_reconciliation_resolutions (payment_transaction_id);");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PaymentAPI v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

 app.Run();
