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
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("Reservations");

            entity.Property(r => r.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(r => r.ReservationDate)
                  .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            entity.HasOne(r => r.Customer)
                  .WithMany()
                  .HasForeignKey(r => r.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Vehicle)
                  .WithMany()
                  .HasForeignKey(r => r.VehicleId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => r.VehicleId)
                  .IsUnique()
                  .HasFilter("[Status] = 'Active'")
                  .HasDatabaseName("IX_Reservations_OneActivePerVehicle");

            entity.HasIndex(r => new { r.CustomerId, r.Status });
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");

            entity.Property(c => c.FullName).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(256).IsRequired();
            entity.Property(c => c.Phone).HasMaxLength(20).IsRequired();
            entity.Property(c => c.Address).HasMaxLength(200);
            entity.Property(c => c.CreatedAt)
                  .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            entity.HasIndex(c => c.Email).IsUnique();
            entity.HasIndex(c => c.UserId).IsUnique();

            entity.HasOne(c => c.User)
                  .WithOne(u => u.Customer)
                  .HasForeignKey<Customer>(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

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
            entity.ToTable("Vehicles", t =>
            {
                t.HasCheckConstraint("CK_Vehicles_Price_Positive", "[Price] > 0");
                t.HasCheckConstraint("CK_Vehicles_Mileage_NonNegative", "[Mileage] >= 0");
                t.HasCheckConstraint("CK_Vehicles_Year_Range", "[ManufacturingYear] BETWEEN 1990 AND 2100");
            });

            entity.HasKey(v => v.Id);

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
            entity.Property(v => v.Price).HasPrecision(18, 2);

            entity.Property(v => v.CreatedAt)
                  .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            entity.HasIndex(v => new { v.Brand, v.AvailabilityStatus, v.Price })
                  .HasDatabaseName("IX_Vehicles_Brand_Status_Price");

            entity.HasIndex(v => new { v.AvailabilityStatus, v.Price })
                  .HasDatabaseName("IX_Vehicles_Status_Price");
        });
    }
}
