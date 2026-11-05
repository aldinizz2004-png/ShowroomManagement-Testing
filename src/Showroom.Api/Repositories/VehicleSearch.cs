using Showroom.Api.Models;

namespace Showroom.Api.Repositories;

public enum VehicleSortField
{
    Id,
    Price,
    Model,
    ManufacturingYear
}

public record VehicleSearch
{
    public Brand? Brand { get; init; }
    public string? Model { get; init; }
    public int? Year { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string? Color { get; init; }
    public AvailabilityStatus? Status { get; init; }
    public VehicleSortField SortBy { get; init; } = VehicleSortField.Id;
    public bool Descending { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}
