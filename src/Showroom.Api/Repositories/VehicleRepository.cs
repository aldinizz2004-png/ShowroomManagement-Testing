using Microsoft.EntityFrameworkCore;
using Showroom.Api.Data;
using Showroom.Api.DTOs;
using Showroom.Api.Models;

namespace Showroom.Api.Repositories;

public class VehicleRepository : IVehicleRepository
{
    private readonly AppDbContext _db;

    public VehicleRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Vehicle?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<List<Vehicle>> GetAllAsync(VehicleQuery filter, CancellationToken ct = default)
    {
        IQueryable<Vehicle> query = _db.Vehicles.AsNoTracking();

        if (filter.Brand is { } brand)
            query = query.Where(v => v.Brand == brand);

        if (!string.IsNullOrWhiteSpace(filter.Model))
        {
            var pattern = $"%{filter.Model.Trim()}%";
            query = query.Where(v => EF.Functions.Like(v.Model, pattern));
        }

        if (filter.Status is { } status)
            query = query.Where(v => v.AvailabilityStatus == status);

        return query.OrderBy(v => v.Id).ToListAsync(ct);
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken ct = default)
    {
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Vehicle vehicle, CancellationToken ct = default)
    {
        _db.Vehicles.Update(vehicle);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Vehicle vehicle, CancellationToken ct = default)
    {
        _db.Vehicles.Remove(vehicle);
        await _db.SaveChangesAsync(ct);
    }
}
