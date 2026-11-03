using System.Net;
using System.Net.Http.Json;
using Showroom.Api.Auth;
using Showroom.Api.DTOs;
using Showroom.IntegrationTests.Infrastructure;

namespace Showroom.IntegrationTests.Customers;

public class CustomerApiTests : IClassFixture<ShowroomApiFactory>
{
    private readonly ShowroomApiFactory _factory;

    public CustomerApiTests(ShowroomApiFactory factory)
    {
        _factory = factory;
    }

    private static CreateCustomerRequest NewCustomer(string? tag = null)
    {
        tag ??= Guid.NewGuid().ToString("N")[..8];
        return new CreateCustomerRequest
        {
            FullName = $"Customer {tag}",
            Email = $"customer-{tag}@test.local",
            Phone = "+970 599 123456",
            Address = "Nablus"
        };
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.SalesRep)]
    public async Task Post_ValidCustomer_AsStaff_Returns201WithLocation(string role)
    {
        // Arrange
        var client = await _factory.CreateClientForRoleAsync(role);

        // Act
        var response = await client.PostAsJsonAsync("/api/customers", NewCustomer());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadAsync<CustomerResponse>();
        Assert.Equal($"/api/customers/{body.Id}", response.Headers.Location!.AbsolutePath);
        Assert.False(body.HasAccount);
    }

    [Fact]
    public async Task Post_InvalidCustomer_Returns400WithFieldErrors()
    {
        var client = await _factory.CreateClientForRoleAsync(Roles.SalesRep);

        var response = await client.PostAsJsonAsync("/api/customers", new { fullName = "A", email = "bad", phone = "1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Contains("fullName", problem.Errors.Keys);
        Assert.Contains("email", problem.Errors.Keys);
        Assert.Contains("phone", problem.Errors.Keys);
    }

    [Fact]
    public async Task Post_DuplicateEmail_Returns400()
    {
        var client = await _factory.CreateClientForRoleAsync(Roles.Admin);
        var request = NewCustomer();
        await client.PostAsJsonAsync("/api/customers", request);

        var second = await client.PostAsJsonAsync("/api/customers", request);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("email", (await second.ReadProblemAsync()).Errors.Keys);
    }

    [Fact]
    public async Task GetById_ExistingAndMissingCustomer_Returns200And404()
    {
        var client = await _factory.CreateClientForRoleAsync(Roles.SalesRep);
        var created = await (await client.PostAsJsonAsync("/api/customers", NewCustomer())).ReadAsync<CustomerResponse>();

        var found = await client.GetAsync($"/api/customers/{created.Id}");
        var missing = await client.GetAsync("/api/customers/999999");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithSearch_ReturnsOnlyMatchingCustomers()
    {
        // Arrange
        var client = await _factory.CreateClientForRoleAsync(Roles.Admin);
        var tag = Guid.NewGuid().ToString("N")[..8];
        await client.PostAsJsonAsync("/api/customers", NewCustomer(tag));
        await client.PostAsJsonAsync("/api/customers", NewCustomer());

        // Act
        var list = await (await client.GetAsync($"/api/customers?search={tag}")).ReadAsync<List<CustomerResponse>>();

        // Assert
        Assert.Equal($"Customer {tag}", Assert.Single(list).FullName);
    }

    [Fact]
    public async Task Put_ExistingCustomer_Returns200AndSavesChanges()
    {
        // Arrange
        var client = await _factory.CreateClientForRoleAsync(Roles.SalesRep);
        var created = await (await client.PostAsJsonAsync("/api/customers", NewCustomer())).ReadAsync<CustomerResponse>();
        var update = new UpdateCustomerRequest
        {
            FullName = "Updated Name", Email = created.Email, Phone = "0598 111222", Address = "Jenin"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/customers/{created.Id}", update);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reloaded = await (await client.GetAsync($"/api/customers/{created.Id}")).ReadAsync<CustomerResponse>();
        Assert.Equal("Updated Name", reloaded.FullName);
        Assert.Equal("Jenin", reloaded.Address);
    }

    [Fact]
    public async Task Put_NonExistentCustomer_Returns404()
    {
        var client = await _factory.CreateClientForRoleAsync(Roles.Admin);
        var update = new UpdateCustomerRequest { FullName = "Nobody", Email = "nobody@test.local", Phone = "0599999999" };

        var response = await client.PutAsJsonAsync("/api/customers/999999", update);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/customers")]
    [InlineData("GET", "/api/customers/1")]
    [InlineData("POST", "/api/customers")]
    [InlineData("PUT", "/api/customers/1")]
    public async Task CustomerRole_CannotUseCustomerManagement_Returns403(string method, string url)
    {
        var client = await _factory.CreateClientForRoleAsync(Roles.Customer);
        var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = method == "GET" ? null : JsonContent.Create(NewCustomer())
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotListCustomers_Returns401()
    {
        var response = await _factory.CreateAnonymousClient().GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_CreatesLinkedCustomerProfileVisibleToStaff()
    {
        // Arrange
        var email = $"online-{Guid.NewGuid():N}@test.local";

        // Act
        var register = await _factory.CreateAnonymousClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { FullName = "Online Buyer", Email = email, Password = "Online@12345", Phone = "0599 777888" });
        var admin = await _factory.CreateClientForRoleAsync(Roles.Admin);
        var list = await (await admin.GetAsync($"/api/customers?search={email}")).ReadAsync<List<CustomerResponse>>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var profile = Assert.Single(list);
        Assert.True(profile.HasAccount);
        Assert.Equal("0599 777888", profile.Phone);
    }
}
