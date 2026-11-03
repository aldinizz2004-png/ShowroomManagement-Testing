using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Showroom.Api.Auth;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Models;
using Showroom.Api.Services;

namespace Showroom.Api.Controllers;

[Route("api/vehicles")]
[Authorize]
public class VehiclesController : ApiControllerBase
{
    private const string NotFoundTitle = "Vehicle not found";
    private readonly IVehicleService _service;

    public VehiclesController(IVehicleService service)
    {
        _service = service;
    }

    private bool IsCustomer => User.IsInRole(Roles.Customer);

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VehicleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<VehicleResponse>>> GetAll([FromQuery] VehicleQuery query, CancellationToken ct)
    {
        if (IsCustomer)
            query.Status = AvailabilityStatus.Available;

        return Ok(await _service.GetAllAsync(query, ct));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleResponse>> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();

        var result = await _service.GetByIdAsync(id, ct);
        if (result.Succeeded && IsCustomer && result.Value!.AvailabilityStatus != AvailabilityStatus.Available)
            result = ServiceResult<VehicleResponse>.NotFound($"No available vehicle exists with id {id}.");

        return FromResult(result, NotFoundTitle);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageVehicles)]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VehicleResponse>> Create(CreateVehicleRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        if (!result.Succeeded) return ErrorResult(result, NotFoundTitle);

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.ManageVehicles)]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleResponse>> Update(int id, UpdateVehicleRequest request, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        return FromResult(await _service.UpdateAsync(id, request, ct), NotFoundTitle);
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = Policies.ManageVehicles)]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleResponse>> UpdateStatus(int id, UpdateVehicleStatusRequest request, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        return FromResult(await _service.UpdateStatusAsync(id, request, ct), NotFoundTitle);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.DeleteVehicles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();

        var result = await _service.DeleteAsync(id, ct);
        return result.Succeeded ? NoContent() : ErrorResult(result, NotFoundTitle);
    }
}
