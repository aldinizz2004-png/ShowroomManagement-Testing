using System.Net;
using System.Net.Http.Json;
using Showroom.Api.Auth;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.IntegrationTests.Infrastructure;
using static Showroom.IntegrationTests.Infrastructure.TestJson;

namespace Showroom.IntegrationTests.Vehicles;

public class CustomerVehicleVisibilityTests : IClassFixture<ShowroomApiFactory>
{
    private readonly ShowroomApiFactory _factory;

    public CustomerVehicleVisibilityTests(ShowroomApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(VehicleResponse Available, VehicleResponse Sold, string Tag)> CreateAvailableAndSoldAsync()
    {
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var available = await admin.CreateVehicleAsync(NewVehicle($"Tiggo {tag} A"));
        var sold = await admin.CreateVehicleAsync(NewVehicle($"Tiggo {tag} S"));
        await admin.PatchAsJsonAsync($"/api/vehicles/{sold.Id}/status",
            new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Sold }, Options);
        return (available, sold, tag);
    }

    [Fact]
    public async Task Customer_ListVehicles_SeesOnlyAvailableOnes()
    {
        // Arrange
        var (available, _, tag) = await CreateAvailableAndSoldAsync();
        var customer = await _factory.CreateClientForRoleAsync(Roles.Customer);

        // Act
        var list = await (await customer.GetAsync($"/api/vehicles?model={tag}")).ReadAsync<List<VehicleResponse>>();

        // Assert
        Assert.Equal(available.Id, Assert.Single(list).Id);
    }

    [Fact]
    public async Task Customer_CannotBypassFilterWithStatusSold()
    {
        var (_, _, tag) = await CreateAvailableAndSoldAsync();
        var customer = await _factory.CreateClientForRoleAsync(Roles.Customer);

        var list = await (await customer.GetAsync($"/api/vehicles?model={tag}&status=Sold")).ReadAsync<List<VehicleResponse>>();

        Assert.All(list, v => Assert.Equal(AvailabilityStatus.Available, v.AvailabilityStatus));
    }

    [Fact]
    public async Task Customer_GetSoldVehicleById_Returns404()
    {
        var (_, sold, _) = await CreateAvailableAndSoldAsync();
        var customer = await _factory.CreateClientForRoleAsync(Roles.Customer);

        var response = await customer.GetAsync($"/api/vehicles/{sold.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SalesRep_ListVehicles_SeesAllStatuses()
    {
        var (_, _, tag) = await CreateAvailableAndSoldAsync();
        var sales = await _factory.CreateClientForRoleAsync(Roles.SalesRep);

        var list = await (await sales.GetAsync($"/api/vehicles?model={tag}")).ReadAsync<List<VehicleResponse>>();

        Assert.Equal(2, list.Count);
    }
}
