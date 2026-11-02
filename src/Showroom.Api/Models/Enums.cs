namespace Showroom.Api.Models;

/// <summary>The three brands sold by the showroom.</summary>
public enum Brand
{
    Chery,
    OMODA,
    JAECOO
}

/// <summary>Where the vehicle is in the sales process.</summary>
public enum AvailabilityStatus
{
    Available,
    Reserved,
    Sold
}
