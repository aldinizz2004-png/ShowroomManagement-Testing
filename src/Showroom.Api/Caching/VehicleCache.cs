using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Repositories;

namespace Showroom.Api.Caching;

public sealed class VehicleCache : IVehicleCache, IDisposable
{
    private const string ListPrefix = "vehicles:list:";
    private const string DetailPrefix = "vehicles:detail:";

    private readonly IMemoryCache _cache;
    private readonly VehicleCacheOptions _options;
    private readonly ILogger<VehicleCache> _logger;
    private readonly object _sync = new();
    private CancellationTokenSource _lists = new();
    private long _version;

    public VehicleCache(IMemoryCache cache, IOptions<VehicleCacheOptions> options, ILogger<VehicleCache> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public static string ListKey(VehicleSearch search) => ListPrefix + search;
    public static string DetailKey(int id) => DetailPrefix + id;

    public async Task<PagedResult<VehicleResponse>> GetListAsync(VehicleSearch search, Func<Task<PagedResult<VehicleResponse>>> load)
    {
        if (!_options.Enabled)
            return await load();

        var key = ListKey(search);
        if (_cache.TryGetValue(key, out PagedResult<VehicleResponse>? cached) && cached is not null)
        {
            _logger.LogDebug("Cache hit {Key}", key);
            return cached;
        }

        _logger.LogDebug("Cache miss {Key}", key);
        var version = Interlocked.Read(ref _version);
        var token = CurrentListsToken();
        var value = await load();

        if (Interlocked.Read(ref _version) == version)
        {
            var entry = new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = _options.ListSlidingExpiration,
                AbsoluteExpirationRelativeToNow = _options.ListAbsoluteExpiration
            };
            entry.AddExpirationToken(new CancellationChangeToken(token));
            _cache.Set(key, value, entry);
        }

        return value;
    }

    public async Task<VehicleResponse?> GetDetailAsync(int id, Func<Task<VehicleResponse?>> load)
    {
        if (!_options.Enabled)
            return await load();

        var key = DetailKey(id);
        if (_cache.TryGetValue(key, out VehicleResponse? cached) && cached is not null)
        {
            _logger.LogDebug("Cache hit {Key}", key);
            return cached;
        }

        _logger.LogDebug("Cache miss {Key}", key);
        var version = Interlocked.Read(ref _version);
        var value = await load();

        if (value is not null && Interlocked.Read(ref _version) == version)
        {
            _cache.Set(key, value, new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = _options.DetailSlidingExpiration,
                AbsoluteExpirationRelativeToNow = _options.DetailAbsoluteExpiration
            });
        }

        return value;
    }

    public void InvalidateLists()
    {
        Interlocked.Increment(ref _version);

        CancellationTokenSource old;
        lock (_sync)
        {
            old = _lists;
            _lists = new CancellationTokenSource();
        }

        old.Cancel();
        _logger.LogDebug("Vehicle list cache invalidated");
    }

    public void InvalidateVehicle(int id)
    {
        Interlocked.Increment(ref _version);
        _cache.Remove(DetailKey(id));
        InvalidateLists();
    }

    public void Dispose()
    {
        lock (_sync)
            _lists.Dispose();
    }

    private CancellationToken CurrentListsToken()
    {
        lock (_sync)
            return _lists.Token;
    }
}
