using Showroom.Api.Caching;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;

namespace Showroom.Api.Services;

public class VehicleService : IVehicleService
{
    private readonly IVehicleRepository _repository;
    private readonly IVehicleCache _cache;

    public VehicleService(IVehicleRepository repository, IVehicleCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<ServiceResult<PagedResult<VehicleResponse>>> GetAllAsync(VehicleQuery query, CancellationToken ct = default)
    {
        var errors = VehicleQueryValidator.Validate(query);
        if (errors.Count > 0)
            return ServiceResult<PagedResult<VehicleResponse>>.Invalid(errors);

        var search = VehicleQueryValidator.ToSearch(query);
        var page = await _cache.GetListAsync(search, async () =>
        {
            var (vehicles, totalCount) = await _repository.SearchAsync(search, ct);
            return new PagedResult<VehicleResponse>
            {
                Items = vehicles.Select(ToResponse).ToList(),
                Page = search.Page,
                PageSize = search.PageSize,
                TotalCount = totalCount
            };
        });

        return ServiceResult<PagedResult<VehicleResponse>>.Ok(page);
    }

    public async Task<ServiceResult<VehicleResponse>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var vehicle = await _cache.GetDetailAsync(id, async () =>
        {
            var entity = await _repository.GetByIdAsync(id, ct);
            return entity is null ? null : ToResponse(entity);
        });

        return vehicle is null
            ? NotFound(id)
            : ServiceResult<VehicleResponse>.Ok(vehicle);
    }

    public async Task<ServiceResult<VehicleResponse>> CreateAsync(CreateVehicleRequest request, CancellationToken ct = default)
    {
        var errors = VehicleValidator.Validate(request.Brand, request.Model, request.ManufacturingYear,
                                               request.Price, request.Color, request.Mileage);
        if (errors.Count > 0)
            return ServiceResult<VehicleResponse>.Invalid(errors);

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
        _cache.InvalidateLists();
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

        await _repository.UpdateAsync(vehicle, ct);
        _cache.InvalidateVehicle(id);
        return ServiceResult<VehicleResponse>.Ok(ToResponse(vehicle));
    }

    public async Task<ServiceResult<VehicleResponse>> UpdateStatusAsync(int id, UpdateVehicleStatusRequest request, CancellationToken ct = default)
    {
        if (request.Status is null || !Enum.IsDefined(request.Status.Value))
            return ServiceResult<VehicleResponse>.Invalid("status", "Status must be Available, Reserved or Sold.");

        var vehicle = await _repository.GetByIdAsync(id, ct);
        if (vehicle is null)
            return NotFound(id);

        if (vehicle.AvailabilityStatus == AvailabilityStatus.Sold && request.Status != AvailabilityStatus.Sold)
            return ServiceResult<VehicleResponse>.Invalid("status", "A sold vehicle cannot change its status.");

        vehicle.AvailabilityStatus = request.Status.Value;
        await _repository.UpdateAsync(vehicle, ct);
        _cache.InvalidateVehicle(id);
        return ServiceResult<VehicleResponse>.Ok(ToResponse(vehicle));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var vehicle = await _repository.GetByIdAsync(id, ct);
        if (vehicle is null)
            return ServiceResult<bool>.NotFound($"No vehicle exists with id {id}.");

        if (await _repository.HasReservationsAsync(id, ct))
            return ServiceResult<bool>.Invalid("id", "This vehicle has reservations and cannot be deleted. Mark it as Sold instead.");

        await _repository.DeleteAsync(vehicle, ct);
        _cache.InvalidateVehicle(id);
        return ServiceResult<bool>.Ok(true);
    }

    private static ServiceResult<VehicleResponse> NotFound(int id) =>
        ServiceResult<VehicleResponse>.NotFound($"No vehicle exists with id {id}.");

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
