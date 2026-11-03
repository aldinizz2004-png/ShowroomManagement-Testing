namespace Showroom.Api.Models;

public enum ReservationStatus
{
    Active,
    Cancelled,
    Completed
}

public class Reservation
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public DateTime ReservationDate { get; set; }
    public DateOnly ExpectedPurchaseDate { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
}
