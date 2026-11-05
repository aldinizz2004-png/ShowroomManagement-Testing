using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Showroom.Api.Caching;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;

namespace Showroom.UnitTests.Caching;

public class VehicleCacheTests : IDisposable
{
    private readonly MemoryCache _memory = new(new MemoryCacheOptions { SizeLimit = 1024 });
    private int _loads;

    public void Dispose() => _memory.Dispose();

    private VehicleCache CreateCache(VehicleCacheOptions? options = null) =>
        new(_memory, Options.Create(options ?? new VehicleCacheOptions()), NullLogger<VehicleCache>.Instance);

    private Task<VehicleResponse?> LoadVehicle(int id)
    {
        _loads++;
        return Task.FromResult<VehicleResponse?>(new VehicleResponse { Id = id, Brand = Brand.Chery, Model = $"Tiggo {_loads}" });
    }

    private Task<PagedResult<VehicleResponse>> LoadPage()
    {
        _loads++;
        return Task.FromResult(new PagedResult<VehicleResponse> { Page = 1, PageSize = 10, TotalCount = _loads });
    }

    [Fact]
    public async Task GetDetailAsync_FirstCall_IsCacheMissAndLoadsFromSource()
    {
        // Arrange
        var cache = CreateCache();

        // Act
        var vehicle = await cache.GetDetailAsync(1, () => LoadVehicle(1));

        // Assert
        Assert.Equal(1, _loads);
        Assert.Equal("Tiggo 1", vehicle!.Model);
    }

    [Fact]
    public async Task GetDetailAsync_SecondCall_IsCacheHitAndDoesNotLoadAgain()
    {
        // Arrange
        var cache = CreateCache();
        await cache.GetDetailAsync(1, () => LoadVehicle(1));

        // Act
        var vehicle = await cache.GetDetailAsync(1, () => LoadVehicle(1));

        // Assert
        Assert.Equal(1, _loads);
        Assert.Equal("Tiggo 1", vehicle!.Model);
    }

    [Fact]
    public async Task GetDetailAsync_MissingVehicle_IsNotCached()
    {
        var cache = CreateCache();

        await cache.GetDetailAsync(9, () => { _loads++; return Task.FromResult<VehicleResponse?>(null); });
        await cache.GetDetailAsync(9, () => { _loads++; return Task.FromResult<VehicleResponse?>(null); });

        Assert.Equal(2, _loads);
    }

    [Fact]
    public async Task GetListAsync_SameSearch_IsCacheHit()
    {
        // Arrange
        var cache = CreateCache();
        var search = new VehicleSearch { Brand = Brand.Chery, MinPrice = 80_000m, Page = 1 };

        // Act
        await cache.GetListAsync(search, LoadPage);
        var second = await cache.GetListAsync(search with { }, LoadPage);

        // Assert
        Assert.Equal(1, _loads);
        Assert.Equal(1, second.TotalCount);
    }

    [Fact]
    public async Task GetListAsync_DifferentSearch_IsCacheMiss()
    {
        var cache = CreateCache();

        await cache.GetListAsync(new VehicleSearch { Page = 1 }, LoadPage);
        await cache.GetListAsync(new VehicleSearch { Page = 2 }, LoadPage);
        await cache.GetListAsync(new VehicleSearch { Page = 1, SortBy = VehicleSortField.Price }, LoadPage);

        Assert.Equal(3, _loads);
    }

    [Fact]
    public async Task InvalidateVehicle_RemovesDetailAndAllLists()
    {
        // Arrange
        var cache = CreateCache();
        var search = new VehicleSearch { Brand = Brand.OMODA };
        await cache.GetDetailAsync(1, () => LoadVehicle(1));
        await cache.GetListAsync(search, LoadPage);

        // Act
        cache.InvalidateVehicle(1);
        var vehicle = await cache.GetDetailAsync(1, () => LoadVehicle(1));
        var page = await cache.GetListAsync(search, LoadPage);

        // Assert
        Assert.Equal(4, _loads);
        Assert.Equal("Tiggo 3", vehicle!.Model);
        Assert.Equal(4, page.TotalCount);
    }

