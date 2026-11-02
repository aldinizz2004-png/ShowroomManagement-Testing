using Showroom.Api.DTOs;
using Showroom.Api.Models;

namespace Showroom.Api.Repositories;

/// <summary>
/// Data access for vehicles. The service depends on this interface, not on EF Core,
/// so unit tests can replace it with a Moq fake and test the business logic without a database.
/// </summary>
public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Vehicle>> GetAllAsync(VehicleQuery filter, CancellationToken ct = default);
    Task AddAsync(Vehicle vehicle, CancellationToken ct = default);
    Task UpdateAsync(Vehicle vehicle, CancellationToken ct = default);
    Task DeleteAsync(Vehicle vehicle, CancellationToken ct = default);
}
