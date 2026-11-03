using Moq;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;
using Showroom.Api.Services;

namespace Showroom.UnitTests.Customers;

public class CustomerServiceTests
{
    private readonly Mock<ICustomerRepository> _repository = new();
    private readonly CustomerService _service;

    public CustomerServiceTests()
    {
        _service = new CustomerService(_repository.Object);
    }

    private static CreateCustomerRequest ValidCreateRequest() => new()
    {
        FullName = "Ahmad Khalil",
        Email = "ahmad@test.com",
        Phone = "+970 599 123456",
        Address = "Nablus"
    };

    private static UpdateCustomerRequest ValidUpdateRequest() => new()
    {
        FullName = "Ahmad K.",
        Email = "ahmad.k@test.com",
        Phone = "0599 654321",
        Address = "Ramallah"
    };

    private static Customer ExistingCustomer(int id = 1) => new()
    {
        Id = id,
        FullName = "Sara Odeh",
        Email = "sara@test.com",
        Phone = "0598000000",
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task CreateAsync_ValidRequest_SavesCustomerWithNormalizedEmail()
    {
        // Arrange
        var request = ValidCreateRequest();
        request.Email = "  Ahmad@Test.COM ";
        Customer? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
                   .Callback<Customer, CancellationToken>((c, _) => saved = c)
                   .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("ahmad@test.com", saved!.Email);
        Assert.Equal("Ahmad Khalil", saved.FullName);
        Assert.False(result.Value!.HasAccount);
        _repository.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_EmptyAddress_IsStoredAsNull()
    {
        var request = ValidCreateRequest();
        request.Address = "   ";

        var result = await _service.CreateAsync(request);

        Assert.Null(result.Value!.Address);
    }

    [Theory]
    [InlineData("fullName")]
    [InlineData("email")]
    [InlineData("phone")]
    public async Task CreateAsync_MissingMandatoryField_ReturnsErrorForThatField(string field)
    {
        // Arrange
        var request = ValidCreateRequest();
        switch (field)
        {
            case "fullName": request.FullName = null; break;
            case "email": request.Email = " "; break;
            case "phone": request.Phone = null; break;
        }

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Equal(field, Assert.Single(result.Errors.Keys));
        _repository.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("two@@test.com")]
    public async Task CreateAsync_InvalidEmail_ReturnsValidationError(string email)
    {
        var request = ValidCreateRequest();
        request.Email = email;

        var result = await _service.CreateAsync(request);

        Assert.Contains("email", result.Errors.Keys);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("059-ABC-1234")]
    [InlineData("+9705991234567890123456")]
    public async Task CreateAsync_InvalidPhone_ReturnsValidationError(string phone)
    {
        var request = ValidCreateRequest();
        request.Phone = phone;

        var result = await _service.CreateAsync(request);

        Assert.Contains("phone", result.Errors.Keys);
    }

    [Fact]
    public async Task CreateAsync_TooLongNameAndAddress_ReturnsBothErrors()
    {
        var request = ValidCreateRequest();
        request.FullName = new string('n', 101);
        request.Address = new string('a', 201);

        var result = await _service.CreateAsync(request);

        Assert.Contains("fullName", result.Errors.Keys);
        Assert.Contains("address", result.Errors.Keys);
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmail_ReturnsValidationErrorAndDoesNotSave()
    {
        // Arrange
        _repository.Setup(r => r.EmailExistsAsync("ahmad@test.com", null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        // Act
        var result = await _service.CreateAsync(ValidCreateRequest());

        // Assert
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains("email", result.Errors.Keys);
        _repository.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsCustomer()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingCustomer(3));

        // Act
        var result = await _service.GetByIdAsync(3);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Sara Odeh", result.Value!.FullName);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Customer?)null);

        var result = await _service.GetByIdAsync(99);

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task GetAllAsync_PassesSearchToRepositoryAndMapsAccountFlag()
    {
        // Arrange
        var withAccount = ExistingCustomer(1);
        withAccount.UserId = 7;
        _repository.Setup(r => r.GetAllAsync("sara", It.IsAny<CancellationToken>()))
                   .ReturnsAsync([withAccount, ExistingCustomer(2)]);

        // Act
        var result = await _service.GetAllAsync("sara");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result[0].HasAccount);
        Assert.False(result[1].HasAccount);
    }

    [Fact]
    public async Task UpdateAsync_ExistingCustomer_UpdatesFields()
    {
        // Arrange
        var existing = ExistingCustomer(4);
        _repository.Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        // Act
        var result = await _service.UpdateAsync(4, ValidUpdateRequest());

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Ahmad K.", existing.FullName);
        Assert.Equal("ahmad.k@test.com", existing.Email);
        Assert.Equal("Ramallah", existing.Address);
        _repository.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_KeepingOwnEmail_IsAllowed()
    {
        // Arrange
        var existing = ExistingCustomer(4);
        _repository.Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repository.Setup(r => r.EmailExistsAsync("sara@test.com", 4, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var request = ValidUpdateRequest();
        request.Email = "sara@test.com";

        // Act
        var result = await _service.UpdateAsync(4, request);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UpdateAsync_EmailOfAnotherCustomer_ReturnsValidationError()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(ExistingCustomer(4));
        _repository.Setup(r => r.EmailExistsAsync("ahmad.k@test.com", 4, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _service.UpdateAsync(4, ValidUpdateRequest());

        // Assert
        Assert.Contains("email", result.Errors.Keys);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((Customer?)null);

        var result = await _service.UpdateAsync(404, ValidUpdateRequest());

        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_InvalidData_DoesNotTouchTheDatabase()
    {
        var request = ValidUpdateRequest();
        request.Phone = "abc";

        var result = await _service.UpdateAsync(4, request);

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
