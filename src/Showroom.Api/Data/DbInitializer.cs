using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Showroom.Api.Auth;
using Showroom.Api.Models;

namespace Showroom.Api.Data;

/// <summary>
/// Prepares the database at startup:
///   Database:Initialization = "Migrate"       -> apply EF Core migrations (SQL Server, development)
///                           = "EnsureCreated" -> create the schema from the model (SQLite test database)
///                           = "None"          -> do nothing (default; run "dotnet ef database update" yourself)
/// Then creates the staff accounts listed in the "SeedUsers" section, if they do not exist yet.
/// Public registration only creates Customers, so Admin / SalesRep accounts come from here.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        switch (config["Database:Initialization"])
        {
            case "Migrate":
                await db.Database.MigrateAsync();
                break;
            case "EnsureCreated":
                await db.Database.EnsureCreatedAsync();
                break;
            default:
                return;
        }

        foreach (var seed in config.GetSection("SeedUsers").GetChildren())
        {
            var email = seed["Email"]?.Trim().ToLowerInvariant();
            var password = seed["Password"];
            var role = seed["Role"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || !Roles.All.Contains(role))
                continue;

            if (await db.Users.AnyAsync(u => u.Email == email))
                continue;

            var user = new User
            {
                FullName = seed["FullName"] ?? role!,
                Email = email,
                Role = role!,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
        }

        await db.SaveChangesAsync();
    }
}
