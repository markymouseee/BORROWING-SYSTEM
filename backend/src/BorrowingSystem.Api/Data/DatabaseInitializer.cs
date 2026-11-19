using BorrowingSystem.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BorrowingSystem.Api.Data;

/// <summary>Applies pending migrations and seeds the minimum data needed to sign in.</summary>
public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var config = services.GetRequiredService<IConfiguration>();
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

        await db.Database.MigrateAsync();

        if (!await db.Departments.AnyAsync())
        {
            db.Departments.AddRange(DefaultDepartments());
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync())
        {
            var adminPassword = config["Seed:AdminPassword"]
                ?? throw new InvalidOperationException("Seed:AdminPassword must be configured for the first run.");

            var admin = new User("Property Custodian", "admin@legacycollege.edu.ph", UserRole.Admin, departmentId: null);
            admin.SetPasswordHash(hasher.HashPassword(admin, adminPassword));
            db.Users.Add(admin);

            if (app.Environment.IsDevelopment() && config["Seed:FacultyPassword"] is { } facultyPassword)
            {
                var deptId = await db.Departments.OrderBy(d => d.Id).Select(d => d.Id).FirstAsync();
                var faculty = new User("Sample Faculty", "faculty@legacycollege.edu.ph", UserRole.Faculty, deptId);
                faculty.SetPasswordHash(hasher.HashPassword(faculty, facultyPassword));
                db.Users.Add(faculty);
            }

            await db.SaveChangesAsync();
        }

        if (app.Environment.IsDevelopment() && !await db.Equipment.AnyAsync())
        {
            db.Equipment.AddRange(SampleEquipment(now));
            await db.SaveChangesAsync();
        }
    }

    // Placeholder list — rename to match the school's actual offices from the Departments page.
    private static IEnumerable<Department> DefaultDepartments() =>
    [
        new("BED", "Basic Education Department"),
        new("CTE", "College of Teacher Education"),
        new("CBA", "College of Business Administration"),
        new("CCJE", "College of Criminal Justice Education"),
        new("CIT", "College of Information Technology"),
        new("SAO", "Student Affairs Office")
    ];

    private static IEnumerable<Equipment> SampleEquipment(DateTime now) =>
    [
        new("SPK-001", "Powered speaker 15\"", "Speaker", "JBL EON, with stand", now),
        new("SPK-002", "Powered speaker 15\"", "Speaker", "JBL EON, with stand", now),
        new("SPK-003", "Portable PA speaker", "Speaker", "Battery-powered, with 2 handheld mics", now),
        new("MIC-001", "Wireless microphone", "Microphone", "Shure handheld, receiver included", now),
        new("MIC-002", "Wireless microphone", "Microphone", "Shure handheld, receiver included", now),
        new("MIC-003", "Wired microphone", "Microphone", "Dynamic, 10 m XLR cable", now),
        new("MIC-004", "Lapel microphone", "Microphone", "Clip-on, wireless pack", now),
        new("PRJ-001", "LCD projector", "Projector", "Epson 3LCD, HDMI/VGA", now),
        new("PRJ-002", "LCD projector", "Projector", "Epson 3LCD, HDMI/VGA", now),
        new("SCR-001", "Tripod projector screen", "Projector screen", "100-inch", now),
        new("MIX-001", "Audio mixer", "Audio mixer", "Yamaha 8-channel", now),
        new("EXT-001", "Extension cord", "Extension cord", "4-gang, 10 m", now),
        new("EXT-002", "Extension cord", "Extension cord", "4-gang, 10 m", now),
        new("EXT-003", "Extension cord", "Extension cord", "6-gang, 5 m", now),
        new("EXT-004", "Cable reel", "Extension cord", "25 m heavy duty", now)
    ];
}
