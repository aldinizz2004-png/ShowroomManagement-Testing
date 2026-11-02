using Showroom.Api.Models;

namespace Showroom.Api.Services;

public static class VehicleValidator
{
    public const int MinYear = 1990;
    public const int ModelMaxLength = 50;
    public const int ColorMaxLength = 30;
    public const decimal MaxPrice = 10_000_000m;
    public const int MaxMileage = 2_000_000;

    public static int MaxYear => DateTime.UtcNow.Year + 1;

    public static Dictionary<string, string[]> Validate(
        Brand? brand, string? model, int? year, decimal? price, string? color, int? mileage)
    {
        var errors = new Dictionary<string, string[]>();

        if (brand is null)
            errors["brand"] = ["Brand is required (Chery, OMODA or JAECOO)."];
        else if (!Enum.IsDefined(brand.Value))
            errors["brand"] = ["Brand must be Chery, OMODA or JAECOO."];

        if (string.IsNullOrWhiteSpace(model))
            errors["model"] = ["Model is required."];
        else if (model.Trim().Length > ModelMaxLength)
            errors["model"] = [$"Model must be at most {ModelMaxLength} characters."];

        if (year is null)
            errors["manufacturingYear"] = ["Manufacturing year is required."];
        else if (year < MinYear || year > MaxYear)
            errors["manufacturingYear"] = [$"Manufacturing year must be between {MinYear} and {MaxYear}."];

        if (price is null)
            errors["price"] = ["Price is required."];
        else if (price <= 0)
            errors["price"] = ["Price must be greater than 0."];
        else if (price > MaxPrice)
            errors["price"] = [$"Price must not exceed {MaxPrice:N0} NIS."];

        if (string.IsNullOrWhiteSpace(color))
            errors["color"] = ["Color is required."];
        else if (color.Trim().Length > ColorMaxLength)
            errors["color"] = [$"Color must be at most {ColorMaxLength} characters."];

        if (mileage is null)
            errors["mileage"] = ["Mileage is required."];
        else if (mileage < 0 || mileage > MaxMileage)
            errors["mileage"] = [$"Mileage must be between 0 and {MaxMileage:N0} km."];

        return errors;
    }
}
