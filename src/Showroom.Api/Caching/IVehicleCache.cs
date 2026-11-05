using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Repositories;

namespace Showroom.Api.Caching;

public interface IVehicleCache
{
    Task<PagedResult<VehicleResponse>> GetListAsync(VehicleSearch search, Func<Task<PagedResult<VehicleResponse>>> load);
    Task<VehicleResponse?> GetDetailAsync(int id, Func<Task<VehicleResponse?>> load);
    void InvalidateLists();
    void InvalidateVehicle(int id);
}
