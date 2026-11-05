using Showroom.Api.Auth;
using Showroom.Api.Caching;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Repositories;

namespace Showroom.Api.Services;

public interface IReservationService
{
    Task<IReadOnlyList<ReservationResponse>> GetAllAsync(ReservationQuery query, CurrentUser user, CancellationToken ct = default);
    Task<ServiceResult<ReservationResponse>> GetByIdAsync(int id, CurrentUser user, CancellationToken ct = default);
    Task<ServiceResult<ReservationResponse>> CreateAsync(CreateReservationRequest request, CurrentUser user, CancellationToken ct = default);
    Task<ServiceResult<ReservationResponse>> CancelAsync(int id, CurrentUser user, CancellationToken ct = default);
    Task<ServiceResult<ReservationResponse>> CompleteAsync(int id, CurrentUser user, CancellationToken ct = default);
}

public class ReservationService : IReservationService
{
    public const int MaxDaysUntilPurchase = 90;

    private readonly IReservationRepository _reservations;
    private readonly IVehicleRepository _vehicles;
    private readonly ICustomerRepository _customers;
    private readonly TimeProvider _clock;
    private readonly IVehicleCache _vehicleCache;

    public ReservationService(
        IReservationRepository reservations,
        IVehicleRepository vehicles,
        ICustomerRepository customers,
        TimeProvider clock,
        IVehicleCache vehicleCache)
    {
        _reservations = reservations;
        _vehicles = vehicles;
        _customers = customers;
        _clock = clock;
        _vehicleCache = vehicleCache;
    }

