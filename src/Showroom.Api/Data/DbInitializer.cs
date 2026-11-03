using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Showroom.Api.Auth;
using Showroom.Api.Models;

namespace Showroom.Api.Data;

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

            var user = await db.Users.Include(u => u.Customer).FirstOrDefaultAsync(u => u.Email == email);
            if (user is null)
            {
                user = new User
                {
                    FullName = seed["FullName"] ?? role!,
                    Email = email,
                    Role = role!,
                    CreatedAt = DateTime.UtcNow
                };
                user.PasswordHash = hasher.HashPassword(user, password);
                db.Users.Add(user);
            }

            if (user.Role == Roles.Customer && user.Customer is null && !await db.Customers.AnyAsync(c => c.Email == email))
            {
                user.Customer = new Customer
                {
                    FullName = user.FullName,
                    Email = email,
                    Phone = seed["Phone"] ?? "0590000000",
                    CreatedAt = DateTime.UtcNow
                };
            }
        }

        await db.SaveChangesAsync();
    }
}
