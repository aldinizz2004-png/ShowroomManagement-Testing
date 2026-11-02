using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Showroom.Api.Auth;
using Showroom.Api.Common;
using Showroom.Api.DTOs;
using Showroom.Api.Services;

namespace Showroom.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _service;

    public VehiclesController(IVehicleService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VehicleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<VehicleResponse>>> GetAll([FromQuery] VehicleQuery query, CancellationToken ct) =>
        Ok(await _service.GetAllAsync(query, ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleResponse>> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        return ToActionResult(await _service.GetByIdAsync(id, ct));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ManageVehicles)]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VehicleResponse>> Create(CreateVehicleRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        if (!result.Succeeded) return ToActionResult(result);

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
        return ToActionResult(await _service.UpdateAsync(id, request, ct));
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
        return ToActionResult(await _service.UpdateStatusAsync(id, request, ct));
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
        return result.Succeeded ? NoContent() : ToErrorResult(result.ErrorType, result.Errors);
    }

    private ActionResult ToActionResult(ServiceResult<VehicleResponse> result) =>
        result.Succeeded ? Ok(result.Value) : ToErrorResult(result.ErrorType, result.Errors);

    private ActionResult ToErrorResult(ServiceErrorType type, IReadOnlyDictionary<string, string[]> errors)
    {
        if (type == ServiceErrorType.NotFound)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Vehicle not found",
                           detail: errors.Values.SelectMany(m => m).FirstOrDefault());

        foreach (var (field, messages) in errors)
            foreach (var message in messages)
                ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }

    private ActionResult InvalidId()
    {
        ModelState.AddModelError("id", "Id must be a positive integer.");
        return ValidationProblem(ModelState);
    }
}
