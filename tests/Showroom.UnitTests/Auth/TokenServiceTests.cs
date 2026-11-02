using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Showroom.Api.Auth;
using Showroom.Api.Models;

namespace Showroom.UnitTests.Auth;

public class TokenServiceTests
{
    private readonly TokenService _service = new(Options.Create(new JwtOptions
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        Key = "unit-test-signing-key-at-least-32-characters-long",
        ExpiresMinutes = 30
    }));

    private static readonly User Admin = new() { Id = 42, FullName = "Admin User", Email = "admin@test.com", Role = Roles.Admin };

    [Fact]
    public void CreateToken_ContainsUserIdEmailAndRole()
    {
        // Act
        var (token, _) = _service.CreateToken(Admin);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        // Assert
        Assert.Equal("42", jwt.Subject);
        Assert.Equal("admin@test.com", jwt.GetClaim("email").Value);
        Assert.Equal(Roles.Admin, jwt.GetClaim("role").Value);
        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
    }

    [Fact]
    public void CreateToken_ExpiresAfterConfiguredMinutes()
    {
        // Act
        var (token, expiresAt) = _service.CreateToken(Admin);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        // Assert
        Assert.InRange(expiresAt, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
        Assert.InRange(jwt.ValidTo, expiresAt.AddSeconds(-2), expiresAt.AddSeconds(2));
    }

    [Fact]
    public void CreateToken_IsSignedWithHmacSha256()
    {
        var (token, _) = _service.CreateToken(Admin);

        Assert.Equal("HS256", new JsonWebTokenHandler().ReadJsonWebToken(token).Alg);
    }
}
