using Showroom.Api.Common;
using Showroom.Api.DTOs;

namespace Showroom.Api.Services;

public interface IVehicleService
{
    Task<IReadOnlyList<VehicleResponse>> GetAllAsync(VehicleQuery filter, CancellationToken ct = default);
    Task<ServiceResult<VehicleResponse>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<VehicleResponse>> CreateAsync(CreateVehicleRequest request, CancellationToken ct = default);
    Task<ServiceResult<VehicleResponse>> UpdateAsync(int id, UpdateVehicleRequest request, CancellationToken ct = default);
    Task<ServiceResult<VehicleResponse>> UpdateStatusAsync(int id, UpdateVehicleStatusRequest request, CancellationToken ct = default);
    Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct = default);
}
