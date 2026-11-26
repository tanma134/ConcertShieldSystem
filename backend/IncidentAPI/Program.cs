using System.Text;
using IncidentAPI.Data;
using IncidentAPI.Repositories;
using IncidentAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<IncidentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();
builder.Services.AddScoped<IIncidentRepository, IncidentRepository>();
// Explicit factories: both services keep DbContext overloads for tests, so the container
// cannot choose between constructors on its own.
builder.Services.AddScoped<IIncidentService>(sp => new IncidentService(
    sp.GetRequiredService<IIncidentRepository>(),
    sp.GetRequiredService<ILogger<IncidentService>>(),
    sp.GetRequiredService<IHttpClientFactory>(),
    sp.GetRequiredService<IConfiguration>()));
builder.Services.AddScoped<IEvidenceService>(sp => new EvidenceService(
    sp.GetRequiredService<IIncidentRepository>(),
    sp.GetRequiredService<ICloudinaryService>(),
    sp.GetRequiredService<ILogger<EvidenceService>>()));
builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Key");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],

            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageIncidents", policy =>
        policy.RequireRole("Staff", "Admin"));

    options.AddPolicy("CanReportIncident", policy =>
        policy.RequireRole("Staff", "Admin"));

    options.AddPolicy("CanResolveIncident", policy =>
        policy.RequireRole("Admin"));
});

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "IncidentAPI",
        Version = "v1",
        Description = "API quản lý sự cố tại hiện trường"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập access token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

const string FrontendCors = "FrontendCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCors, policy =>
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddHttpClient<IAuditLogClient, AuditLogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AdminApi:BaseUrl"] ?? "https://localhost:7076");
    client.DefaultRequestHeaders.Add("X-Internal-Api-Key", builder.Configuration["InternalApi:Key"]);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCors);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<IncidentDbContext>();
        db.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Không thể tự động khởi tạo database khi khởi động (nếu DB chưa chạy hoặc đã tạo bằng SQL script): {Message}", ex.Message);
    }
}

app.Run();
