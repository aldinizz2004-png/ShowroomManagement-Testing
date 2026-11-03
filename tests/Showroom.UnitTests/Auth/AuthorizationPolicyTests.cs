using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Showroom.Api.Auth;

namespace Showroom.UnitTests.Auth;

public class AuthorizationPolicyTests
{
    private readonly IAuthorizationService _authorization;

    public AuthorizationPolicyTests()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddAuthorization(options => options.AddShowroomPolicies())
            .BuildServiceProvider();
        _authorization = services.GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal UserWithRole(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], authenticationType: "Test"));

    [Theory]
    [InlineData(Roles.Admin, true)]
    [InlineData(Roles.SalesRep, true)]
    [InlineData(Roles.Customer, false)]
    public async Task ManageVehicles_AllowsAdminAndSalesRepOnly(string role, bool expected)
    {
        // Act
        var result = await _authorization.AuthorizeAsync(UserWithRole(role), Policies.ManageVehicles);

        // Assert
        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData(Roles.Admin, true)]
    [InlineData(Roles.SalesRep, false)]
    [InlineData(Roles.Customer, false)]
    public async Task DeleteVehicles_AllowsAdminOnly(string role, bool expected)
    {
        var result = await _authorization.AuthorizeAsync(UserWithRole(role), Policies.DeleteVehicles);

        Assert.Equal(expected, result.Succeeded);
    }

    [Theory]
    [InlineData(Roles.Admin, true)]
    [InlineData(Roles.SalesRep, true)]
    [InlineData(Roles.Customer, false)]
    public async Task ManageCustomers_AllowsAdminAndSalesRepOnly(string role, bool expected)
    {
        var result = await _authorization.AuthorizeAsync(UserWithRole(role), Policies.ManageCustomers);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task FallbackPolicy_RejectsAnonymousUser()
    {
        // Arrange
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var options = new AuthorizationOptions().AddShowroomPolicies();

        // Act
        var result = await _authorization.AuthorizeAsync(anonymous, options.FallbackPolicy!);

        // Assert
        Assert.False(result.Succeeded);
    }
}
