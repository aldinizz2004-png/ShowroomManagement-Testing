using Showroom.Api.DTOs;
using Showroom.Api.Models;

namespace Showroom.Api.Repositories;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Vehicle>> GetAllAsync(VehicleQuery filter, CancellationToken ct = default);
    Task AddAsync(Vehicle vehicle, CancellationToken ct = default);
    Task UpdateAsync(Vehicle vehicle, CancellationToken ct = default);
    Task DeleteAsync(Vehicle vehicle, CancellationToken ct = default);
}
