using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Showroom.Api.Caching;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;
using Showroom.Api.Services;
using static Showroom.UnitTests.Vehicles.VehicleTestData;

namespace Showroom.UnitTests.Vehicles;

public class VehicleServiceCachingTests : IDisposable
{
    private readonly Mock<IVehicleRepository> _repository = new();
    private readonly MemoryCache _memory = new(new MemoryCacheOptions { SizeLimit = 1024 });
    private readonly VehicleService _service;

    public VehicleServiceCachingTests()
    {
        var cache = new VehicleCache(_memory, Options.Create(new VehicleCacheOptions()), NullLogger<VehicleCache>.Instance);
        _service = new VehicleService(_repository.Object, cache);
        _repository.Setup(r => r.SearchAsync(It.IsAny<VehicleSearch>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(([ExistingVehicle(1)], 1));
    }

    public void Dispose() => _memory.Dispose();

    private void VerifySearches(int times) =>
        _repository.Verify(r => r.SearchAsync(It.IsAny<VehicleSearch>(), It.IsAny<CancellationToken>()), Times.Exactly(times));

    [Fact]
    public async Task GetByIdAsync_RepeatedCalls_HitDatabaseOnce()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingVehicle(1));

        // Act
        await _service.GetByIdAsync(1);
        await _service.GetByIdAsync(1);
        var result = await _service.GetByIdAsync(1);

        // Assert
        Assert.True(result.Succeeded);
        _repository.Verify(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_RepeatedSameQuery_HitsDatabaseOnce()
    {
        var query = new VehicleQuery { Brand = Brand.JAECOO, SortBy = "price" };

        await _service.GetAllAsync(query);
        await _service.GetAllAsync(new VehicleQuery { Brand = Brand.JAECOO, SortBy = "PRICE" });

        VerifySearches(1);
    }

    [Fact]
    public async Task GetAllAsync_InvalidQuery_IsNotCachedOrSearched()
    {
        await _service.GetAllAsync(new VehicleQuery { Page = 0 });

        VerifySearches(0);
    }

    [Fact]
    public async Task UpdateAsync_InvalidatesDetail_NextReadReturnsNewData()
    {
        // Arrange
        var stored = ExistingVehicle(1);
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(() => stored);
        await _service.GetByIdAsync(1);

        // Act
        await _service.UpdateAsync(1, ValidUpdateRequest());
        var after = await _service.GetByIdAsync(1);

        // Assert
        Assert.Equal("C5", after.Value!.Model);
        Assert.Equal(120_000m, after.Value.Price);
    }

    [Fact]
    public async Task CreateAsync_InvalidatesLists()
    {
        await _service.GetAllAsync(new VehicleQuery());

        await _service.CreateAsync(ValidCreateRequest());
        await _service.GetAllAsync(new VehicleQuery());

        VerifySearches(2);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidatesDetailAndLists()
    {
        // Arrange
        var stored = ExistingVehicle(1);
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(() => stored);
        await _service.GetByIdAsync(1);
        await _service.GetAllAsync(new VehicleQuery());

        // Act
        await _service.UpdateStatusAsync(1, new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Reserved });
        var detail = await _service.GetByIdAsync(1);
        await _service.GetAllAsync(new VehicleQuery());

        // Assert
        Assert.Equal(AvailabilityStatus.Reserved, detail.Value!.AvailabilityStatus);
        VerifySearches(2);
    }

    [Fact]
    public async Task DeleteAsync_InvalidatesDetail_NextReadReturnsNotFound()
    {
        // Arrange
        Vehicle? stored = ExistingVehicle(1);
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(() => stored);
        _repository.Setup(r => r.DeleteAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()))
                   .Callback(() => stored = null)
                   .Returns(Task.CompletedTask);
        await _service.GetByIdAsync(1);

        // Act
        await _service.DeleteAsync(1);
        var after = await _service.GetByIdAsync(1);

        // Assert
        Assert.False(after.Succeeded);
    }

    [Fact]
    public async Task FailedUpdate_DoesNotInvalidateCache()
    {
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingVehicle(1));
        await _service.GetAllAsync(new VehicleQuery());

        await _service.UpdateAsync(1, new UpdateVehicleRequest());
        await _service.GetAllAsync(new VehicleQuery());

        VerifySearches(1);
    }
}
