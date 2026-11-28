using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;
using System.Text.Json.Nodes;

namespace GatewayAPI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var ocelotFile = builder.Environment.IsDevelopment()
                ? "ocelot.json"
                : "ocelot.json";

            var ocelotConfiguration = JsonNode.Parse(await File.ReadAllTextAsync(ocelotFile))
                ?? throw new InvalidOperationException($"Could not parse {ocelotFile}.");
            if (string.Equals(Environment.GetEnvironmentVariable("DOCKER_MODE"), "true", StringComparison.OrdinalIgnoreCase))
            {
                var routes = ocelotConfiguration["Routes"]?.AsArray();
                if (routes is not null)
                {
                    foreach (var route in routes)
                    {
                        var downstream = route?["DownstreamHostAndPorts"]?.AsArray()?.FirstOrDefault();
                        if (downstream?["Port"] is not JsonValue portValue ||
                            !portValue.TryGetValue<int>(out var port))
                        {
                            continue;
                        }

                        route!["DownstreamScheme"] = "http";
                        downstream["Host"] = GetDockerDownstreamHost(port);
                        downstream["Port"] = 8080;
                    }
                }
            }

            builder.Configuration.AddJsonStream(
                new MemoryStream(Encoding.UTF8.GetBytes(ocelotConfiguration.ToJsonString())));
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

        private static string GetDockerDownstreamHost(int port) => port switch
        {
            7058 or 7243 => "authentication-api",
            7076 => "admin-api",
            7084 or 7227 => "payment-api",
            7108 or 7268 or 7320 => "ticket-api",
            7186 or 7197 => "notification-api",
            7289 or 7020 => "event-api",
            7290 => "review-api",
            7310 => "queue-api",
            7330 => "checkin-api",
            7340 => "incident-api",
            _ => "unavailable-service"
        };
    }
}