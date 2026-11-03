namespace Showroom.Api.Models;

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; }
}
