using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Showroom.Api.Auth;
using Showroom.Api.Data;
using Showroom.Api.DTOs;

namespace Showroom.IntegrationTests.Infrastructure;

public class ShowroomApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Test@12345";
    public static readonly IReadOnlyDictionary<string, string> Emails = new Dictionary<string, string>
    {
        [Roles.Admin] = "admin@test.local",
        [Roles.SalesRep] = "sales@test.local",
        [Roles.Customer] = "customer@test.local"
    };

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Dictionary<string, string> _tokens = new();

    public ShowroomApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Database:Initialization"] = "EnsureCreated",
                ["Jwt:Issuer"] = "Showroom.Tests",
                ["Jwt:Audience"] = "Showroom.Tests.Clients",
                ["Jwt:Key"] = "integration-test-signing-key-at-least-32-chars",
                ["Jwt:ExpiresMinutes"] = "30"
            };
            var i = 0;
            foreach (var (role, email) in Emails)
            {
                settings[$"SeedUsers:{i}:FullName"] = $"Test {role}";
                settings[$"SeedUsers:{i}:Email"] = email;
                settings[$"SeedUsers:{i}:Password"] = Password;
                settings[$"SeedUsers:{i}:Role"] = role;
                i++;
            }
            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public HttpClient CreateAnonymousClient() => CreateClient();

    public async Task<HttpClient> CreateClientForRoleAsync(string role)
    {
        if (!_tokens.TryGetValue(role, out var token))
        {
            using var anonymous = CreateClient();
            var response = await anonymous.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Email = Emails[role], Password = Password });
            response.EnsureSuccessStatusCode();
            token = (await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!.Token;
            _tokens[role] = token;
        }

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
