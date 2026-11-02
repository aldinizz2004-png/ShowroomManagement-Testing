using Showroom.Api.Models;

namespace Showroom.Api.DTOs;

public class CreateVehicleRequest
{
    public Brand? Brand { get; set; }
    public string? Model { get; set; }
    public int? ManufacturingYear { get; set; }
    public decimal? Price { get; set; }
    public string? Color { get; set; }
    public int? Mileage { get; set; }
}

public class UpdateVehicleRequest
{
    public Brand? Brand { get; set; }
    public string? Model { get; set; }
    public int? ManufacturingYear { get; set; }
    public decimal? Price { get; set; }
    public string? Color { get; set; }
    public int? Mileage { get; set; }
}

public class UpdateVehicleStatusRequest
{
    public AvailabilityStatus? Status { get; set; }
}

public class VehicleQuery
{
    public Brand? Brand { get; set; }
    public string? Model { get; set; }
    public AvailabilityStatus? Status { get; set; }
}

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
