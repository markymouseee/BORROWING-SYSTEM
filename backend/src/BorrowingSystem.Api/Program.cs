using System.Text.Json.Serialization;
using BorrowingSystem.Api.Common;
using BorrowingSystem.Api.Data;
using BorrowingSystem.Api.Domain;
using BorrowingSystem.Api.Features.Auth;
using BorrowingSystem.Api.Features.Inventory;
using BorrowingSystem.Api.Features.Reservations;
using BorrowingSystem.Api.Features.Scheduling;
using BorrowingSystem.Api.Features.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

var builder = WebApplication.CreateBuilder(args);

// Web
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();

// Infrastructure
builder.Services.AddDbContext<AppDbContext>(o => o
    .UseMySQL(builder.Configuration.GetConnectionString("Default")
              ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured."))
    .ReplaceService<IHistoryRepository, MariaDbHistoryRepository>());
builder.Services.AddSingleton(TimeProvider.System);

// Security
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApiSecurity();

// Features
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<EquipmentService>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<UserService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// In production the compiled Angular app is copied into wwwroot and served by this same process.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

await app.InitializeDatabaseAsync();
app.Run();
