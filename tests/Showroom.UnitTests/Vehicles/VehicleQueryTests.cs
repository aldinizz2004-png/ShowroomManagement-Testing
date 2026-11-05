using Moq;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;
using Showroom.Api.Services;
using static Showroom.UnitTests.Vehicles.VehicleTestData;

namespace Showroom.UnitTests.Vehicles;

public class VehicleQueryTests
{
    private readonly Mock<IVehicleRepository> _repository = new();
    private readonly VehicleService _service;

    public VehicleQueryTests()
    {
        _service = new VehicleService(_repository.Object);
    }

    [Fact]
    public void Validate_DefaultQuery_HasNoErrors()
    {
        // Arrange
        var query = new VehicleQuery();

        // Act
        var errors = VehicleQueryValidator.Validate(query);

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(0, 10, null, null, null, null, null, "page")]
    [InlineData(1, 0, null, null, null, null, null, "pageSize")]
    [InlineData(1, 51, null, null, null, null, null, "pageSize")]
    [InlineData(1, 10, 1989, null, null, null, null, "year")]
    [InlineData(1, 10, null, -1, null, null, null, "minPrice")]
    [InlineData(1, 10, null, null, -1, null, null, "maxPrice")]
    [InlineData(1, 10, null, 200000, 100000, null, null, "minPrice")]
    [InlineData(1, 10, null, null, null, "color", null, "sortBy")]
    [InlineData(1, 10, null, null, null, null, "up", "sortOrder")]
    public void Validate_InvalidParameter_ReturnsErrorForThatField(
        int page, int pageSize, int? year, int? minPrice, int? maxPrice, string? sortBy, string? sortOrder, string field)
    {
        // Arrange
        var query = new VehicleQuery
        {
            Page = page,
            PageSize = pageSize,
            Year = year,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            SortBy = sortBy,
            SortOrder = sortOrder
        };

        // Act
        var errors = VehicleQueryValidator.Validate(query);

        // Assert
        Assert.True(errors.ContainsKey(field));
    }

    [Fact]
    public void Validate_TooLongModelAndColor_ReturnsErrors()
    {
        var query = new VehicleQuery { Model = new string('m', 51), Color = new string('c', 31) };

        var errors = VehicleQueryValidator.Validate(query);

        Assert.True(errors.ContainsKey("model"));
        Assert.True(errors.ContainsKey("color"));
    }

    [Fact]
    public void Validate_EqualMinAndMaxPrice_IsAllowed()
    {
        var query = new VehicleQuery { MinPrice = 120_000m, MaxPrice = 120_000m };

        Assert.Empty(VehicleQueryValidator.Validate(query));
    }

    [Theory]
    [InlineData("price", VehicleSortField.Price)]
    [InlineData("PRICE", VehicleSortField.Price)]
    [InlineData("model", VehicleSortField.Model)]
    [InlineData("year", VehicleSortField.ManufacturingYear)]
    [InlineData("manufacturingYear", VehicleSortField.ManufacturingYear)]
    [InlineData(null, VehicleSortField.Id)]
    public void ToSearch_MapsSortByToSortField(string? sortBy, VehicleSortField expected)
    {
        var search = VehicleQueryValidator.ToSearch(new VehicleQuery { SortBy = sortBy });

        Assert.Equal(expected, search.SortBy);
    }

    [Theory]
    [InlineData("desc", true)]
    [InlineData("DESC", true)]
    [InlineData("asc", false)]
    [InlineData(null, false)]
    public void ToSearch_MapsSortOrder(string? sortOrder, bool descending)
    {
        var search = VehicleQueryValidator.ToSearch(new VehicleQuery { SortOrder = sortOrder });

        Assert.Equal(descending, search.Descending);
    }

    [Fact]
    public void ToSearch_TrimsTextFiltersAndDropsBlankOnes()
    {
        var search = VehicleQueryValidator.ToSearch(new VehicleQuery { Model = "  Tiggo 8 ", Color = "   " });

        Assert.Equal("Tiggo 8", search.Model);
        Assert.Null(search.Color);
    }

    [Fact]
    public async Task GetAllAsync_CombinedFilters_PassesAllCriteriaToRepository()
    {
        // Arrange
        var query = new VehicleQuery
        {
            Brand = Brand.Chery,
            Model = "Tiggo",
            Year = 2025,
            MinPrice = 80_000m,
            MaxPrice = 150_000m,
            Color = "White",
            Status = AvailabilityStatus.Available,
            SortBy = "price",
            SortOrder = "asc",
            Page = 2,
            PageSize = 5
        };
        _repository.Setup(r => r.SearchAsync(It.IsAny<VehicleSearch>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(([], 0));

        // Act
        await _service.GetAllAsync(query);

        // Assert
        var expected = new VehicleSearch
        {
            Brand = Brand.Chery,
            Model = "Tiggo",
            Year = 2025,
            MinPrice = 80_000m,
            MaxPrice = 150_000m,
            Color = "White",
            Status = AvailabilityStatus.Available,
            SortBy = VehicleSortField.Price,
            Descending = false,
            Page = 2,
            PageSize = 5
        };
        _repository.Verify(r => r.SearchAsync(expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_InvalidQuery_ReturnsValidationErrorAndDoesNotQueryDatabase()
    {
        // Arrange
        var query = new VehicleQuery { MinPrice = 150_000m, MaxPrice = 80_000m, PageSize = 500 };

        // Act
        var result = await _service.GetAllAsync(query);

        // Assert
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.True(result.Errors.ContainsKey("minPrice"));
        Assert.True(result.Errors.ContainsKey("pageSize"));
        _repository.Verify(r => r.SearchAsync(It.IsAny<VehicleSearch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_MiddlePage_ReturnsPaginationMetadata()
    {
        // Arrange
        _repository.Setup(r => r.SearchAsync(It.IsAny<VehicleSearch>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(([ExistingVehicle(11), ExistingVehicle(12)], 25));

        // Act
        var result = await _service.GetAllAsync(new VehicleQuery { Page = 2, PageSize = 10 });

        // Assert
        var page = result.Value!;
        Assert.Equal(2, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(25, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task GetAllAsync_LastPage_HasNoNextPage()
    {
        _repository.Setup(r => r.SearchAsync(It.IsAny<VehicleSearch>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(([ExistingVehicle(21)], 21));

        var result = await _service.GetAllAsync(new VehicleQuery { Page = 3, PageSize = 10 });

        Assert.Equal(3, result.Value!.TotalPages);
        Assert.False(result.Value.HasNextPage);
    }

    [Fact]
    public void PagedResult_NoItems_HasZeroPages()
    {
        var page = new PagedResult<VehicleResponse> { Page = 1, PageSize = 10, TotalCount = 0 };

        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasNextPage);
        Assert.False(page.HasPreviousPage);
    }
}
