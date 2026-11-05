using Microsoft.EntityFrameworkCore;
using Showroom.Api.Data;
using Showroom.Api.Models;
using Showroom.Api.Repositories;
using Xunit.Abstractions;

namespace Showroom.IntegrationTests.Vehicles;

public class VehicleQuerySqlTests : IDisposable
{
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer("Server=localhost;Database=SqlPreview;Trusted_Connection=True")
        .Options);

    private readonly VehicleRepository _repository;
    private readonly ITestOutputHelper _output;

    public VehicleQuerySqlTests(ITestOutputHelper output)
    {
        _output = output;
        _repository = new VehicleRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private static readonly VehicleSearch SpecificationExample = new()
    {
        Brand = Brand.Chery,
        Year = 2025,
        MinPrice = 80_000m,
        MaxPrice = 150_000m,
        SortBy = VehicleSortField.Price,
        Page = 1,
        PageSize = 10
    };

    [Fact]
    public void PageQuery_TranslatesFiltersSortingAndPagingToSql()
    {
        // Act
        var sql = _repository.PageQuery(SpecificationExample).ToQueryString();
        _output.WriteLine(sql);

        // Assert
        Assert.Contains("-- " + VehicleRepository.SearchTag, sql);
        Assert.Contains("WHERE [v].[Brand] = ", sql);
        Assert.Contains("[v].[ManufacturingYear] = ", sql);
        Assert.Contains("[v].[Price] >= ", sql);
        Assert.Contains("[v].[Price] <= ", sql);
        Assert.Contains("ORDER BY [v].[Price], [v].[Id]", sql);
        Assert.Contains("OFFSET ", sql);
        Assert.Contains("FETCH NEXT ", sql);
    }

    [Fact]
    public void FilterQuery_CountRunsInTheDatabase()
    {
        var sql = _repository.FilterQuery(SpecificationExample).ToQueryString();
        _output.WriteLine(sql);

        Assert.Contains("FROM [Vehicles] AS [v]", sql);
        Assert.Contains("WHERE", sql);
        Assert.DoesNotContain("OFFSET", sql);
    }

    [Fact]
    public void PageQuery_DescendingModelSort_UsesIdAsTieBreaker()
    {
        var sql = _repository.PageQuery(new VehicleSearch { SortBy = VehicleSortField.Model, Descending = true }).ToQueryString();

        Assert.Contains("ORDER BY [v].[Model] DESC, [v].[Id]", sql);
    }

    [Fact]
    public void PageQuery_TextFilters_UseLikeWithEscapeCharacter()
    {
        var sql = _repository.PageQuery(new VehicleSearch { Model = "Tiggo", Color = "White" }).ToQueryString();
        _output.WriteLine(sql);

        Assert.Contains("[v].[Model] LIKE ", sql);
        Assert.Contains("[v].[Color] LIKE ", sql);
        Assert.Contains("ESCAPE N'\\'", sql);
    }

    [Theory]
    [InlineData("IX_Vehicles_Brand_Status_Price", new[] { "Brand", "AvailabilityStatus", "Price" })]
    [InlineData("IX_Vehicles_Status_Price", new[] { "AvailabilityStatus", "Price" })]
    public void Model_HasCompositeIndexForFrequentQueries(string name, string[] columns)
    {
        var index = _db.Model.FindEntityType(typeof(Vehicle))!.GetIndexes()
            .Single(i => i.GetDatabaseName() == name);

        Assert.Equal(columns, index.Properties.Select(p => p.Name));
    }
}
