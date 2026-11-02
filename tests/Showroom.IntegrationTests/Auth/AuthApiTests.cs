using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Showroom.Api.Auth;
using Showroom.Api.DTOs;
using Showroom.IntegrationTests.Infrastructure;

namespace Showroom.IntegrationTests.Auth;

/// <summary>Authentication through HTTP: register, login, and using the returned token.</summary>
public class AuthApiTests : IClassFixture<ShowroomApiFactory>
{
    private readonly ShowroomApiFactory _factory;
    private readonly HttpClient _client;

    public AuthApiTests(ShowroomApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAnonymousClient();
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.SalesRep)]
    [InlineData(Roles.Customer)]
    public async Task Login_SeededUser_Returns200WithTokenContainingRole(string role)
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = ShowroomApiFactory.Emails[role], Password = ShowroomApiFactory.Password });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<AuthResponse>();
        Assert.Equal(role, body.User.Role);
        Assert.Equal(role, new JsonWebTokenHandler().ReadJsonWebToken(body.Token).GetClaim("role").Value);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = ShowroomApiFactory.Emails[Roles.Admin], Password = "Wrong@12345" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_MissingFields_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ThenLogin_CreatesCustomerWhoCanReadButNotAdd()
    {
        // Arrange
        var email = $"buyer-{Guid.NewGuid():N}@test.local";

        // Act: register, then log in with the new account
        var register = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { FullName = "New Buyer", Email = email, Password = "Buyer@12345" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "Buyer@12345" });
        var token = (await login.ReadAsync<AuthResponse>()).Token;

        var client = _factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.Equal(Roles.Customer, (await register.ReadAsync<UserResponse>()).Role);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/vehicles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostVehicleAsync(TestJson.NewVehicle())).StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { FullName = "Copy", Email = ShowroomApiFactory.Emails[Roles.Customer], Password = "Copy@12345" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("email", (await response.ReadProblemAsync()).Errors.Keys);
    }

    [Fact]
    public async Task Register_InvalidData_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { FullName = "A", Email = "not-an-email", Password = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