    public async Task<IReadOnlyList<ReservationResponse>> GetAllAsync(ReservationQuery query, CurrentUser user, CancellationToken ct = default)
    {
        int? customerId = query.CustomerId;

        if (!user.IsStaff)
        {
            var own = await _customers.GetByUserIdAsync(user.UserId, ct);
            if (own is null)
                return [];
            customerId = own.Id;
        }

        var reservations = await _reservations.GetAllAsync(customerId, query.Status, ct);
        return reservations.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<ReservationResponse>> GetByIdAsync(int id, CurrentUser user, CancellationToken ct = default)
    {
        var reservation = await _reservations.GetByIdAsync(id, ct);
        if (reservation is null)
            return NotFound(id);

        if (!CanAccess(reservation, user))
            return Forbidden();

        return ServiceResult<ReservationResponse>.Ok(ToResponse(reservation));
    }

    public async Task<ServiceResult<ReservationResponse>> CreateAsync(CreateReservationRequest request, CurrentUser user, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        var errors = new Dictionary<string, string[]>();
        if (request.VehicleId is null or <= 0)
            errors["vehicleId"] = ["Vehicle id is required."];
        if (request.ExpectedPurchaseDate is null)
            errors["expectedPurchaseDate"] = ["Expected purchase date is required."];
        else if (request.ExpectedPurchaseDate < today)
            errors["expectedPurchaseDate"] = ["Expected purchase date cannot be in the past."];
        else if (request.ExpectedPurchaseDate > today.AddDays(MaxDaysUntilPurchase))
            errors["expectedPurchaseDate"] = [$"Expected purchase date must be within {MaxDaysUntilPurchase} days."];
        if (user.IsStaff && request.CustomerId is null or <= 0)
            errors["customerId"] = ["Customer id is required."];
        if (errors.Count > 0)
            return ServiceResult<ReservationResponse>.Invalid(errors);

        Customer? customer;
        if (user.IsStaff)
        {
            customer = await _customers.GetByIdAsync(request.CustomerId!.Value, ct);
            if (customer is null)
                return ServiceResult<ReservationResponse>.Invalid("customerId", $"No customer exists with id {request.CustomerId}.");
        }
        else
        {
            customer = await _customers.GetByUserIdAsync(user.UserId, ct);
            if (customer is null)
                return ServiceResult<ReservationResponse>.Invalid("customerId", "Your account has no customer profile.");
            if (request.CustomerId is not null && request.CustomerId != customer.Id)
                return ServiceResult<ReservationResponse>.Forbidden("Customers can only create reservations for themselves.");
        }

        var vehicle = await _vehicles.GetByIdAsync(request.VehicleId!.Value, ct);
        if (vehicle is null)
            return ServiceResult<ReservationResponse>.Invalid("vehicleId", $"No vehicle exists with id {request.VehicleId}.");
        if (vehicle.AvailabilityStatus != AvailabilityStatus.Available)
            return ServiceResult<ReservationResponse>.Invalid("vehicleId", $"Vehicle is not available (current status: {vehicle.AvailabilityStatus}).");

        vehicle.AvailabilityStatus = AvailabilityStatus.Reserved;
        var reservation = new Reservation
        {
            Customer = customer,
            CustomerId = customer.Id,
            Vehicle = vehicle,
            VehicleId = vehicle.Id,
            ReservationDate = _clock.GetUtcNow().UtcDateTime,
            ExpectedPurchaseDate = request.ExpectedPurchaseDate!.Value,
            Status = ReservationStatus.Active
        };

        await _reservations.AddAsync(reservation, ct);
        _vehicleCache.InvalidateVehicle(vehicle.Id);
        return ServiceResult<ReservationResponse>.Ok(ToResponse(reservation));
    }

    public async Task<ServiceResult<ReservationResponse>> CancelAsync(int id, CurrentUser user, CancellationToken ct = default)
    {
        var reservation = await _reservations.GetByIdAsync(id, ct);
        if (reservation is null)
            return NotFound(id);

        if (!CanAccess(reservation, user))
            return Forbidden();

        if (reservation.Status != ReservationStatus.Active)
            return ServiceResult<ReservationResponse>.Invalid("status", $"Only active reservations can be cancelled (current status: {reservation.Status}).");

        reservation.Status = ReservationStatus.Cancelled;
        if (reservation.Vehicle.AvailabilityStatus == AvailabilityStatus.Reserved)
            reservation.Vehicle.AvailabilityStatus = AvailabilityStatus.Available;

        await _reservations.UpdateAsync(reservation, ct);
        _vehicleCache.InvalidateVehicle(reservation.VehicleId);
        return ServiceResult<ReservationResponse>.Ok(ToResponse(reservation));
    }

    public async Task<ServiceResult<ReservationResponse>> CompleteAsync(int id, CurrentUser user, CancellationToken ct = default)
    {
        if (!user.IsStaff)
            return ServiceResult<ReservationResponse>.Forbidden("Only staff can complete a sale.");

        var reservation = await _reservations.GetByIdAsync(id, ct);
        if (reservation is null)
            return NotFound(id);

        if (reservation.Status != ReservationStatus.Active)
            return ServiceResult<ReservationResponse>.Invalid("status", $"Only active reservations can be completed (current status: {reservation.Status}).");

        reservation.Status = ReservationStatus.Completed;
        reservation.Vehicle.AvailabilityStatus = AvailabilityStatus.Sold;

        await _reservations.UpdateAsync(reservation, ct);
        _vehicleCache.InvalidateVehicle(reservation.VehicleId);
        return ServiceResult<ReservationResponse>.Ok(ToResponse(reservation));
    }

    private static bool CanAccess(Reservation reservation, CurrentUser user) =>
        user.IsStaff || (reservation.Customer.UserId is { } ownerId && ownerId == user.UserId);

    private static ServiceResult<ReservationResponse> NotFound(int id) =>
        ServiceResult<ReservationResponse>.NotFound($"No reservation exists with id {id}.");

    private static ServiceResult<ReservationResponse> Forbidden() =>
        ServiceResult<ReservationResponse>.Forbidden("You can only access your own reservations.");

    public static ReservationResponse ToResponse(Reservation r) => new()
    {
        Id = r.Id,
        CustomerId = r.CustomerId,
        CustomerName = r.Customer.FullName,
        VehicleId = r.VehicleId,
        Vehicle = $"{r.Vehicle.Brand} {r.Vehicle.Model} {r.Vehicle.ManufacturingYear}",
        ReservationDate = r.ReservationDate,
        ExpectedPurchaseDate = r.ExpectedPurchaseDate,
        Status = r.Status
    };
}
