using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ReviewAPI.Data;
using ReviewAPI.Repositories;
using ReviewAPI.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "ConcertShield ReviewAPI", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddDbContext<ReviewDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("event", client => client.BaseAddress = new Uri(builder.Configuration["Services:EventApi"]!.TrimEnd('/') + "/"));
builder.Services.AddHttpClient("identity", client => client.BaseAddress = new Uri(builder.Configuration["Services:AuthenticationApi"]?.TrimEnd('/') + "/" ?? "http://localhost:5010/"));
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IEventClient, EventClient>();
builder.Services.AddScoped<IUserClient, UserClient>();

var jwtSecretKey = builder.Configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
    ValidIssuer = builder.Configuration["JwtSettings:Issuer"], ValidAudience = builder.Configuration["JwtSettings:Audience"],
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)), ClockSkew = TimeSpan.FromSeconds(30)
});
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddPolicy("AllowFrontend", policy => policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS public.reviews (
            review_id integer NOT NULL GENERATED ALWAYS AS IDENTITY,
            event_id integer NOT NULL,
            user_id integer NOT NULL,
            rating integer NOT NULL CHECK (rating BETWEEN 1 AND 5),
            comment character varying(1000),
            created_at timestamp with time zone NOT NULL DEFAULT now(),
            updated_at timestamp with time zone,
            is_deleted boolean NOT NULL DEFAULT false,
            deleted_at timestamp with time zone,
            deleted_by integer,
            CONSTRAINT reviews_pkey PRIMARY KEY (review_id),
            CONSTRAINT uq_reviews_event_user UNIQUE (event_id, user_id)
        );
        CREATE INDEX IF NOT EXISTS ix_reviews_event_id ON public.reviews(event_id);
        CREATE TABLE IF NOT EXISTS public.review_replies (
            reply_id integer NOT NULL GENERATED ALWAYS AS IDENTITY,
            review_id integer NOT NULL,
            user_id integer NOT NULL,
            role character varying(30) NOT NULL,
            comment character varying(1000) NOT NULL,
            created_at timestamp with time zone NOT NULL DEFAULT now(),
            updated_at timestamp with time zone,
            is_deleted boolean NOT NULL DEFAULT false,
            deleted_at timestamp with time zone,
            deleted_by integer,
            CONSTRAINT review_replies_pkey PRIMARY KEY (reply_id),
            CONSTRAINT fk_review_replies_review FOREIGN KEY (review_id) REFERENCES public.reviews(review_id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS ix_review_replies_review_id ON public.review_replies(review_id);
        """);
}
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection(); app.UseCors("AllowFrontend"); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); app.Run();
