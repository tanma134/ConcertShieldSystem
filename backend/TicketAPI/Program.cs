using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using TicketAPI.API;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using TicketAPI.Services;
using TicketAPI.Services.TicketAPI.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString =
    builder.Configuration.GetConnectionString("MyConnection");

builder.Services.AddDbContext<TicketDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllers();

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = builder.Configuration.GetConnectionString("RedisConnection");

    if (string.IsNullOrEmpty(configuration))
    {
        throw new InvalidOperationException("Thiếu cấu hình 'RedisConnection' trong appsettings.json!");
    }

    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddHttpClient<IEventApiClient, EventApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiSettings:EventApiUrl"] ?? "https://localhost:7289/"
        ?? throw new InvalidOperationException("EventApiUrl is missing.");

    var apiKey = builder.Configuration["InternalApiKey"]
        ?? throw new InvalidOperationException("EventAPI internal API key is missing.");

    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("X-Internal-Api-Key", apiKey);
});

builder.Services.AddHttpClient<IPaymentAPIClient, PaymentAPIClient>(client =>

{
    var eventApiUrl = builder.Configuration["ApiSettings:PaymentApiUrl"] ?? "https://localhost:7084/";
    client.BaseAddress = new Uri(eventApiUrl);
});

builder.Services.AddScoped<IHoldService, HoldService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ITicketService, TicketService>();

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

builder.Services.AddAuthorization();

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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();   // Phải đứng TRƯỚC UseAuthorization()
app.UseAuthorization();

app.MapControllers();

app.Run();