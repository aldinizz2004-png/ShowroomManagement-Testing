using Microsoft.EntityFrameworkCore;
using Showroom.Api.Models;

namespace Showroom.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", t =>
                t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Admin', 'SalesRep', 'Customer')"));

            entity.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(u => u.Role).HasMaxLength(20).IsRequired();
            entity.Property(u => u.CreatedAt)
                  .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            // The database rejects invalid data too, even if it is written from outside the API
            entity.ToTable("Vehicles", t =>
            {
                t.HasCheckConstraint("CK_Vehicles_Price_Positive", "[Price] > 0");
                t.HasCheckConstraint("CK_Vehicles_Mileage_NonNegative", "[Mileage] >= 0");
                t.HasCheckConstraint("CK_Vehicles_Year_Range", "[ManufacturingYear] BETWEEN 1990 AND 2100");
            });

            entity.HasKey(v => v.Id);

            // Enums stored as text ("Chery", "Available") - readable in the database
            entity.Property(v => v.Brand)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(v => v.AvailabilityStatus)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(v => v.Model).HasMaxLength(50).IsRequired();
            entity.Property(v => v.Color).HasMaxLength(30).IsRequired();
            entity.Property(v => v.Price).HasPrecision(18, 2);   // NIS

            // Always UTC when read back, so JSON ends with "Z"
            entity.Property(v => v.CreatedAt)
                  .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            // Filtering by brand + model, and by status
            entity.HasIndex(v => new { v.Brand, v.Model });
            entity.HasIndex(v => v.AvailabilityStatus);
        });
    }
}
