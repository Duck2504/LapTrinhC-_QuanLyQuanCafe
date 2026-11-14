using Microsoft.EntityFrameworkCore;
using QuanLyCafe.Api.Middleware;
using QuanLyCafe.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Default — đặt bằng dotnet user-secrets.")));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    bool seeded;
    try
    {
        seeded = await DbSeeder.SeedManagerAsync(db,
            app.Configuration["Seed:ManagerUsername"] ?? "manager",
            app.Configuration["Seed:ManagerPassword"]);
    }
    catch (Microsoft.Data.SqlClient.SqlException)
    {
        app.Logger.LogCritical("Không truy cập được DB: kiểm tra SQL Server và đã chạy dotnet ef database update chưa.");
        throw;
    }
    if (!seeded)
        app.Logger.LogWarning("Chưa tạo được Manager: đặt Seed:ManagerPassword bằng user-secrets, hoặc Seed:ManagerUsername đã bị dùng.");
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
