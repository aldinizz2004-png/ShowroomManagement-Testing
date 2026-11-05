using System.Net;
using System.Net.Http.Json;
using Showroom.Api.Auth;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.IntegrationTests.Infrastructure;
using static Showroom.IntegrationTests.Infrastructure.TestJson;

namespace Showroom.IntegrationTests.Vehicles;

public class VehicleFilteringTests : IClassFixture<ShowroomApiFactory>
{
    private readonly ShowroomApiFactory _factory;

    public VehicleFilteringTests(ShowroomApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, string Tag)> SeedAsync()
    {
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);
        var tag = Guid.NewGuid().ToString("N")[..8];

        await admin.CreateVehicleAsync(Vehicle($"Tiggo 7 {tag}", Brand.Chery, 2025, 95_000m, "White"));
        await admin.CreateVehicleAsync(Vehicle($"Tiggo 8 {tag}", Brand.Chery, 2025, 145_000m, "Black"));
        await admin.CreateVehicleAsync(Vehicle($"Arrizo 8 {tag}", Brand.Chery, 2024, 120_000m, "White"));
        await admin.CreateVehicleAsync(Vehicle($"C5 {tag}", Brand.OMODA, 2025, 110_000m, "Red"));
        await admin.CreateVehicleAsync(Vehicle($"J7 {tag}", Brand.JAECOO, 2024, 160_000m, "Grey"));
        var sold = await admin.CreateVehicleAsync(Vehicle($"Tiggo 9 {tag}", Brand.Chery, 2025, 175_000m, "White"));
        await admin.PatchAsJsonAsync($"/api/vehicles/{sold.Id}/status",
            new UpdateVehicleStatusRequest { Status = AvailabilityStatus.Sold }, Options);

