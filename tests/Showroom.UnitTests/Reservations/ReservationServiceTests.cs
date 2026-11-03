using Moq;
using Showroom.Api.Auth;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;
using Showroom.Api.Services;

namespace Showroom.UnitTests.Reservations;

public class ReservationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    private static readonly CurrentUser Admin = new(1, Roles.Admin);
    private static readonly CurrentUser Sales = new(2, Roles.SalesRep);
    private static readonly CurrentUser Ali = new(10, Roles.Customer);
    private static readonly CurrentUser Mona = new(11, Roles.Customer);

    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IVehicleRepository> _vehicles = new();
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly ReservationService _service;

    private readonly Customer _aliProfile = new() { Id = 100, FullName = "Ali", Email = "ali@test.com", Phone = "0599000001", UserId = 10 };
    private readonly Customer _monaProfile = new() { Id = 101, FullName = "Mona", Email = "mona@test.com", Phone = "0599000002", UserId = 11 };

    public ReservationServiceTests()
    {
        _customers.Setup(c => c.GetByUserIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(_aliProfile);
        _customers.Setup(c => c.GetByUserIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(_monaProfile);
        _customers.Setup(c => c.GetByIdAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(_aliProfile);
        _service = new ReservationService(_reservations.Object, _vehicles.Object, _customers.Object, new FixedClock(Now));
    }

    private Vehicle SetupVehicle(int id, AvailabilityStatus status = AvailabilityStatus.Available)
    {
        var vehicle = new Vehicle { Id = id, Brand = Brand.Chery, Model = "Tiggo 8", ManufacturingYear = 2025, Price = 140_000m, Color = "White", AvailabilityStatus = status };
        _vehicles.Setup(v => v.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(vehicle);
        return vehicle;
    }

    private Reservation SetupReservation(int id, Customer owner, ReservationStatus status = ReservationStatus.Active)
    {
        var reservation = new Reservation
        {
            Id = id,
            Customer = owner,
            CustomerId = owner.Id,
            Vehicle = new Vehicle { Id = 50, Brand = Brand.OMODA, Model = "C5", ManufacturingYear = 2024, AvailabilityStatus = AvailabilityStatus.Reserved },
            VehicleId = 50,
            ReservationDate = Now.UtcDateTime,
            ExpectedPurchaseDate = Today.AddDays(10),
            Status = status
        };
        _reservations.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        return reservation;
    }

    private static CreateReservationRequest Request(int vehicleId = 5, int daysAhead = 7, int? customerId = null) => new()
    {
        VehicleId = vehicleId,
        ExpectedPurchaseDate = Today.AddDays(daysAhead),
        CustomerId = customerId
    };

    private void VerifyNothingSaved() =>
        _reservations.Verify(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task CreateAsync_CustomerReservesAvailableVehicle_UsesOwnProfileAndMarksVehicleReserved()
    {
        // Arrange
        var vehicle = SetupVehicle(5);
        Reservation? saved = null;
        _reservations.Setup(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
                     .Callback<Reservation, CancellationToken>((r, _) => saved = r)
                     .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateAsync(Request(5), Ali);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(100, saved!.CustomerId);
        Assert.Equal(ReservationStatus.Active, saved.Status);
        Assert.Equal(Now.UtcDateTime, saved.ReservationDate);
        Assert.Equal(AvailabilityStatus.Reserved, vehicle.AvailabilityStatus);
        Assert.Equal("Chery Tiggo 8 2025", result.Value!.Vehicle);
    }

    [Theory]
    [InlineData(AvailabilityStatus.Reserved)]
    [InlineData(AvailabilityStatus.Sold)]
    public async Task CreateAsync_VehicleNotAvailable_ReturnsValidationErrorAndSavesNothing(AvailabilityStatus status)
    {
        // Arrange
        var vehicle = SetupVehicle(5, status);

        // Act
        var result = await _service.CreateAsync(Request(5), Ali);

        // Assert
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains("vehicleId", result.Errors.Keys);
        Assert.Equal(status, vehicle.AvailabilityStatus);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task CreateAsync_VehicleDoesNotExist_ReturnsValidationError()
    {
        _vehicles.Setup(v => v.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((Vehicle?)null);

        var result = await _service.CreateAsync(Request(404), Ali);

        Assert.Contains("vehicleId", result.Errors.Keys);
        VerifyNothingSaved();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(91)]
    public async Task CreateAsync_ExpectedPurchaseDateOutOfRange_ReturnsValidationError(int daysAhead)
    {
        var result = await _service.CreateAsync(Request(5, daysAhead), Ali);

        Assert.Contains("expectedPurchaseDate", result.Errors.Keys);
        VerifyNothingSaved();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    public async Task CreateAsync_ExpectedPurchaseDateOnBoundary_IsAccepted(int daysAhead)
    {
        SetupVehicle(5);

        var result = await _service.CreateAsync(Request(5, daysAhead), Ali);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CreateAsync_MissingVehicleAndDate_ReturnsBothErrors()
    {
        var result = await _service.CreateAsync(new CreateReservationRequest(), Ali);

        Assert.Contains("vehicleId", result.Errors.Keys);
        Assert.Contains("expectedPurchaseDate", result.Errors.Keys);
    }

    [Fact]
    public async Task CreateAsync_CustomerForAnotherCustomer_ReturnsForbidden()
    {
        // Arrange
        SetupVehicle(5);

        // Act
        var result = await _service.CreateAsync(Request(5, customerId: 101), Ali);

        // Assert
        Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task CreateAsync_CustomerAccountWithoutProfile_ReturnsValidationError()
    {
        var result = await _service.CreateAsync(Request(5), new CurrentUser(99, Roles.Customer));

        Assert.Contains("customerId", result.Errors.Keys);
    }

    [Fact]
    public async Task CreateAsync_StaffWithoutCustomerId_ReturnsValidationError()
    {
        var result = await _service.CreateAsync(Request(5), Sales);

        Assert.Contains("customerId", result.Errors.Keys);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task CreateAsync_StaffForUnknownCustomer_ReturnsValidationError()
    {
        _customers.Setup(c => c.GetByIdAsync(555, It.IsAny<CancellationToken>())).ReturnsAsync((Customer?)null);

        var result = await _service.CreateAsync(Request(5, customerId: 555), Sales);

        Assert.Contains("customerId", result.Errors.Keys);
    }

    [Fact]
    public async Task CreateAsync_StaffForExistingCustomer_Succeeds()
    {
        SetupVehicle(5);

        var result = await _service.CreateAsync(Request(5, customerId: 100), Sales);

        Assert.True(result.Succeeded);
        Assert.Equal(100, result.Value!.CustomerId);
        _reservations.Verify(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_OwnReservation_ReturnsIt()
    {
        SetupReservation(7, _aliProfile);

        var result = await _service.GetByIdAsync(7, Ali);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task GetByIdAsync_AnotherCustomersReservation_ReturnsForbidden()
    {
        // Arrange
        SetupReservation(7, _aliProfile);

        // Act
        var result = await _service.GetByIdAsync(7, Mona);

        // Assert
        Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetByIdAsync_StaffCanViewAnyReservation()
    {
        SetupReservation(7, _aliProfile);

        Assert.True((await _service.GetByIdAsync(7, Admin)).Succeeded);
        Assert.True((await _service.GetByIdAsync(7, Sales)).Succeeded);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentReservation_ReturnsNotFound()
    {
        _reservations.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((Reservation?)null);

        var result = await _service.GetByIdAsync(404, Admin);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetAllAsync_Customer_AlwaysFiltersByOwnProfile()
    {
        // Arrange
        _reservations.Setup(r => r.GetAllAsync(100, null, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        // Act
        await _service.GetAllAsync(new ReservationQuery { CustomerId = 101 }, Ali);

        // Assert
        _reservations.Verify(r => r.GetAllAsync(100, null, It.IsAny<CancellationToken>()), Times.Once);
        _reservations.Verify(r => r.GetAllAsync(101, It.IsAny<ReservationStatus?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_Staff_UsesRequestedFilters()
    {
        _reservations.Setup(r => r.GetAllAsync(101, ReservationStatus.Active, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _service.GetAllAsync(new ReservationQuery { CustomerId = 101, Status = ReservationStatus.Active }, Sales);

        _reservations.Verify(r => r.GetAllAsync(101, ReservationStatus.Active, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_CustomerWithoutProfile_ReturnsEmptyList()
    {
        var result = await _service.GetAllAsync(new ReservationQuery(), new CurrentUser(99, Roles.Customer));

        Assert.Empty(result);
        _reservations.Verify(r => r.GetAllAsync(It.IsAny<int?>(), It.IsAny<ReservationStatus?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_OwnActiveReservation_CancelsAndReleasesVehicle()
    {
        // Arrange
        var reservation = SetupReservation(7, _aliProfile);

        // Act
        var result = await _service.CancelAsync(7, Ali);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(AvailabilityStatus.Available, reservation.Vehicle.AvailabilityStatus);
        _reservations.Verify(r => r.UpdateAsync(reservation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_AnotherCustomersReservation_ReturnsForbiddenAndChangesNothing()
    {
        // Arrange
        var reservation = SetupReservation(7, _aliProfile);

        // Act
        var result = await _service.CancelAsync(7, Mona);

        // Assert
        Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
        Assert.Equal(ReservationStatus.Active, reservation.Status);
        _reservations.Verify(r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Completed)]
    public async Task CancelAsync_NotActive_ReturnsValidationError(ReservationStatus status)
    {
        SetupReservation(7, _aliProfile, status);

        var result = await _service.CancelAsync(7, Ali);

        Assert.Contains("status", result.Errors.Keys);
    }

    [Fact]
    public async Task CancelAsync_StaffCanCancelAnyReservation()
    {
        SetupReservation(7, _aliProfile);

        var result = await _service.CancelAsync(7, Sales);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CancelAsync_NonExistent_ReturnsNotFound()
    {
        _reservations.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((Reservation?)null);

        var result = await _service.CancelAsync(404, Ali);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task CompleteAsync_Staff_CompletesAndMarksVehicleSold()
    {
        // Arrange
        var reservation = SetupReservation(7, _aliProfile);

        // Act
        var result = await _service.CompleteAsync(7, Sales);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStatus.Completed, reservation.Status);
        Assert.Equal(AvailabilityStatus.Sold, reservation.Vehicle.AvailabilityStatus);
    }

    [Fact]
    public async Task CompleteAsync_Customer_ReturnsForbidden()
    {
        SetupReservation(7, _aliProfile);

        var result = await _service.CompleteAsync(7, Ali);

        Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
        _reservations.Verify(r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteAsync_CancelledReservation_ReturnsValidationError()
    {
        SetupReservation(7, _aliProfile, ReservationStatus.Cancelled);

        var result = await _service.CompleteAsync(7, Admin);

        Assert.Contains("status", result.Errors.Keys);
    }

    [Fact]
    public async Task CompleteAsync_NonExistent_ReturnsNotFound()
    {
        _reservations.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((Reservation?)null);

        var result = await _service.CompleteAsync(404, Admin);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
