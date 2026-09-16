using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Models;
using ManagementSystem.Api.Constants;
using ManagementSystem.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() 
                     ?? new[] { "http://localhost:5173", "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("LoginLimiter", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync("{\"message\": \"Bạn đã thử đăng nhập quá nhiều lần. Vui lòng thử lại sau 1 phút.\"}", token);
    };
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString), ServiceLifetime.Scoped, ServiceLifetime.Singleton);

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

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
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://localhost:5001",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "https://localhost:5001",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "super_secret_key_12345678901234567890"))
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
            var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var stampClaim = context.Principal?.FindFirst("SecurityStamp")?.Value;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(stampClaim))
            {
                context.Fail("Invalid token claims.");
                return;
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user == null || user.SecurityStamp != stampClaim)
            {
                context.Fail("Token has been revoked or user not found.");
                return;
            }

            if (userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(user))
            {
                context.Fail("Tài khoản đang bị tạm khóa.");
                return;
            }
        }
    };
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    
    // Tự động Apply Migration khi khởi động (Tiện cho deploy)
    if (context.Database.IsRelational())
    {
        context.Database.Migrate();
    }
    
    await DbSeeder.SeedRolesAndAdminAsync(services);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("StrictCors");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "API is running!");

app.Run();
