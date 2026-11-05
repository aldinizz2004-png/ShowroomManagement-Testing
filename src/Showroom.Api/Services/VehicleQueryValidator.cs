using Showroom.Api.DTOs;
using Showroom.Api.Repositories;

namespace Showroom.Api.Services;

public static class VehicleQueryValidator
{
    public const int MaxPageSize = 50;

    private static readonly Dictionary<string, VehicleSortField> SortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["price"] = VehicleSortField.Price,
        ["model"] = VehicleSortField.Model,
        ["year"] = VehicleSortField.ManufacturingYear,
        ["manufacturingYear"] = VehicleSortField.ManufacturingYear
    };

    public static Dictionary<string, string[]> Validate(VehicleQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        if (query.Page < 1)
            errors["page"] = ["Page must be 1 or greater."];

        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];

        if (query.Year is { } year && (year < VehicleValidator.MinYear || year > VehicleValidator.MaxYear))
            errors["year"] = [$"Year must be between {VehicleValidator.MinYear} and {VehicleValidator.MaxYear}."];

        if (query.MinPrice < 0)
            errors["minPrice"] = ["Minimum price cannot be negative."];

        if (query.MaxPrice < 0)
            errors["maxPrice"] = ["Maximum price cannot be negative."];

        if (query.MinPrice is { } min && query.MaxPrice is { } max && min >= 0 && max >= 0 && min > max)
            errors["minPrice"] = ["Minimum price cannot be greater than maximum price."];

        if (query.Model?.Trim().Length > VehicleValidator.ModelMaxLength)
            errors["model"] = [$"Model must be at most {VehicleValidator.ModelMaxLength} characters."];

        if (query.Color?.Trim().Length > VehicleValidator.ColorMaxLength)
            errors["color"] = [$"Color must be at most {VehicleValidator.ColorMaxLength} characters."];

        if (!string.IsNullOrWhiteSpace(query.SortBy) && !SortFields.ContainsKey(query.SortBy.Trim()))
            errors["sortBy"] = ["Sort by must be price, model or year."];

        if (!string.IsNullOrWhiteSpace(query.SortOrder) && ParseOrder(query.SortOrder) is null)
            errors["sortOrder"] = ["Sort order must be asc or desc."];

        return errors;
    }

    public static VehicleSearch ToSearch(VehicleQuery query) => new()
    {
        Brand = query.Brand,
        Model = Clean(query.Model),
        Year = query.Year,
        MinPrice = query.MinPrice,
        MaxPrice = query.MaxPrice,
        Color = Clean(query.Color),
        Status = query.Status,
        SortBy = string.IsNullOrWhiteSpace(query.SortBy) ? VehicleSortField.Id : SortFields[query.SortBy.Trim()],
        Descending = ParseOrder(query.SortOrder) ?? false,
        Page = query.Page,
        PageSize = query.PageSize
    };

    private static bool? ParseOrder(string? order) => order?.Trim().ToLowerInvariant() switch
    {
        null or "" or "asc" => false,
        "desc" => true,
        _ => null
    };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
