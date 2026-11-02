using Showroom.Api.Models;

namespace Showroom.Api.DTOs;

// Request fields are nullable on purpose: a missing field stays null and the
// service reports "X is required" instead of silently using 0 / the first enum value.
// All validation rules live in VehicleValidator (one place, unit-testable).

/// <summary>POST /api/vehicles</summary>
public class CreateVehicleRequest
{
    public Brand? Brand { get; set; }
    public string? Model { get; set; }
    public int? ManufacturingYear { get; set; }
    public decimal? Price { get; set; }
    public string? Color { get; set; }
    public int? Mileage { get; set; }
}

/// <summary>PUT /api/vehicles/{id} - replaces the vehicle details (status has its own endpoint).</summary>
public class UpdateVehicleRequest
{
    public Brand? Brand { get; set; }
    public string? Model { get; set; }
    public int? ManufacturingYear { get; set; }
    public decimal? Price { get; set; }
    public string? Color { get; set; }
    public int? Mileage { get; set; }
}

/// <summary>PATCH /api/vehicles/{id}/status</summary>
public class UpdateVehicleStatusRequest
{
    public AvailabilityStatus? Status { get; set; }
}

/// <summary>GET /api/vehicles?brand=Chery&amp;model=Tiggo&amp;status=Available - all filters optional.</summary>
public class VehicleQuery
{
    public Brand? Brand { get; set; }
    public string? Model { get; set; }                      // contains, case-insensitive
    public AvailabilityStatus? Status { get; set; }
}

/// <summary>What the client receives.</summary>
public class VehicleResponse
{
    public int Id { get; set; }
    public Brand Brand { get; set; }
    public string Model { get; set; } = string.Empty;
    public int ManufacturingYear { get; set; }
    public decimal Price { get; set; }
    public string Color { get; set; } = string.Empty;
    public int Mileage { get; set; }
    public AvailabilityStatus AvailabilityStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}