    [Fact]
    public async Task InvalidateVehicle_KeepsOtherVehiclesCached()
    {
        var cache = CreateCache();
        await cache.GetDetailAsync(1, () => LoadVehicle(1));
        await cache.GetDetailAsync(2, () => LoadVehicle(2));

        cache.InvalidateVehicle(1);
        await cache.GetDetailAsync(2, () => LoadVehicle(2));

        Assert.Equal(2, _loads);
    }

    [Fact]
    public async Task InvalidateLists_RemovesListsButKeepsDetails()
    {
        var cache = CreateCache();
        await cache.GetDetailAsync(1, () => LoadVehicle(1));
        await cache.GetListAsync(new VehicleSearch(), LoadPage);

        cache.InvalidateLists();
        await cache.GetDetailAsync(1, () => LoadVehicle(1));
        await cache.GetListAsync(new VehicleSearch(), LoadPage);

        Assert.Equal(3, _loads);
    }

    [Fact]
    public async Task InvalidationDuringLoad_DoesNotCacheStaleValue()
    {
        // Arrange
        var cache = CreateCache();

        // Act
        await cache.GetDetailAsync(1, async () =>
        {
            var stale = await LoadVehicle(1);
            cache.InvalidateVehicle(1);
            return stale;
        });
        await cache.GetDetailAsync(1, () => LoadVehicle(1));

        // Assert
        Assert.Equal(2, _loads);
    }

    [Fact]
    public async Task AbsoluteExpiration_EntryExpiresEvenWhenUsed()
    {
        // Arrange
        var cache = CreateCache(new VehicleCacheOptions
        {
            DetailSlidingExpiration = TimeSpan.FromMinutes(5),
            DetailAbsoluteExpiration = TimeSpan.FromMilliseconds(200)
        });
        await cache.GetDetailAsync(1, () => LoadVehicle(1));

        // Act
        await Task.Delay(450);
        await cache.GetDetailAsync(1, () => LoadVehicle(1));

        // Assert
        Assert.Equal(2, _loads);
    }

    [Fact]
    public async Task SlidingExpiration_RenewedByAccess_ExpiresWhenIdle()
    {
        // Arrange
        var cache = CreateCache(new VehicleCacheOptions
        {
            ListSlidingExpiration = TimeSpan.FromMilliseconds(400),
            ListAbsoluteExpiration = TimeSpan.FromMinutes(5)
        });
        var search = new VehicleSearch();
        await cache.GetListAsync(search, LoadPage);

        // Act
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(150);
            await cache.GetListAsync(search, LoadPage);
        }
        var loadsWhileActive = _loads;

        await Task.Delay(900);
        await cache.GetListAsync(search, LoadPage);

        // Assert
        Assert.Equal(1, loadsWhileActive);
        Assert.Equal(2, _loads);
    }

    [Fact]
    public async Task Disabled_AlwaysLoadsFromSource()
    {
        var cache = CreateCache(new VehicleCacheOptions { Enabled = false });

        await cache.GetDetailAsync(1, () => LoadVehicle(1));
        await cache.GetDetailAsync(1, () => LoadVehicle(1));
        await cache.GetListAsync(new VehicleSearch(), LoadPage);
        await cache.GetListAsync(new VehicleSearch(), LoadPage);

        Assert.Equal(4, _loads);
    }

    [Fact]
    public void ListKey_DependsOnEverySearchValue()
    {
        var a = VehicleCache.ListKey(new VehicleSearch { Brand = Brand.Chery, Page = 1 });
        var b = VehicleCache.ListKey(new VehicleSearch { Brand = Brand.Chery, Page = 1 });
        var c = VehicleCache.ListKey(new VehicleSearch { Brand = Brand.Chery, Page = 1, Status = AvailabilityStatus.Available });

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
