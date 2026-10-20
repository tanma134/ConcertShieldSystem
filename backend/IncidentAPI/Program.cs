using System.Text;
using IncidentAPI.Data;
using IncidentAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. Database - PostgreSQL + EF Core (pattern nhất quán với toàn dự án)
// ============================================================
builder.Services.AddDbContext<IncidentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ============================================================
// 2. Dependency Injection - Services
// ============================================================
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<IEvidenceService, EvidenceService>();
builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();

// ============================================================
// 3. JWT Authentication (cùng key/issuer/audience với toàn dự án)
// ============================================================
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

// ============================================================
// 4. Authorization Policies - RBAC phân quyền theo role
// ============================================================
builder.Services.AddAuthorization(options =>
{
    // Staff và Admin đều được xem/cập nhật incidents
    options.AddPolicy("CanManageIncidents", policy =>
        policy.RequireRole("Staff", "Admin"));

    // Chỉ Staff mới được tạo báo cáo và escalate
    // (Admin có thể tạo thông qua CanManageIncidents nếu cần, nhưng theo SRS thì Staff là actor chính)
    options.AddPolicy("CanReportIncident", policy =>
        policy.RequireRole("Staff", "Admin"));

    // Chỉ Admin mới được giải quyết sự cố (UC_16.5)
    options.AddPolicy("CanResolveIncident", policy =>
        policy.RequireRole("Admin"));
});

// ============================================================
// 5. Controllers + JSON
// ============================================================
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Giữ camelCase cho JSON response (nhất quán với toàn dự án)
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// ============================================================
// 6. Swagger với JWT Bearer support
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "IncidentAPI",
        Version = "v1",
        Description = "API quản lý sự cố tại hiện trường - UC_16.1 đến UC_16.5"
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

// ============================================================
// 7. CORS - cho phép Frontend gọi API
// ============================================================
const string FrontendCors = "FrontendCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCors, policy =>
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// ============================================================
// 8. Middleware Pipeline
// ============================================================
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

// Tự động tạo bảng database nếu chưa tồn tại
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
