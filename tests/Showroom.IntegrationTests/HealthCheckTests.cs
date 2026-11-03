using System.Net;
using Showroom.IntegrationTests.Infrastructure;

namespace Showroom.IntegrationTests;

public class HealthCheckTests : IClassFixture<ShowroomApiFactory>
{
    private readonly ShowroomApiFactory _factory;

    public HealthCheckTests(ShowroomApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_WithoutToken_Returns200Healthy()
    {
        // Act
        var response = await _factory.CreateAnonymousClient().GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
