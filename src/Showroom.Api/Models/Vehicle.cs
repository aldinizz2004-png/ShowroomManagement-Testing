namespace Showroom.Api.Models;

/// <summary>A vehicle in the showroom inventory (database entity).</summary>
public class Vehicle
{
    public int Id { get; set; }
    public Brand Brand { get; set; }
    public string Model { get; set; } = string.Empty;
    public int ManufacturingYear { get; set; }
    public decimal Price { get; set; }                      // NIS
    public string Color { get; set; } = string.Empty;
    public int Mileage { get; set; }                        // km
    public AvailabilityStatus AvailabilityStatus { get; set; } = AvailabilityStatus.Available;
    public DateTime CreatedAt { get; set; }
}
