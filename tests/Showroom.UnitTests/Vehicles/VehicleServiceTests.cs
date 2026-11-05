using Moq;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;
using Showroom.Api.Services;
using static Showroom.UnitTests.Vehicles.VehicleTestData;

namespace Showroom.UnitTests.Vehicles;

public class VehicleServiceTests
{
    private readonly Mock<IVehicleRepository> _repository = new();
    private readonly VehicleService _service;

    public VehicleServiceTests()
    {
        _service = new VehicleService(_repository.Object);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_SavesVehicleAsAvailableAndReturnsIt()
    {
        // Arrange
        var request = ValidCreateRequest();
        Vehicle? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()))
                   .Callback<Vehicle, CancellationToken>((v, _) => saved = v)
                   .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Tiggo 8 Pro", result.Value!.Model);
        Assert.Equal(AvailabilityStatus.Available, result.Value.AvailabilityStatus);

        Assert.NotNull(saved);
        Assert.Equal(Brand.Chery, saved!.Brand);
        Assert.Equal(145_000m, saved.Price);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
        _repository.Verify(r => r.AddAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ModelAndColorWithSpaces_AreTrimmed()
    {
        // Arrange
        var request = ValidCreateRequest();
        request.Model = "  Tiggo 7  ";
        request.Color = " Red ";

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.Equal("Tiggo 7", result.Value!.Model);
        Assert.Equal("Red", result.Value.Color);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-150_000)]
    [InlineData(0)]
    public async Task CreateAsync_PriceNotPositive_ReturnsValidationErrorAndDoesNotSave(decimal price)
    {
        // Arrange
        var request = ValidCreateRequest();
        request.Price = price;

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains("price", result.Errors.Keys);
        _repository.Verify(r => r.AddAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("brand")]
    [InlineData("model")]
    [InlineData("manufacturingYear")]
    [InlineData("price")]
    [InlineData("color")]
    [InlineData("mileage")]
    public async Task CreateAsync_MissingMandatoryField_ReturnsErrorForThatField(string field)
    {
        // Arrange
        var request = ValidCreateRequest();
        switch (field)
        {
            case "brand": request.Brand = null; break;
            case "model": request.Model = "   "; break;
            case "manufacturingYear": request.ManufacturingYear = null; break;
            case "price": request.Price = null; break;
            case "color": request.Color = null; break;
            case "mileage": request.Mileage = null; break;
        }

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Equal(field, Assert.Single(result.Errors.Keys));
        _repository.Verify(r => r.AddAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmptyRequest_ReportsEveryMandatoryField()
    {
        // Arrange
        var request = new CreateVehicleRequest();

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.Equal(6, result.Errors.Count);
    }

    [Theory]
    [InlineData(1989)]
    [InlineData(99)]
    [InlineData(3000)]
    public async Task CreateAsync_InvalidManufacturingYear_ReturnsValidationError(int year)
    {
        var request = ValidCreateRequest();
        request.ManufacturingYear = year;

        var result = await _service.CreateAsync(request);

        Assert.Contains("manufacturingYear", result.Errors.Keys);
    }

    [Fact]
    public async Task CreateAsync_NextYearModel_IsAccepted()
    {
        var request = ValidCreateRequest();
        request.ManufacturingYear = DateTime.UtcNow.Year + 1;

        var result = await _service.CreateAsync(request);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CreateAsync_NegativeMileage_ReturnsValidationError()
    {
        var request = ValidCreateRequest();
        request.Mileage = -10;

        var result = await _service.CreateAsync(request);

        Assert.Contains("mileage", result.Errors.Keys);
    }

    [Fact]
    public async Task CreateAsync_TooLongColorAndUnrealisticMileage_ReturnsBothErrors()
    {
        var request = ValidCreateRequest();
        request.Color = new string('c', 31);
        request.Mileage = 2_000_001;

        var result = await _service.CreateAsync(request);

        Assert.Contains("color", result.Errors.Keys);
        Assert.Contains("mileage", result.Errors.Keys);
    }

    [Fact]
    public async Task CreateAsync_UnknownBrandValue_ReturnsValidationError()
    {
        var request = ValidCreateRequest();
        request.Brand = (Brand)99;

        var result = await _service.CreateAsync(request);

        Assert.Contains("brand", result.Errors.Keys);
    }

    [Fact]
    public async Task CreateAsync_TooLongModelAndPriceAboveMaximum_ReturnsBothErrors()
    {
        var request = ValidCreateRequest();
        request.Model = new string('x', 51);
        request.Price = 10_000_001m;

        var result = await _service.CreateAsync(request);

        Assert.Contains("model", result.Errors.Keys);
        Assert.Contains("price", result.Errors.Keys);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsVehicle()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(ExistingVehicle(7));

        // Act
        var result = await _service.GetByIdAsync(7);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(7, result.Value!.Id);
        Assert.Equal("J7", result.Value.Model);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((Vehicle?)null);

        // Act
        var result = await _service.GetByIdAsync(404);

        // Assert
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetAllAsync_PassesFilterToRepositoryAndMapsResults()
    {
        // Arrange
        var filter = new VehicleQuery { Brand = Brand.JAECOO, Model = "J7", Status = AvailabilityStatus.Available };
        _repository.Setup(r => r.SearchAsync(It.IsAny<VehicleSearch>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(([ExistingVehicle(1), ExistingVehicle(2)], 2));

        // Act
        var result = await _service.GetAllAsync(filter);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.All(result.Value.Items, v => Assert.Equal(Brand.JAECOO, v.Brand));
        _repository.Verify(r => r.SearchAsync(
            It.Is<VehicleSearch>(s => s.Brand == Brand.JAECOO && s.Model == "J7" && s.Status == AvailabilityStatus.Available),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_SearchForUnavailableOrNonExistentVehicle_ReturnsEmptyList()
    {
        // Arrange
        var filter = new VehicleQuery { Brand = Brand.OMODA, Model = "Unknown", Status = AvailabilityStatus.Available };
        _repository.Setup(r => r.SearchAsync(It.IsAny<VehicleSearch>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(([], 0));

        // Act
        var result = await _service.GetAllAsync(filter);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }

    [Fact]
    public async Task UpdateAsync_ExistingVehicle_UpdatesFieldsButKeepsStatusAndCreatedAt()
    {
        // Arrange
        var existing = ExistingVehicle(3, AvailabilityStatus.Reserved);
        var createdAt = existing.CreatedAt;
        _repository.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act
        var result = await _service.UpdateAsync(3, ValidUpdateRequest());

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(Brand.OMODA, result.Value!.Brand);
        Assert.Equal("C5", result.Value.Model);
        Assert.Equal(120_000m, result.Value.Price);
        Assert.Equal(AvailabilityStatus.Reserved, result.Value.AvailabilityStatus);
        Assert.Equal(createdAt, result.Value.CreatedAt);
        _repository.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ReturnsNotFoundAndDoesNotSave()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Vehicle?)null);

        // Act
        var result = await _service.UpdateAsync(999, ValidUpdateRequest());

        // Assert
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_InvalidData_ReturnsValidationErrorWithoutTouchingTheDatabase()
    {
        // Arrange
        var request = ValidUpdateRequest();
        request.Price = -5;

        // Act
        var result = await _service.UpdateAsync(1, request);

        // Assert
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(AvailabilityStatus.Available, AvailabilityStatus.Reserved)]
    [InlineData(AvailabilityStatus.Reserved, AvailabilityStatus.Sold)]
    [InlineData(AvailabilityStatus.Reserved, AvailabilityStatus.Available)]
    public async Task UpdateStatusAsync_AllowedChange_SavesNewStatus(AvailabilityStatus from, AvailabilityStatus to)
    {
        // Arrange
        var vehicle = ExistingVehicle(5, from);
        _repository.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(vehicle);

        // Act
        var result = await _service.UpdateStatusAsync(5, new UpdateVehicleStatusRequest { Status = to });

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(to, result.Value!.AvailabilityStatus);
        _repository.Verify(r => r.UpdateAsync(vehicle, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_SoldVehicleBackToAvailable_IsRejected()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(ExistingVehicle(5, AvailabilityStatus.Sold));

        // Act
        var result = await _service.UpdateStatusAsync(5, new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Available });

        // Assert
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains("status", result.Errors.Keys);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_MissingStatus_ReturnsValidationError()
    {
        var result = await _service.UpdateStatusAsync(5, new UpdateVehicleStatusRequest { Status = null });

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_NonExistentId_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync((Vehicle?)null);

        var result = await _service.UpdateStatusAsync(77, new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Sold });

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_DeletesVehicle()
    {
        // Arrange
        var vehicle = ExistingVehicle(9);
        _repository.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(vehicle);

        // Act
        var result = await _service.DeleteAsync(9);

        // Assert
        Assert.True(result.Succeeded);
        _repository.Verify(r => r.DeleteAsync(vehicle, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_VehicleWithReservations_ReturnsValidationErrorAndDeletesNothing()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingVehicle(9));
        _repository.Setup(r => r.HasReservationsAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _service.DeleteAsync(9);

        // Assert
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        _repository.Verify(r => r.DeleteAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsNotFoundAndDeletesNothing()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(123, It.IsAny<CancellationToken>())).ReturnsAsync((Vehicle?)null);

        // Act
        var result = await _service.DeleteAsync(123);

        // Assert
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        _repository.Verify(r => r.DeleteAsync(It.IsAny<Vehicle>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
