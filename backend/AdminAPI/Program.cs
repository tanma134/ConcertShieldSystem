using System.Text;
using AdminAPI.Data;
using AdminAPI.Services;
using AdminAPI.Services.Risks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AdminDbContext>(options =>
 options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISupportEmailService, SupportEmailService>();
builder.Services.AddScoped<ISupportCaseService, SupportCaseService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
 ?? throw new InvalidOperationException("Missing configuration Jwt:Key");
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
 .AddJwtBearer(options =>
 {
 options.TokenValidationParameters = new TokenValidationParameters
 {
 ValidateIssuerSigningKey = true,
 IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

 ValidateIssuer = true,
 ValidIssuer = jwtIssuer,

 ValidateAudience = true,
 ValidAudience = jwtAudience,

 ValidateLifetime = true,
 ClockSkew = TimeSpan.FromMinutes(1)
 };
 });


builder.Services.AddAuthorization(options =>
{
 options.AddPolicy("CanManageUsers", policy =>
 policy.RequireRole("Admin"));

 options.AddPolicy("CanManageSupportCases", policy =>
 policy.RequireRole("Admin", "Staff", "SupportStaff", "SecurityStaff"));

 // chỉ Admin được xem/xử lý fraud-risk (Staff không còn quyền).
 // Bật "Risk:RequirePermissionClaim": true để yêu cầu thêm claim permission = fraud.manage | risk.manage
 // (claim này phải do AuthenticationAPI cấp trong JWT).
 options.AddPolicy("CanReviewFraud", policy =>
 {
 policy.RequireRole("Admin");
 if (builder.Configuration.GetValue<bool>("Risk:RequirePermissionClaim"))
 policy.RequireAssertion(ctx => ctx.User.Claims.Any(c =>
 c.Type == "permission" && (c.Value == "fraud.manage" || c.Value == "risk.manage")));
 });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
 options.SwaggerDoc("v1", new OpenApiInfo { Title = "AdminAPI", Version = "v1" });

 options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
 {
 Name = "Authorization",
 Type = SecuritySchemeType.Http,
 Scheme = "bearer",
 BearerFormat = "JWT",
 In = ParameterLocation.Header,
 Description = "Nhập access token (không cần gõ chữ 'Bearer '), ví dụ: eyJhbGciOi..."
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
builder.Services.AddRiskEngine(builder.Configuration);
builder.Services.AddAppealEngine();
var app = builder.Build();

// Tự động khởi tạo schema Support Cases nếu chưa có trong admin_db
using (var scope = app.Services.CreateScope())
{
 var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
 await adminDb.Database.ExecuteSqlRawAsync(@"
 CREATE TABLE IF NOT EXISTS support_cases (
 case_id SERIAL PRIMARY KEY,
 case_number VARCHAR(32) NOT NULL UNIQUE,
 customer_id INT NULL,
 customer_name VARCHAR(150) NOT NULL,
 customer_email VARCHAR(150) NOT NULL,
 customer_phone VARCHAR(50) NULL,
 title VARCHAR(255) NOT NULL,
 description TEXT NOT NULL,
 category VARCHAR(50) NOT NULL DEFAULT 'General Inquiry',
 priority VARCHAR(20) NOT NULL DEFAULT 'Medium',
 status VARCHAR(20) NOT NULL DEFAULT 'Open',
 order_id INT NULL,
 ticket_id INT NULL,
 payment_reference VARCHAR(100) NULL,
 assigned_staff_id INT NULL,
 assigned_staff_name VARCHAR(150) NULL,
 assigned_at TIMESTAMPTZ NULL,
 assigned_by INT NULL,
 resolution TEXT NULL,
 resolved_by INT NULL,
 resolved_by_name VARCHAR(150) NULL,
 resolved_at TIMESTAMPTZ NULL,
 created_by INT NOT NULL,
 created_by_name VARCHAR(150) NOT NULL,
 created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
 updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
 );

 CREATE TABLE IF NOT EXISTS support_case_notes (
 note_id SERIAL PRIMARY KEY,
 case_id INT NOT NULL REFERENCES support_cases(case_id) ON DELETE CASCADE,
 author_id INT NOT NULL,
 author_name VARCHAR(150) NOT NULL,
 author_role VARCHAR(50) NOT NULL,
 note_type VARCHAR(50) NOT NULL DEFAULT 'InternalNote',
 content TEXT NOT NULL,
 created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
 );

 CREATE INDEX IF NOT EXISTS ix_support_cases_status ON support_cases(status);
 CREATE INDEX IF NOT EXISTS ix_support_cases_priority ON support_cases(priority);
 CREATE INDEX IF NOT EXISTS ix_support_cases_assigned_staff ON support_cases(assigned_staff_id);
 CREATE INDEX IF NOT EXISTS ix_support_cases_customer_email ON support_cases(customer_email);
 CREATE INDEX IF NOT EXISTS ix_support_case_notes_case_id ON support_case_notes(case_id);
 ");
}

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

app.Run();