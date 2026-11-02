using Showroom.Api.DTOs;
using Showroom.Api.Models;

namespace Showroom.UnitTests.Vehicles;

internal static class VehicleTestData
{
    public static CreateVehicleRequest ValidCreateRequest() => new()
    {
        Brand = Brand.Chery,
        Model = "Tiggo 8 Pro",
        ManufacturingYear = 2025,
        Price = 145_000m,
        Color = "White",
        Mileage = 0
    };

    public static UpdateVehicleRequest ValidUpdateRequest() => new()
    {
        Brand = Brand.OMODA,
        Model = "C5",
        ManufacturingYear = 2024,
        Price = 120_000m,
        Color = "Black",
        Mileage = 15_000
    };

    public static Vehicle ExistingVehicle(int id = 1, AvailabilityStatus status = AvailabilityStatus.Available) => new()
    {
        Id = id,
        Brand = Brand.JAECOO,
        Model = "J7",
        ManufacturingYear = 2024,
        Price = 160_000m,
        Color = "Grey",
        Mileage = 5_000,
        AvailabilityStatus = status,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };
}
