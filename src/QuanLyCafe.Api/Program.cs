using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using QuanLyCafe.Api.Auth;
using QuanLyCafe.Api.Middleware;
using QuanLyCafe.Application.Ordering;
using QuanLyCafe.Application.Staff;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Ordering;
using QuanLyCafe.Domain.Staff;
using QuanLyCafe.Infrastructure.Persistence;
using QuanLyCafe.Infrastructure.Repositories.Ordering;
using QuanLyCafe.Infrastructure.Repositories.Staff;
using Serilog;

using QuanLyCafe.Api;

var builder = WebApplication.CreateBuilder(args);

// Serilog: console + file theo ngày, cấu hình trong appsettings "Serilog".
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// Add services to the container.

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default — đặt bằng dotnet user-secrets.")));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddMemoryCache();

// Staff
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

// Ordering
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<OrderService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false; // giữ nguyên tên claim "sub", "name", "role"
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = JwtTokenGenerator.SigningKey(builder.Configuration),
        NameClaimType = JwtRegisteredClaimNames.Name,
        RoleClaimType = "role",
    };
    o.Events = new JwtBearerEvents
    {
        // Mục 4.3.4: tài khoản bị vô hiệu hóa hoặc bị đổi vai trò bị chặn ngay dù token còn hạn (đệm 30 giây).
        OnTokenValidated = async ctx =>
        {
            var sp = ctx.HttpContext.RequestServices;
            var userId = Guid.Parse(ctx.Principal!.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
            var currentRole = await sp.GetRequiredService<IMemoryCache>().GetOrCreateAsync($"user-role:{userId}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                var user = await sp.GetRequiredService<IUserRepository>().GetByIdAsync(userId);
                return user?.IsActive == true ? user.Role.RoleName : null; // null = bị khóa / không còn
            });
            if (currentRole is null)
            {
                ctx.HttpContext.Items["authCode"] = ErrorCodes.AuthAccountLocked;
                ctx.Fail("Tài khoản đã bị vô hiệu hóa.");
            }
            else if (ctx.Principal.FindFirst("role")?.Value != currentRole)
                ctx.Fail("Vai trò đã thay đổi, cần đăng nhập lại."); // → 401 ERR-AUTH-003, Desktop về form đăng nhập
        },
        // 401 trả ProblemDetails có code để Desktop hiển thị đúng thông báo.
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            var code = ctx.HttpContext.Items["authCode"] as string ?? ErrorCodes.AuthInvalidToken;
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new()
            {
                HttpContext = ctx.HttpContext,
                ProblemDetails =
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = code == ErrorCodes.AuthAccountLocked
                        ? "Tài khoản đã bị vô hiệu hóa."
                        : "Chưa đăng nhập hoặc phiên đăng nhập đã hết hạn.",
                    Extensions = { ["code"] = code },
                },
            });
        },
    };
});
// Mặc định mọi endpoint cần đăng nhập; chỉ auth/login và /health được [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCatalogAndInventory();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    bool seeded;
    try
    {
        // Tự tạo/cập nhật bảng: người chạy Docker không cần cài dotnet-ef.
        await db.Database.MigrateAsync();
        seeded = await DbSeeder.SeedManagerAsync(db,
            app.Configuration["Seed:ManagerUsername"] ?? "manager",
            app.Configuration["Seed:ManagerPassword"]);
    }
    catch (Microsoft.Data.SqlClient.SqlException)
    {
        app.Logger.LogCritical("Không truy cập được DB: kiểm tra SQL Server và ConnectionStrings:Default.");
        throw;
    }
    if (!seeded)
        app.Logger.LogWarning("Chưa tạo được Manager: đặt Seed:ManagerPassword bằng user-secrets, hoặc Seed:ManagerUsername đã bị dùng.");
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Kiểm tra kết nối SQL Server (mục 4.5.4).
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "Healthy" })
            : Results.Json(new { status = "Unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous();

app.Run();
