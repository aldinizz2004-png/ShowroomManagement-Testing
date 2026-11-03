using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;

namespace Showroom.Api.Services;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerResponse>> GetAllAsync(string? search, CancellationToken ct = default);
    Task<ServiceResult<CustomerResponse>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<CustomerResponse>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<ServiceResult<CustomerResponse>> UpdateAsync(int id, UpdateCustomerRequest request, CancellationToken ct = default);
}

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _repository;

    public CustomerService(ICustomerRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<CustomerResponse>> GetAllAsync(string? search, CancellationToken ct = default)
    {
        var customers = await _repository.GetAllAsync(search, ct);
        return customers.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<CustomerResponse>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var customer = await _repository.GetByIdAsync(id, ct);
        return customer is null ? NotFound(id) : ServiceResult<CustomerResponse>.Ok(ToResponse(customer));
    }

    public async Task<ServiceResult<CustomerResponse>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        var errors = CustomerValidator.Validate(request.FullName, request.Email, request.Phone, request.Address);
        if (errors.Count > 0)
            return ServiceResult<CustomerResponse>.Invalid(errors);

        var email = CustomerValidator.NormalizeEmail(request.Email!);
        if (await _repository.EmailExistsAsync(email, null, ct))
            return ServiceResult<CustomerResponse>.Invalid("email", "A customer with this email already exists.");

        var customer = new Customer
        {
            FullName = request.FullName!.Trim(),
            Email = email,
            Phone = request.Phone!.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(customer, ct);
        return ServiceResult<CustomerResponse>.Ok(ToResponse(customer));
    }

    public async Task<ServiceResult<CustomerResponse>> UpdateAsync(int id, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        var errors = CustomerValidator.Validate(request.FullName, request.Email, request.Phone, request.Address);
        if (errors.Count > 0)
            return ServiceResult<CustomerResponse>.Invalid(errors);

        var customer = await _repository.GetByIdAsync(id, ct);
        if (customer is null)
            return NotFound(id);

        var email = CustomerValidator.NormalizeEmail(request.Email!);
        if (await _repository.EmailExistsAsync(email, id, ct))
            return ServiceResult<CustomerResponse>.Invalid("email", "A customer with this email already exists.");

        customer.FullName = request.FullName!.Trim();
        customer.Email = email;
        customer.Phone = request.Phone!.Trim();
        customer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();

        await _repository.UpdateAsync(customer, ct);
        return ServiceResult<CustomerResponse>.Ok(ToResponse(customer));
    }

    private static ServiceResult<CustomerResponse> NotFound(int id) =>
        ServiceResult<CustomerResponse>.NotFound($"No customer exists with id {id}.");

    public static CustomerResponse ToResponse(Customer c) => new()
    {
        Id = c.Id,
        FullName = c.FullName,
        Email = c.Email,
        Phone = c.Phone,
        Address = c.Address,
        HasAccount = c.UserId is not null || c.User is not null,
        CreatedAt = c.CreatedAt
    };
}
