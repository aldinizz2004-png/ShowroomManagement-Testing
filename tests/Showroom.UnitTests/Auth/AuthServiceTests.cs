using Microsoft.AspNetCore.Identity;
using Moq;
using Showroom.Api.Auth;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;
using Showroom.Api.Services;

namespace Showroom.UnitTests.Auth;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly PasswordHasher<User> _hasher = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _tokens.Setup(t => t.CreateToken(It.IsAny<User>()))
               .Returns(("fake.jwt.token", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        _service = new AuthService(_users.Object, _customers.Object, _hasher, _tokens.Object);
    }

    private User StoredUser(string email, string password, string role = Roles.Customer)
    {
        var user = new User { Id = 10, FullName = "Test User", Email = email, Role = role };
        user.PasswordHash = _hasher.HashPassword(user, password);
        return user;
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_ReturnsTokenAndUser()
    {
        // Arrange
        _users.Setup(u => u.GetByEmailAsync("sales@test.com", It.IsAny<CancellationToken>()))
              .ReturnsAsync(StoredUser("sales@test.com", "Sales@12345", Roles.SalesRep));

        // Act
        var result = await _service.LoginAsync(new LoginRequest { Email = "sales@test.com", Password = "Sales@12345" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("fake.jwt.token", result!.Token);
        Assert.Equal(Roles.SalesRep, result.User.Role);
        _tokens.Verify(t => t.CreateToken(It.Is<User>(u => u.Email == "sales@test.com")), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_EmailWithCapitalsAndSpaces_IsNormalized()
    {
        // Arrange
        _users.Setup(u => u.GetByEmailAsync("admin@test.com", It.IsAny<CancellationToken>()))
              .ReturnsAsync(StoredUser("admin@test.com", "Admin@12345", Roles.Admin));

        // Act
        var result = await _service.LoginAsync(new LoginRequest { Email = "  ADMIN@Test.com ", Password = "Admin@12345" });

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNullAndIssuesNoToken()
    {
        // Arrange
        _users.Setup(u => u.GetByEmailAsync("a@test.com", It.IsAny<CancellationToken>()))
              .ReturnsAsync(StoredUser("a@test.com", "Correct@123"));

        // Act
        var result = await _service.LoginAsync(new LoginRequest { Email = "a@test.com", Password = "Wrong@123" });

        // Assert
        Assert.Null(result);
        _tokens.Verify(t => t.CreateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsNull()
    {
        // Arrange
        _users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);

        // Act
        var result = await _service.LoginAsync(new LoginRequest { Email = "nobody@test.com", Password = "x" });

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesCustomerWithHashedPassword()
    {
        // Arrange
        User? saved = null;
        _users.Setup(u => u.EmailExistsAsync("new@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => saved = u)
              .Returns(Task.CompletedTask);

        // Act
        var result = await _service.RegisterAsync(new RegisterRequest
        {
            FullName = " Ahmad Khalil ", Email = "New@Test.com", Password = "Strong@123", Phone = "+970 599 123456"
        });

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(Roles.Customer, result.Value!.Role);
        Assert.Equal("new@test.com", saved!.Email);
        Assert.Equal("Ahmad Khalil", saved.FullName);
        Assert.NotEqual("Strong@123", saved.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
                     _hasher.VerifyHashedPassword(saved, saved.PasswordHash, "Strong@123"));
        Assert.NotNull(saved.Customer);
        Assert.Equal("new@test.com", saved.Customer!.Email);
        Assert.Equal("+970 599 123456", saved.Customer.Phone);
    }

    [Fact]
    public async Task RegisterAsync_EmailOfWalkInCustomer_LinksExistingProfileInsteadOfCreatingNew()
    {
        // Arrange
        var walkIn = new Customer { Id = 5, FullName = "Walk In", Email = "walkin@test.com", Phone = "0599000000" };
        _users.Setup(u => u.EmailExistsAsync("walkin@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _customers.Setup(c => c.GetByEmailAsync("walkin@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(walkIn);

        // Act
        var result = await _service.RegisterAsync(new RegisterRequest
        {
            FullName = "Walk In", Email = "walkin@test.com", Password = "Strong@123", Phone = "0599000000"
        });

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(walkIn.User);
        Assert.Equal(Roles.Customer, walkIn.User!.Role);
        _customers.Verify(c => c.UpdateAsync(walkIn, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsValidationErrorAndSavesNothing()
    {
        // Arrange
        _users.Setup(u => u.EmailExistsAsync("taken@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _service.RegisterAsync(new RegisterRequest
        {
            FullName = "Someone", Email = "taken@test.com", Password = "Strong@123", Phone = "0599111111"
        });

        // Assert
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains("email", result.Errors.Keys);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
