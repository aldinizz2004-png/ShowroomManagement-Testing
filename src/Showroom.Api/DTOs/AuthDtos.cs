using System.ComponentModel.DataAnnotations;

namespace Showroom.Api.DTOs;

public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string? FullName { get; set; }

    [Required, EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [Required, StringLength(100, MinimumLength = 8)]
    public string? Password { get; set; }

    [Required, RegularExpression(@"^\+?[0-9 ]{7,20}$", ErrorMessage = "Phone must contain 7 to 20 digits, optionally starting with +.")]
    public string? Phone { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string? Email { get; set; }

    [Required]
    public string? Password { get; set; }
}

public class UserResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAt { get; set; }
    public UserResponse User { get; set; } = new();
}
