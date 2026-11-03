using Microsoft.AspNetCore.Identity;
using Showroom.Api.Auth;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;

namespace Showroom.Api.Services;

public interface IAuthService
{
    Task<ServiceResult<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly ICustomerRepository _customers;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ITokenService _tokens;

    public AuthService(IUserRepository users, ICustomerRepository customers, IPasswordHasher<User> hasher, ITokenService tokens)
    {
        _users = users;
        _customers = customers;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<ServiceResult<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email!);

        if (await _users.EmailExistsAsync(email, ct))
            return ServiceResult<UserResponse>.Invalid("email", "This email is already registered.");

        var user = new User
        {
            FullName = request.FullName!.Trim(),
            Email = email,
            Role = Roles.Customer,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password!);

        var existingProfile = await _customers.GetByEmailAsync(email, ct);
        if (existingProfile is not null && existingProfile.UserId is null)
        {
            existingProfile.User = user;
            await _customers.UpdateAsync(existingProfile, ct);
        }
        else
        {
            user.Customer = new Customer
            {
                FullName = user.FullName,
                Email = email,
                Phone = request.Phone!.Trim(),
                CreatedAt = user.CreatedAt
            };
            await _users.AddAsync(user, ct);
        }

        return ServiceResult<UserResponse>.Ok(ToResponse(user));
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(Normalize(request.Email!), ct);

        if (user is null) return null;

        var check = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!);
        if (check == PasswordVerificationResult.Failed) return null;

        if (check == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password!);
            await _users.UpdateAsync(user, ct);
        }

        var (token, expiresAt) = _tokens.CreateToken(user);
        return new AuthResponse { Token = token, ExpiresAt = expiresAt, User = ToResponse(user) };
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static UserResponse ToResponse(User u) => new()
    {
        Id = u.Id,
        FullName = u.FullName,
        Email = u.Email,
        Role = u.Role
    };
}
