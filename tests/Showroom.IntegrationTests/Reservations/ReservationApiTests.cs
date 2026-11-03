using System.Net;
using System.Net.Http.Json;
using Showroom.Api.Auth;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.IntegrationTests.Infrastructure;
using static Showroom.IntegrationTests.Infrastructure.TestJson;

namespace Showroom.IntegrationTests.Reservations;

public class ReservationApiTests : IClassFixture<ShowroomApiFactory>
{
    private readonly ShowroomApiFactory _factory;
    private static readonly DateOnly NextWeek = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);

    public ReservationApiTests(ShowroomApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<VehicleResponse> NewAvailableVehicleAsync()
    {
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);
        return await admin.CreateVehicleAsync();
    }

    private static Task<HttpResponseMessage> ReserveAsync(HttpClient client, int vehicleId, int? customerId = null, DateOnly? date = null) =>
        client.PostAsJsonAsync("/api/reservations",
            new CreateReservationRequest { VehicleId = vehicleId, CustomerId = customerId, ExpectedPurchaseDate = date ?? NextWeek }, Options);

    private async Task<AvailabilityStatus> VehicleStatusAsync(int vehicleId)
    {
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);
        return (await (await admin.GetAsync($"/api/vehicles/{vehicleId}")).ReadAsync<VehicleResponse>()).AvailabilityStatus;
    }

    [Fact]
    public async Task Customer_ReservesAvailableVehicle_Returns201AndVehicleBecomesReserved()
    {
        // Arrange
        var vehicle = await NewAvailableVehicleAsync();
        var (customer, customerId) = await _factory.CreateClientForNewCustomerAsync();

        // Act
        var response = await ReserveAsync(customer, vehicle.Id);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadAsync<ReservationResponse>();
        Assert.Equal(customerId, body.CustomerId);
        Assert.Equal(ReservationStatus.Active, body.Status);
        Assert.Equal($"/api/reservations/{body.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal(AvailabilityStatus.Reserved, await VehicleStatusAsync(vehicle.Id));
    }

    [Fact]
    public async Task ReservingAnAlreadyReservedVehicle_Returns400()
    {
        // Arrange
        var vehicle = await NewAvailableVehicleAsync();
        var (first, _) = await _factory.CreateClientForNewCustomerAsync();
        var (second, _) = await _factory.CreateClientForNewCustomerAsync();
        await ReserveAsync(first, vehicle.Id);

        // Act
        var response = await ReserveAsync(second, vehicle.Id);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("vehicleId", (await response.ReadProblemAsync()).Errors.Keys);
    }

    [Fact]
    public async Task ReservingASoldVehicle_Returns400()
    {
        var vehicle = await NewAvailableVehicleAsync();
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);
        await admin.PatchAsJsonAsync($"/api/vehicles/{vehicle.Id}/status",
            new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Sold }, Options);
        var (customer, _) = await _factory.CreateClientForNewCustomerAsync();

        var response = await ReserveAsync(customer, vehicle.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidReservations_Return400()
    {
        // Arrange
        var vehicle = await NewAvailableVehicleAsync();
        var (customer, _) = await _factory.CreateClientForNewCustomerAsync();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        // Act
        var pastDate = await ReserveAsync(customer, vehicle.Id, date: yesterday);
        var unknownVehicle = await ReserveAsync(customer, 999999);
        var empty = await customer.PostAsJsonAsync("/api/reservations", new { });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, pastDate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownVehicle.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(AvailabilityStatus.Available, await VehicleStatusAsync(vehicle.Id));
    }

    [Fact]
    public async Task Customer_CannotViewOrCancelAnotherCustomersReservation_Returns403()
    {
        // Arrange
        var vehicle = await NewAvailableVehicleAsync();
        var (owner, _) = await _factory.CreateClientForNewCustomerAsync();
        var (other, _) = await _factory.CreateClientForNewCustomerAsync();
        var reservation = await (await ReserveAsync(owner, vehicle.Id)).ReadAsync<ReservationResponse>();

        // Act
        var view = await other.GetAsync($"/api/reservations/{reservation.Id}");
        var cancel = await other.PatchAsync($"/api/reservations/{reservation.Id}/cancel", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, view.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/reservations/{reservation.Id}")).StatusCode);
    }

    [Fact]
    public async Task Customer_CannotReserveForAnotherCustomer_Returns403()
    {
        var vehicle = await NewAvailableVehicleAsync();
        var (customer, _) = await _factory.CreateClientForNewCustomerAsync();
        var (_, otherCustomerId) = await _factory.CreateClientForNewCustomerAsync();

        var response = await ReserveAsync(customer, vehicle.Id, otherCustomerId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_ListReservations_SeesOnlyOwn()
    {
        // Arrange
        var (alice, aliceId) = await _factory.CreateClientForNewCustomerAsync();
        var (bob, _) = await _factory.CreateClientForNewCustomerAsync();
        await ReserveAsync(alice, (await NewAvailableVehicleAsync()).Id);
        await ReserveAsync(bob, (await NewAvailableVehicleAsync()).Id);

        // Act: even asking for another customer's id returns only her own
        var list = await (await alice.GetAsync("/api/reservations?customerId=1")).ReadAsync<List<ReservationResponse>>();

        // Assert
        Assert.All(list, r => Assert.Equal(aliceId, r.CustomerId));
        Assert.Single(list);
    }

    [Fact]
    public async Task Customer_CancelsOwnReservation_VehicleBecomesAvailableAgain()
    {
        // Arrange
        var vehicle = await NewAvailableVehicleAsync();
        var (customer, _) = await _factory.CreateClientForNewCustomerAsync();
        var reservation = await (await ReserveAsync(customer, vehicle.Id)).ReadAsync<ReservationResponse>();

        // Act
        var cancel = await customer.PatchAsync($"/api/reservations/{reservation.Id}/cancel", null);
        var cancelAgain = await customer.PatchAsync($"/api/reservations/{reservation.Id}/cancel", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(ReservationStatus.Cancelled, (await cancel.ReadAsync<ReservationResponse>()).Status);
        Assert.Equal(HttpStatusCode.BadRequest, cancelAgain.StatusCode);
        Assert.Equal(AvailabilityStatus.Available, await VehicleStatusAsync(vehicle.Id));
    }

    [Fact]
    public async Task SalesRep_ReservesForCustomer_AndCompletesSale_VehicleBecomesSold()
    {
        // Arrange
        var vehicle = await NewAvailableVehicleAsync();
        var (_, customerId) = await _factory.CreateClientForNewCustomerAsync();
        var sales = await _factory.CreateClientForRoleAsync(Roles.SalesRep);

        // Act
        var create = await ReserveAsync(sales, vehicle.Id, customerId);
        var reservation = await create.ReadAsync<ReservationResponse>();
        var complete = await sales.PatchAsync($"/api/reservations/{reservation.Id}/complete", null);

        // Assert
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal(ReservationStatus.Completed, (await complete.ReadAsync<ReservationResponse>()).Status);
        Assert.Equal(AvailabilityStatus.Sold, await VehicleStatusAsync(vehicle.Id));
    }

    [Fact]
    public async Task SalesRep_ReserveWithoutCustomerId_Returns400()
    {
        var vehicle = await NewAvailableVehicleAsync();
        var sales = await _factory.CreateClientForRoleAsync(Roles.SalesRep);

        var response = await ReserveAsync(sales, vehicle.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("customerId", (await response.ReadProblemAsync()).Errors.Keys);
    }

    [Fact]
    public async Task Customer_CannotCompleteSale_Returns403()
    {
        var vehicle = await NewAvailableVehicleAsync();
        var (customer, _) = await _factory.CreateClientForNewCustomerAsync();
        var reservation = await (await ReserveAsync(customer, vehicle.Id)).ReadAsync<ReservationResponse>();

        var response = await customer.PatchAsync($"/api/reservations/{reservation.Id}/complete", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanViewAnyReservation_AndMissingOneReturns404()
    {
        var vehicle = await NewAvailableVehicleAsync();
        var (customer, _) = await _factory.CreateClientForNewCustomerAsync();
        var reservation = await (await ReserveAsync(customer, vehicle.Id)).ReadAsync<ReservationResponse>();
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/reservations/{reservation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/reservations/999999")).StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/reservations")]
    [InlineData("GET", "/api/reservations/1")]
    [InlineData("POST", "/api/reservations")]
    [InlineData("PATCH", "/api/reservations/1/cancel")]
    [InlineData("PATCH", "/api/reservations/1/complete")]
    public async Task ReservationEndpoints_WithoutToken_Return401(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = method == "POST" ? JsonContent.Create(new { }) : null
        };

        var response = await _factory.CreateAnonymousClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeletingVehicleWithReservation_Returns400()
    {
        var vehicle = await NewAvailableVehicleAsync();
        var (customer, _) = await _factory.CreateClientForNewCustomerAsync();
        await ReserveAsync(customer, vehicle.Id);
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);

        var response = await admin.DeleteAsync($"/api/vehicles/{vehicle.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
