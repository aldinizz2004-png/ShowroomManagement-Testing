using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Showroom.Api.DTOs;
using Showroom.Api.Models;

namespace Showroom.IntegrationTests.Infrastructure;

/// <summary>JSON settings matching the API (camelCase + enums as strings) and small request helpers.</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static CreateVehicleRequest NewVehicle(string? model = null, Brand brand = Brand.Chery, decimal price = 145_000m) => new()
    {
        Brand = brand,
        Model = model ?? $"Tiggo {Guid.NewGuid():N}"[..20],   // unique, so tests sharing a database do not collide
        ManufacturingYear = 2025,
        Price = price,
        Color = "White",
        Mileage = 0
    };

    public static Task<HttpResponseMessage> PostVehicleAsync(this HttpClient client, CreateVehicleRequest request) =>
        client.PostAsJsonAsync("/api/vehicles", request, Options);

    public static async Task<VehicleResponse> CreateVehicleAsync(this HttpClient client, CreateVehicleRequest? request = null)
    {
        var response = await client.PostVehicleAsync(request ?? NewVehicle());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VehicleResponse>(Options))!;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Options))!;

    public static Task<ValidationProblemDetails> ReadProblemAsync(this HttpResponseMessage response) =>
        response.ReadAsync<ValidationProblemDetails>();
}
