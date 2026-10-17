using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QueueAPI.Data;
using QueueAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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
builder.Services.AddDbContext<QueueDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsql => npgsql.EnableRetryOnFailure()));
builder.Services.AddSingleton<IRedisQueueStore, RedisQueueStore>();
builder.Services.AddScoped<IQueueService, QueueService>();
var eventApiBaseUrl = builder.Configuration["Services:EventAPI"] ?? "https://localhost:7289";
builder.Services.AddHttpClient("EventAPI", client =>
{
    client.BaseAddress = new Uri(eventApiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHostedService<QueueAdmissionWorker>();

var jwt = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
    ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"],
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SecretKey"]!)),
    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
    ClockSkew = TimeSpan.FromSeconds(30)
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", policy => policy.RequireRole("Customer", "Organizer", "Admin"));
    options.AddPolicy("QueueOrganizer", policy => policy.RequireRole("Organizer", "Admin"));
    options.AddPolicy("QueueAdmin", policy => policy.RequireRole("Admin", "Staff"));
});
builder.Services.AddCors(options => options.AddPolicy("AllowFrontend", policy => policy.WithOrigins("http://localhost:5173", "https://localhost:5173").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QueueDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
