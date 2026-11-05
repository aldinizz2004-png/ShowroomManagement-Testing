using Microsoft.EntityFrameworkCore;
using Showroom.Api.Data;
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

    public const string SearchTag = "Vehicle search";

    public async Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> SearchAsync(VehicleSearch search, CancellationToken ct = default)
    {
        var totalCount = await FilterQuery(search).CountAsync(ct);
        if (totalCount == 0)
            return ([], 0);

        var items = await PageQuery(search).ToListAsync(ct);
        return (items, totalCount);
    }

    public IQueryable<Vehicle> FilterQuery(VehicleSearch search) =>
        ApplyFilters(_db.Vehicles.AsNoTracking().TagWith(SearchTag), search);

    public IQueryable<Vehicle> PageQuery(VehicleSearch search) =>
        ApplySorting(FilterQuery(search), search)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize);

    private static IQueryable<Vehicle> ApplyFilters(IQueryable<Vehicle> query, VehicleSearch search)
    {
        if (search.Brand is { } brand)
            query = query.Where(v => v.Brand == brand);

        if (search.Model is { } model)
        {
            var pattern = $"%{EscapeLike(model)}%";
            query = query.Where(v => EF.Functions.Like(v.Model, pattern, LikeEscape));
        }

        if (search.Year is { } year)
            query = query.Where(v => v.ManufacturingYear == year);

        if (search.MinPrice is { } minPrice)
            query = query.Where(v => v.Price >= minPrice);

        if (search.MaxPrice is { } maxPrice)
            query = query.Where(v => v.Price <= maxPrice);

        if (search.Color is { } color)
        {
            var pattern = EscapeLike(color);
            query = query.Where(v => EF.Functions.Like(v.Color, pattern, LikeEscape));
        }

        if (search.Status is { } status)
            query = query.Where(v => v.AvailabilityStatus == status);

        return query;
    }

    private static IQueryable<Vehicle> ApplySorting(IQueryable<Vehicle> query, VehicleSearch search) =>
        (search.SortBy, search.Descending) switch
        {
            (VehicleSortField.Price, false) => query.OrderBy(v => v.Price).ThenBy(v => v.Id),
            (VehicleSortField.Price, true) => query.OrderByDescending(v => v.Price).ThenBy(v => v.Id),
            (VehicleSortField.Model, false) => query.OrderBy(v => v.Model).ThenBy(v => v.Id),
            (VehicleSortField.Model, true) => query.OrderByDescending(v => v.Model).ThenBy(v => v.Id),
            (VehicleSortField.ManufacturingYear, false) => query.OrderBy(v => v.ManufacturingYear).ThenBy(v => v.Id),
            (VehicleSortField.ManufacturingYear, true) => query.OrderByDescending(v => v.ManufacturingYear).ThenBy(v => v.Id),
            (_, true) => query.OrderByDescending(v => v.Id),
            _ => query.OrderBy(v => v.Id)
        };

    private const string LikeEscape = "\\";

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

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

    public Task<bool> HasReservationsAsync(int vehicleId, CancellationToken ct = default) =>
        _db.Reservations.AnyAsync(r => r.VehicleId == vehicleId, ct);
}
