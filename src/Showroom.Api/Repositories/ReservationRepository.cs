using Microsoft.EntityFrameworkCore;
using Showroom.Api.Data;
using Showroom.Api.Models;

namespace Showroom.Api.Repositories;

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Reservation>> GetAllAsync(int? customerId, ReservationStatus? status, CancellationToken ct = default);
    Task AddAsync(Reservation reservation, CancellationToken ct = default);
    Task UpdateAsync(Reservation reservation, CancellationToken ct = default);
}

public class ReservationRepository : IReservationRepository
{
    private readonly AppDbContext _db;

    public ReservationRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Reservation?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Reservations
           .Include(r => r.Customer)
           .Include(r => r.Vehicle)
           .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<Reservation>> GetAllAsync(int? customerId, ReservationStatus? status, CancellationToken ct = default)
    {
        IQueryable<Reservation> query = _db.Reservations
            .AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Vehicle);

        if (customerId is { } cid)
            query = query.Where(r => r.CustomerId == cid);

        if (status is { } s)
            query = query.Where(r => r.Status == s);

        return query.OrderByDescending(r => r.Id).ToListAsync(ct);
    }

    public async Task AddAsync(Reservation reservation, CancellationToken ct = default)
    {
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Reservation reservation, CancellationToken ct = default)
    {
        _db.Reservations.Update(reservation);
        await _db.SaveChangesAsync(ct);
    }
}
