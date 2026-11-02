using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;

namespace Showroom.Api.Services;

/// <summary>
/// Vehicle business logic. Depends only on IVehicleRepository (no EF Core, no HTTP),
/// so it can be unit-tested with a mocked repository.
/// </summary>
public class VehicleService : IVehicleService
{
    private readonly IVehicleRepository _repository;

    public VehicleService(IVehicleRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<VehicleResponse>> GetAllAsync(VehicleQuery filter, CancellationToken ct = default)
    {
        var vehicles = await _repository.GetAllAsync(filter, ct);
        return vehicles.Select(ToResponse).ToList();   // no match -> empty list (200), not an error
    }

    public async Task<ServiceResult<VehicleResponse>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var vehicle = await _repository.GetByIdAsync(id, ct);
        return vehicle is null
            ? NotFound(id)
            : ServiceResult<VehicleResponse>.Ok(ToResponse(vehicle));
    }

    public async Task<ServiceResult<VehicleResponse>> CreateAsync(CreateVehicleRequest request, CancellationToken ct = default)
    {
        // 1) Validate first: invalid data never reaches the database
        var errors = VehicleValidator.Validate(request.Brand, request.Model, request.ManufacturingYear,
                                               request.Price, request.Color, request.Mileage);
        if (errors.Count > 0)
            return ServiceResult<VehicleResponse>.Invalid(errors);

        // 2) Map and save. A new vehicle is always Available.
        var vehicle = new Vehicle
        {
            Brand = request.Brand!.Value,
            Model = request.Model!.Trim(),
            ManufacturingYear = request.ManufacturingYear!.Value,
            Price = request.Price!.Value,
            Color = request.Color!.Trim(),
            Mileage = request.Mileage!.Value,
            AvailabilityStatus = AvailabilityStatus.Available,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(vehicle, ct);
        return ServiceResult<VehicleResponse>.Ok(ToResponse(vehicle));
    }

    public async Task<ServiceResult<VehicleResponse>> UpdateAsync(int id, UpdateVehicleRequest request, CancellationToken ct = default)
    {
        var errors = VehicleValidator.Validate(request.Brand, request.Model, request.ManufacturingYear,
                                               request.Price, request.Color, request.Mileage);
        if (errors.Count > 0)
            return ServiceResult<VehicleResponse>.Invalid(errors);

        var vehicle = await _repository.GetByIdAsync(id, ct);
        if (vehicle is null)
            return NotFound(id);

        vehicle.Brand = request.Brand!.Value;
        vehicle.Model = request.Model!.Trim();
        vehicle.ManufacturingYear = request.ManufacturingYear!.Value;
        vehicle.Price = request.Price!.Value;
        vehicle.Color = request.Color!.Trim();
        vehicle.Mileage = request.Mileage!.Value;
        // Status and CreatedAt are not changed here (status has its own endpoint)

        await _repository.UpdateAsync(vehicle, ct);
        return ServiceResult<VehicleResponse>.Ok(ToResponse(vehicle));
    }

    public async Task<ServiceResult<VehicleResponse>> UpdateStatusAsync(int id, UpdateVehicleStatusRequest request, CancellationToken ct = default)
    {
        if (request.Status is null || !Enum.IsDefined(request.Status.Value))
            return ServiceResult<VehicleResponse>.Invalid("status", "Status must be Available, Reserved or Sold.");

        var vehicle = await _repository.GetByIdAsync(id, ct);
        if (vehicle is null)
            return NotFound(id);

        // Business rule: a sale is final
        if (vehicle.AvailabilityStatus == AvailabilityStatus.Sold && request.Status != AvailabilityStatus.Sold)
            return ServiceResult<VehicleResponse>.Invalid("status", "A sold vehicle cannot change its status.");

        vehicle.AvailabilityStatus = request.Status.Value;
        await _repository.UpdateAsync(vehicle, ct);
        return ServiceResult<VehicleResponse>.Ok(ToResponse(vehicle));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var vehicle = await _repository.GetByIdAsync(id, ct);
        if (vehicle is null)
            return ServiceResult<bool>.NotFound($"No vehicle exists with id {id}.");

        await _repository.DeleteAsync(vehicle, ct);
        return ServiceResult<bool>.Ok(true);
    }

    private static ServiceResult<VehicleResponse> NotFound(int id) =>
        ServiceResult<VehicleResponse>.NotFound($"No vehicle exists with id {id}.");

    // The only place an entity becomes a response
    public static VehicleResponse ToResponse(Vehicle v) => new()
    {
        Id = v.Id,
        Brand = v.Brand,
        Model = v.Model,
        ManufacturingYear = v.ManufacturingYear,
        Price = v.Price,
        Color = v.Color,
        Mileage = v.Mileage,
        AvailabilityStatus = v.AvailabilityStatus,
        CreatedAt = v.CreatedAt
    };
}
