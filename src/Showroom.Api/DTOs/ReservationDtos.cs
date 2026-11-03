using Showroom.Api.Models;

namespace Showroom.Api.DTOs;

public class CreateReservationRequest
{
    public int? CustomerId { get; set; }
    public int? VehicleId { get; set; }
    public DateOnly? ExpectedPurchaseDate { get; set; }
}

public class ReservationQuery
{
    public int? CustomerId { get; set; }
    public ReservationStatus? Status { get; set; }
}

public class ReservationResponse
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int VehicleId { get; set; }
    public string Vehicle { get; set; } = string.Empty;
    public DateTime ReservationDate { get; set; }
    public DateOnly ExpectedPurchaseDate { get; set; }
    public ReservationStatus Status { get; set; }
}
