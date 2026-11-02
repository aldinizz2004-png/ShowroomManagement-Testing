namespace Showroom.Api.Models;

/// <summary>A user account (Admin, SalesRep or Customer).</summary>
public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;          // stored lower-case, unique
    public string PasswordHash { get; set; } = string.Empty;   // PBKDF2 hash, never the password
    public string Role { get; set; } = Auth.Roles.Customer;
    public DateTime CreatedAt { get; set; }
}
