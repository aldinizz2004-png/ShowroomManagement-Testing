using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Showroom.Api.Auth;
using Showroom.Api.Data;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.IntegrationTests.Infrastructure;
using static Showroom.IntegrationTests.Infrastructure.TestJson;

namespace Showroom.IntegrationTests.Vehicles;

public class VehicleCachingTests : IClassFixture<ShowroomApiFactory>
{
    private static readonly DateOnly NextWeek = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
    private readonly ShowroomApiFactory _factory;

    public VehicleCachingTests(ShowroomApiFactory factory)
    {
        _factory = factory;
    }

    private Task<HttpClient> AdminAsync() => _factory.CreateClientForRoleAsync(Roles.Admin);

    private async Task ChangePriceBehindTheApiAsync(int vehicleId, decimal price)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Vehicles.Where(v => v.Id == vehicleId)
                         .ExecuteUpdateAsync(s => s.SetProperty(v => v.Price, price));
    }

    private static async Task<VehicleResponse> GetVehicleAsync(HttpClient client, int id) =>
        await (await client.GetAsync($"/api/vehicles/{id}")).ReadAsync<VehicleResponse>();

    [Fact]
    public async Task GetById_SecondRequest_IsServedFromCache()
    {
        // Arrange
        var admin = await AdminAsync();
        var vehicle = await admin.CreateVehicleAsync(NewVehicle(price: 100_000m));
        await GetVehicleAsync(admin, vehicle.Id);
        await ChangePriceBehindTheApiAsync(vehicle.Id, 1m);

        // Act
        var cached = await GetVehicleAsync(admin, vehicle.Id);

        // Assert
        Assert.Equal(100_000m, cached.Price);
    }

    [Fact]
    public async Task GetAll_SameQueryAgain_IsServedFromCache()
    {
        // Arrange
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var vehicle = await admin.CreateVehicleAsync(NewVehicle($"Tiggo {tag}", price: 100_000m));
        await admin.GetAsync($"/api/vehicles?model={tag}");
        await ChangePriceBehindTheApiAsync(vehicle.Id, 1m);

        // Act
        var items = await (await admin.GetAsync($"/api/vehicles?model={tag}")).ReadVehicleItemsAsync();

        // Assert
        Assert.Equal(100_000m, Assert.Single(items).Price);
    }

    [Fact]
    public async Task Put_InvalidatesCache_NextReadReturnsUpdatedVehicle()
    {
        // Arrange
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var vehicle = await admin.CreateVehicleAsync(NewVehicle($"Tiggo {tag}", price: 100_000m));
        await GetVehicleAsync(admin, vehicle.Id);
        await admin.GetAsync($"/api/vehicles?model={tag}");

        // Act
        var update = new UpdateVehicleRequest
        {
            Brand = Brand.Chery,
            Model = $"Tiggo {tag}",
            ManufacturingYear = 2025,
            Price = 125_000m,
            Color = "Black",
            Mileage = 10
        };
        await admin.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", update, Options);
        var detail = await GetVehicleAsync(admin, vehicle.Id);
        var items = await (await admin.GetAsync($"/api/vehicles?model={tag}")).ReadVehicleItemsAsync();

        // Assert
        Assert.Equal(125_000m, detail.Price);
        Assert.Equal(125_000m, Assert.Single(items).Price);
    }

    [Fact]
    public async Task Post_InvalidatesListCache_NewVehicleAppearsImmediately()
    {
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var before = await (await admin.GetAsync($"/api/vehicles?model={tag}")).ReadVehiclePageAsync();

        await admin.CreateVehicleAsync(NewVehicle($"C5 {tag}", Brand.OMODA));
        var after = await (await admin.GetAsync($"/api/vehicles?model={tag}")).ReadVehiclePageAsync();

        Assert.Equal(0, before.TotalCount);
        Assert.Equal(1, after.TotalCount);
    }

    [Fact]
    public async Task StatusChange_InvalidatesCache_SoldVehicleLeavesAvailableList()
    {
        // Arrange
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var vehicle = await admin.CreateVehicleAsync(NewVehicle($"J7 {tag}", Brand.JAECOO));
        await admin.GetAsync($"/api/vehicles?model={tag}&status=Available");
        await GetVehicleAsync(admin, vehicle.Id);

        // Act
        await admin.PatchAsJsonAsync($"/api/vehicles/{vehicle.Id}/status",
            new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Sold }, Options);
        var available = await (await admin.GetAsync($"/api/vehicles?model={tag}&status=Available")).ReadVehicleItemsAsync();
        var detail = await GetVehicleAsync(admin, vehicle.Id);

        // Assert
        Assert.Empty(available);
        Assert.Equal(AvailabilityStatus.Sold, detail.AvailabilityStatus);
    }

    [Fact]
    public async Task Delete_InvalidatesCache_DeletedVehicleReturns404()
    {
        var admin = await AdminAsync();
        var vehicle = await admin.CreateVehicleAsync();
        await GetVehicleAsync(admin, vehicle.Id);

        await admin.DeleteAsync($"/api/vehicles/{vehicle.Id}");
        var response = await admin.GetAsync($"/api/vehicles/{vehicle.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reservation_InvalidatesCache_CustomerNoLongerSeesReservedVehicle()
    {
        // Arrange
        var admin = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var vehicle = await admin.CreateVehicleAsync(NewVehicle($"Tiggo {tag}"));
        var (customer, _) = await _factory.CreateClientForNewCustomerAsync();
        var before = await (await customer.GetAsync($"/api/vehicles?model={tag}")).ReadVehicleItemsAsync();
        await GetVehicleAsync(admin, vehicle.Id);

        // Act
        var reserve = await customer.PostAsJsonAsync("/api/reservations",
            new CreateReservationRequest { VehicleId = vehicle.Id, ExpectedPurchaseDate = NextWeek }, Options);
        var after = await (await customer.GetAsync($"/api/vehicles?model={tag}")).ReadVehicleItemsAsync();
        var detail = await GetVehicleAsync(admin, vehicle.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Created, reserve.StatusCode);
        Assert.Single(before);
        Assert.Empty(after);
        Assert.Equal(AvailabilityStatus.Reserved, detail.AvailabilityStatus);
    }
}
