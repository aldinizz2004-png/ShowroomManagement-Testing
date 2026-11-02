using System.Net;
using System.Net.Http.Json;
using Showroom.Api.Auth;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.IntegrationTests.Infrastructure;
using static Showroom.IntegrationTests.Infrastructure.TestJson;

namespace Showroom.IntegrationTests.Vehicles;

/// <summary>
/// CRUD and error scenarios through real HTTP requests (as Admin).
/// IClassFixture: one API + one test database shared by the tests in this class;
/// every test creates its own vehicles, so the tests do not depend on each other.
/// </summary>
public class VehicleCrudTests : IClassFixture<ShowroomApiFactory>
{
    private readonly ShowroomApiFactory _factory;

    public VehicleCrudTests(ShowroomApiFactory factory)
    {
        _factory = factory;
    }

    private Task<HttpClient> AdminAsync() => _factory.CreateClientForRoleAsync(Roles.Admin);

    // ====================== Create ======================

    [Fact]
    public async Task Post_ValidVehicle_Returns201WithLocationAndBody()
    {
        // Arrange
        var client = await AdminAsync();
        var request = NewVehicle("Tiggo 8 Pro");

        // Act
        var response = await client.PostVehicleAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadAsync<VehicleResponse>();
        Assert.True(body.Id > 0);
        Assert.Equal("Tiggo 8 Pro", body.Model);
        Assert.Equal(AvailabilityStatus.Available, body.AvailabilityStatus);
        Assert.Equal($"/api/vehicles/{body.Id}", response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Post_NegativePrice_Returns400WithPriceError()
    {
        var client = await AdminAsync();

        var response = await client.PostVehicleAsync(NewVehicle(price: -100));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("price", problem.Errors.Keys);
    }

    [Fact]
    public async Task Post_MissingFields_Returns400ListingEveryMissingField()
    {
        var client = await AdminAsync();

        var response = await client.PostAsJsonAsync("/api/vehicles", new { brand = "OMODA" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(new[] { "color", "manufacturingYear", "mileage", "model", "price" }, problem.Errors.Keys.Order().ToArray());
    }

    [Fact]
    public async Task Post_UnknownBrand_Returns400()
    {
        var client = await AdminAsync();

        var response = await client.PostAsJsonAsync("/api/vehicles",
            new { brand = "Toyota", model = "Corolla", manufacturingYear = 2024, price = 90000, color = "Red", mileage = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ====================== Read ======================

    [Fact]
    public async Task GetById_ExistingVehicle_Returns200()
    {
        var client = await AdminAsync();
        var created = await client.CreateVehicleAsync();

        var response = await client.GetAsync($"/api/vehicles/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Model, (await response.ReadAsync<VehicleResponse>()).Model);
    }

    [Fact]
    public async Task GetById_NonExistentVehicle_Returns404()
    {
        var client = await AdminAsync();

        var response = await client.GetAsync("/api/vehicles/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task GetById_InvalidId_Returns400(string id)
    {
        var client = await AdminAsync();

        var response = await client.GetAsync($"/api/vehicles/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_FilterByBrandAndModel_ReturnsOnlyMatchingVehicles()
    {
        // Arrange: a unique model name, so other tests' vehicles do not interfere
        var client = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        await client.CreateVehicleAsync(NewVehicle($"C5 {tag}", Brand.OMODA));
        await client.CreateVehicleAsync(NewVehicle($"C5 X {tag}", Brand.OMODA));
        await client.CreateVehicleAsync(NewVehicle($"J7 {tag}", Brand.JAECOO));

        // Act: model filter is "contains" and case-insensitive
        var response = await client.GetAsync($"/api/vehicles?brand=OMODA&model=c5%20x%20{tag}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.ReadAsync<List<VehicleResponse>>();
        var only = Assert.Single(list);
        Assert.Equal($"C5 X {tag}", only.Model);
    }

    [Fact]
    public async Task GetAll_StatusAvailable_ExcludesSoldVehicles()
    {
        var client = await AdminAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var available = await client.CreateVehicleAsync(NewVehicle($"Tiggo {tag} A"));
        var sold = await client.CreateVehicleAsync(NewVehicle($"Tiggo {tag} S"));
        await client.PatchAsJsonAsync($"/api/vehicles/{sold.Id}/status",
            new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Sold }, Options);

        var list = await (await client.GetAsync($"/api/vehicles?model={tag}&status=Available")).ReadAsync<List<VehicleResponse>>();

        Assert.Equal(available.Id, Assert.Single(list).Id);
    }

    [Fact]
    public async Task GetAll_NoMatch_Returns200WithEmptyList()
    {
        var client = await AdminAsync();

        var response = await client.GetAsync($"/api/vehicles?model=does-not-exist-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.ReadAsync<List<VehicleResponse>>());
    }

    // ====================== Update ======================

    [Fact]
    public async Task Put_ExistingVehicle_Returns200AndChangesAreSaved()
    {
        // Arrange
        var client = await AdminAsync();
        var created = await client.CreateVehicleAsync();
        var update = new UpdateVehicleRequest
        {
            Brand = Brand.JAECOO, Model = "J8", ManufacturingYear = 2026, Price = 210_000m, Color = "Green", Mileage = 10
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/vehicles/{created.Id}", update, Options);

        // Assert: response and a fresh GET both show the new values
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reloaded = await (await client.GetAsync($"/api/vehicles/{created.Id}")).ReadAsync<VehicleResponse>();
        Assert.Equal("J8", reloaded.Model);
        Assert.Equal(Brand.JAECOO, reloaded.Brand);
        Assert.Equal(210_000m, reloaded.Price);
    }

    [Fact]
    public async Task Put_NonExistentVehicle_Returns404()
    {
        var client = await AdminAsync();
        var update = new UpdateVehicleRequest
        {
            Brand = Brand.Chery, Model = "Arrizo 8", ManufacturingYear = 2025, Price = 99_000m, Color = "Blue", Mileage = 0
        };

        var response = await client.PutAsJsonAsync("/api/vehicles/999999", update, Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_InvalidData_Returns400()
    {
        var client = await AdminAsync();
        var created = await client.CreateVehicleAsync();

        var response = await client.PutAsJsonAsync($"/api/vehicles/{created.Id}", new UpdateVehicleRequest { Model = "X" }, Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ====================== Status ======================

    [Fact]
    public async Task PatchStatus_Reserved_Returns200WithNewStatus()
    {
        var client = await AdminAsync();
        var created = await client.CreateVehicleAsync();

        var response = await client.PatchAsJsonAsync($"/api/vehicles/{created.Id}/status",
            new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Reserved }, Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AvailabilityStatus.Reserved, (await response.ReadAsync<VehicleResponse>()).AvailabilityStatus);
    }

    [Fact]
    public async Task PatchStatus_SoldBackToAvailable_Returns400()
    {
        var client = await AdminAsync();
        var created = await client.CreateVehicleAsync();
        await client.PatchAsJsonAsync($"/api/vehicles/{created.Id}/status",
            new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Sold }, Options);

        var response = await client.PatchAsJsonAsync($"/api/vehicles/{created.Id}/status",
            new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Available }, Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_NonExistentVehicle_Returns404()
    {
        var client = await AdminAsync();

        var response = await client.PatchAsJsonAsync("/api/vehicles/999999/status",
            new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Sold }, Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ====================== Delete ======================

    [Fact]
    public async Task Delete_ExistingVehicle_Returns204ThenGetReturns404()
    {
        var client = await AdminAsync();
        var created = await client.CreateVehicleAsync();

        var delete = await client.DeleteAsync($"/api/vehicles/{created.Id}");
        var getAfter = await client.GetAsync($"/api/vehicles/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getAfter.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentVehicle_Returns404()
    {
        var client = await AdminAsync();

        var response = await client.DeleteAsync("/api/vehicles/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
