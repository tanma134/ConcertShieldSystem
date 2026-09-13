using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;
using Microsoft.IdentityModel.Logging;

namespace GatewayAPI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            IdentityModelEventSource.ShowPII = true;

            var builder = WebApplication.CreateBuilder(args);
            Console.WriteLine("===== CONFIG SOURCES =====");
            Console.WriteLine(File.ReadAllText("appsettings.json"));
            foreach (var s in ((IConfigurationRoot)builder.Configuration).Providers)
            {
                Console.WriteLine(s);
            }
            Console.WriteLine(builder.Environment.ContentRootPath);
            var ocelotFile = builder.Environment.IsDevelopment()
                ? "ocelot.json"
                : "ocelot.json";

            builder.Configuration.AddJsonFile(ocelotFile, optional: false, reloadOnChange: true);
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy
                        .WithOrigins(
                            builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                            ?? ["https://localhost:7104", "http://localhost:5213"])
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "Event Booking – API Gateway", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new()
                {
                    Name = "Authorization",
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Description = "Enter JWT token"
                });
                c.AddSecurityRequirement(new()
                {
                    {
                        new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
                        []
                    }
                });
            });

            var jwtKey = builder.Configuration["Jwt:Key"]

                         ?? throw new InvalidOperationException("Jwt:Key is not configured.");
            Console.WriteLine("================================");
            Console.WriteLine(jwtKey);
            Console.WriteLine(jwtKey.Length);
            Console.WriteLine("================================");
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer("Bearer", options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) &&
                                (path.StartsWithSegments("/hubs") || path.StartsWithSegments("/notificationHub") || path.StartsWithSegments("/dashboardHub")))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddAuthorization();
            builder.Services.AddHttpClient();
            builder.Services.AddOcelot(builder.Configuration);
            var app = builder.Build();
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway v1"));
            }

            app.UseHttpsRedirection();
            app.UseCors("AllowFrontend");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30)
            });
            app.MapControllers();
            await app.UseOcelot();
            app.Run();
        }
    }
}