using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Showroom.Api.Auth;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.IntegrationTests.Infrastructure;
using static Showroom.IntegrationTests.Infrastructure.TestJson;

namespace Showroom.IntegrationTests.Vehicles;

public class VehicleAuthorizationTests : IClassFixture<ShowroomApiFactory>
{
    private readonly ShowroomApiFactory _factory;

    public VehicleAuthorizationTests(ShowroomApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("GET", "/api/vehicles")]
    [InlineData("GET", "/api/vehicles/1")]
    [InlineData("POST", "/api/vehicles")]
    [InlineData("PUT", "/api/vehicles/1")]
    [InlineData("PATCH", "/api/vehicles/1/status")]
    [InlineData("DELETE", "/api/vehicles/1")]
    public async Task AnyVehicleEndpoint_WithoutToken_Returns401(string method, string url)
    {
        // Arrange
        var client = _factory.CreateAnonymousClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = method is "GET" or "DELETE" ? null : JsonContent.Create(new { })
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicles_WithTamperedToken_Returns401()
    {
        // Arrange
        var customer = await _factory.CreateClientForRoleAsync(Roles.Customer);
        var token = customer.DefaultRequestHeaders.Authorization!.Parameter!;
        var client = _factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        // Act
        var response = await client.GetAsync("/api/vehicles");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_CanListAndViewVehicles()
    {
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);
        var vehicle = await admin.CreateVehicleAsync();
        var customer = await _factory.CreateClientForRoleAsync(Roles.Customer);

        var list = await customer.GetAsync("/api/vehicles");
        var single = await customer.GetAsync($"/api/vehicles/{vehicle.Id}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotAddVehicle_Returns403()
    {
        var customer = await _factory.CreateClientForRoleAsync(Roles.Customer);

        var response = await customer.PostVehicleAsync(NewVehicle());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotUpdateOrChangeStatus_Returns403()
    {
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);
        var vehicle = await admin.CreateVehicleAsync();
        var customer = await _factory.CreateClientForRoleAsync(Roles.Customer);

        var put = await customer.PutAsJsonAsync($"/api/vehicles/{vehicle.Id}", new UpdateVehicleRequest(), Options);
        var patch = await customer.PatchAsJsonAsync($"/api/vehicles/{vehicle.Id}/status",
            new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Sold }, Options);

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, patch.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotDeleteVehicle_Returns403AndVehicleStillExists()
    {
        // Arrange
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);
        var vehicle = await admin.CreateVehicleAsync();
        var customer = await _factory.CreateClientForRoleAsync(Roles.Customer);

        // Act
        var response = await customer.DeleteAsync($"/api/vehicles/{vehicle.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/vehicles/{vehicle.Id}")).StatusCode);
    }

    [Fact]
    public async Task SalesRep_CanAddUpdateAndChangeStatus()
    {
        var sales = await _factory.CreateClientForRoleAsync(Roles.SalesRep);

        var post = await sales.PostVehicleAsync(NewVehicle());
        var created = await post.ReadAsync<VehicleResponse>();
        var put = await sales.PutAsJsonAsync($"/api/vehicles/{created.Id}", new UpdateVehicleRequest
        {
            Brand = Brand.Chery, Model = "Tiggo 9", ManufacturingYear = 2026, Price = 180_000m, Color = "Silver", Mileage = 0
        }, Options);
        var patch = await sales.PatchAsJsonAsync($"/api/vehicles/{created.Id}/status",
            new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Reserved }, Options);

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
    }

    [Fact]
    public async Task SalesRep_CannotDeleteVehicle_Returns403()
    {
        var sales = await _factory.CreateClientForRoleAsync(Roles.SalesRep);
        var vehicle = await sales.CreateVehicleAsync();

        var response = await sales.DeleteAsync($"/api/vehicles/{vehicle.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanDeleteVehicle_Returns204()
    {
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);
        var vehicle = await admin.CreateVehicleAsync();

        var response = await admin.DeleteAsync($"/api/vehicles/{vehicle.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
