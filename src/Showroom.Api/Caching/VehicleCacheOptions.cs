namespace Showroom.Api.Caching;

public class VehicleCacheOptions
{
    public const string Section = "VehicleCache";

    public bool Enabled { get; set; } = true;
    public TimeSpan ListSlidingExpiration { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan ListAbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan DetailSlidingExpiration { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan DetailAbsoluteExpiration { get; set; } = TimeSpan.FromMinutes(10);
}
