namespace Showroom.Api.Auth;

/// <summary>Bound from the "Jwt" configuration section (key comes from user secrets / environment, not source code).</summary>
public class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "Showroom.Api";
    public string Audience { get; set; } = "Showroom.Clients";
    public string Key { get; set; } = string.Empty;   // at least 32 characters for HMAC-SHA256
    public int ExpiresMinutes { get; set; } = 60;
}
