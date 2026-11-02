namespace Showroom.Api.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "Showroom.Api";
    public string Audience { get; set; } = "Showroom.Clients";
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 60;
}