        return (admin, tag);
    }

    private static CreateVehicleRequest Vehicle(string model, Brand brand, int year, decimal price, string color) => new()
    {
        Brand = brand,
        Model = model,
        ManufacturingYear = year,
        Price = price,
        Color = color,
        Mileage = 0
    };

    [Theory]
    [InlineData("brand=Chery", 4)]
    [InlineData("brand=JAECOO", 1)]
    [InlineData("year=2024", 2)]
    [InlineData("minPrice=100000&maxPrice=150000", 3)]
    [InlineData("minPrice=95000&maxPrice=95000", 1)]
    [InlineData("minPrice=150000", 2)]
    [InlineData("maxPrice=110000", 2)]
    [InlineData("color=white", 3)]
    [InlineData("status=Sold", 1)]
    public async Task GetAll_SingleFilter_ReturnsOnlyMatchingVehicles(string filter, int expected)
    {
        // Arrange
        var (client, tag) = await SeedAsync();

        // Act
        var response = await client.GetAsync($"/api/vehicles?model={tag}&{filter}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.ReadVehiclePageAsync();
        Assert.Equal(expected, page.TotalCount);
        Assert.Equal(expected, page.Items.Count);
    }

    [Fact]
    public async Task GetAll_CombinedFilters_AppliesAllOfThem()
    {
        // Arrange
        var (client, tag) = await SeedAsync();

        // Act
        var response = await client.GetAsync(
            $"/api/vehicles?model={tag}&brand=Chery&year=2025&minPrice=80000&maxPrice=150000&color=White&status=Available");

        // Assert
        var page = await response.ReadVehiclePageAsync();
        var only = Assert.Single(page.Items);
        Assert.Equal($"Tiggo 7 {tag}", only.Model);
    }

    [Fact]
    public async Task GetAll_ExampleQueryFromSpecification_ReturnsFilteredSortedPage()
    {
        // Arrange
        var (client, tag) = await SeedAsync();

        // Act
        var response = await client.GetAsync(
            $"/api/vehicles?model={tag}&brand=Chery&minPrice=80000&maxPrice=150000&year=2025&page=1&pageSize=10&sortBy=price&sortOrder=asc");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.ReadVehiclePageAsync();
        Assert.Equal([95_000m, 145_000m], page.Items.Select(v => v.Price));
        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(2, page.TotalCount);
    }

    [Theory]
    [InlineData("price", "asc")]
    [InlineData("price", "desc")]
    [InlineData("model", "asc")]
    [InlineData("model", "desc")]
    [InlineData("year", "asc")]
    [InlineData("year", "desc")]
    public async Task GetAll_SortBy_ReturnsItemsInRequestedOrder(string sortBy, string sortOrder)
    {
        // Arrange
        var (client, tag) = await SeedAsync();

        // Act
        var items = await (await client.GetAsync($"/api/vehicles?model={tag}&sortBy={sortBy}&sortOrder={sortOrder}"))
            .ReadVehicleItemsAsync();

        // Assert
        Assert.Equal(6, items.Count);
        var expected = (sortBy, sortOrder) switch
        {
            ("price", "asc") => items.OrderBy(v => v.Price),
            ("price", _) => items.OrderByDescending(v => v.Price),
            ("model", "asc") => items.OrderBy(v => v.Model, StringComparer.Ordinal),
            ("model", _) => items.OrderByDescending(v => v.Model, StringComparer.Ordinal),
            (_, "asc") => items.OrderBy(v => v.ManufacturingYear).ThenBy(v => v.Id),
            _ => items.OrderByDescending(v => v.ManufacturingYear).ThenBy(v => v.Id)
        };
        Assert.Equal(expected.Select(v => v.Id), items.Select(v => v.Id));
    }

    [Fact]
    public async Task GetAll_SecondPage_ReturnsPaginationMetadata()
    {
        // Arrange
        var (client, tag) = await SeedAsync();

        // Act
        var page = await (await client.GetAsync($"/api/vehicles?model={tag}&page=2&pageSize=2")).ReadVehiclePageAsync();

        // Assert
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(6, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task GetAll_AllPages_ReturnEveryVehicleExactlyOnce()
    {
        // Arrange
        var (client, tag) = await SeedAsync();
        var ids = new List<int>();

        // Act
        for (var p = 1; p <= 3; p++)
        {
            var items = await (await client.GetAsync($"/api/vehicles?model={tag}&sortBy=year&page={p}&pageSize=2"))
                .ReadVehicleItemsAsync();
            ids.AddRange(items.Select(v => v.Id));
        }

        // Assert
        Assert.Equal(6, ids.Count);
        Assert.Equal(6, ids.Distinct().Count());
    }

    [Fact]
    public async Task GetAll_PageBeyondLast_ReturnsEmptyItemsWithTotalCount()
    {
        var (client, tag) = await SeedAsync();

        var page = await (await client.GetAsync($"/api/vehicles?model={tag}&page=5&pageSize=2")).ReadVehiclePageAsync();

        Assert.Empty(page.Items);
        Assert.Equal(6, page.TotalCount);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task GetAll_NoMatchingVehicles_ReturnsEmptyPage()
    {
        // Arrange
        var (client, tag) = await SeedAsync();

        // Act
        var response = await client.GetAsync($"/api/vehicles?model={tag}&brand=JAECOO&year=2025");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.ReadVehiclePageAsync();
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public async Task GetAll_ModelFilterWithWildcardCharacters_IsTreatedAsText()
    {
        var (client, tag) = await SeedAsync();

        var page = await (await client.GetAsync($"/api/vehicles?model={tag}%25")).ReadVehiclePageAsync();

        Assert.Equal(0, page.TotalCount);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=100", "pageSize")]
    [InlineData("year=1900", "year")]
    [InlineData("minPrice=-5", "minPrice")]
    [InlineData("minPrice=200000&maxPrice=100000", "minPrice")]
    [InlineData("sortBy=color", "sortBy")]
    [InlineData("sortOrder=up", "sortOrder")]
    public async Task GetAll_InvalidParameter_Returns400WithFieldError(string query, string field)
    {
        // Arrange
        var client = await _factory.CreateClientForRoleAsync(Roles.Admin);

        // Act
        var response = await client.GetAsync($"/api/vehicles?{query}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains(field, problem.Errors.Keys);
    }

    [Theory]
    [InlineData("brand=Toyota")]
    [InlineData("status=Lost")]
    [InlineData("page=abc")]
    [InlineData("minPrice=cheap")]
    public async Task GetAll_ValueOfWrongType_Returns400(string query)
    {
        var client = await _factory.CreateClientForRoleAsync(Roles.Admin);

        var response = await client.GetAsync($"/api/vehicles?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customer_FiltersAndSorting_StillSeeOnlyAvailableVehicles()
    {
        // Arrange
        var (_, tag) = await SeedAsync();
        var customer = await _factory.CreateClientForRoleAsync(Roles.Customer);

        // Act
        var page = await (await customer.GetAsync($"/api/vehicles?model={tag}&brand=Chery&sortBy=price&sortOrder=desc"))
            .ReadVehiclePageAsync();

        // Assert
        Assert.Equal(3, page.TotalCount);
        Assert.All(page.Items, v => Assert.Equal(AvailabilityStatus.Available, v.AvailabilityStatus));
        Assert.Equal(145_000m, page.Items[0].Price);
    }
}
