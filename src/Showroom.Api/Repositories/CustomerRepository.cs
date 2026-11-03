using Microsoft.EntityFrameworkCore;
using Showroom.Api.Data;
using Showroom.Api.Models;

namespace Showroom.Api.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Customer?> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<List<Customer>> GetAllAsync(string? search, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, int? exceptCustomerId = null, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
    Task UpdateAsync(Customer customer, CancellationToken ct = default);
}

public class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;

    public CustomerRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Email == email, ct);

    public Task<Customer?> GetByUserIdAsync(int userId, CancellationToken ct = default) =>
        _db.Customers.FirstOrDefaultAsync(c => c.UserId == userId, ct);

    public Task<List<Customer>> GetAllAsync(string? search, CancellationToken ct = default)
    {
        IQueryable<Customer> query = _db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.FullName, pattern)
                                  || EF.Functions.Like(c.Email, pattern)
                                  || EF.Functions.Like(c.Phone, pattern));
        }

        return query.OrderBy(c => c.Id).ToListAsync(ct);
    }

    public Task<bool> EmailExistsAsync(string email, int? exceptCustomerId = null, CancellationToken ct = default) =>
        _db.Customers.AnyAsync(c => c.Email == email && c.Id != exceptCustomerId, ct);

    public async Task AddAsync(Customer customer, CancellationToken ct = default)
    {
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Customer customer, CancellationToken ct = default)
    {
        _db.Customers.Update(customer);
        await _db.SaveChangesAsync(ct);
    }
}
