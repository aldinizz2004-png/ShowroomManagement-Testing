using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Showroom.Api.Auth;
using Showroom.Api.DTOs;
using Showroom.Api.Services;

namespace Showroom.Api.Controllers;

[Route("api/reservations")]
[Authorize]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public class ReservationsController : ApiControllerBase
{
    private const string NotFoundTitle = "Reservation not found";
    private readonly IReservationService _service;

    public ReservationsController(IReservationService service)
    {
        _service = service;
    }

    private CurrentUser CurrentUser => CurrentUser.From(User);

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReservationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ReservationResponse>>> GetAll([FromQuery] ReservationQuery query, CancellationToken ct) =>
        Ok(await _service.GetAllAsync(query, CurrentUser, ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        return FromResult(await _service.GetByIdAsync(id, CurrentUser, ct), NotFoundTitle);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReservationResponse>> Create(CreateReservationRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, CurrentUser, ct);
        if (!result.Succeeded) return ErrorResult(result, NotFoundTitle);

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPatch("{id}/cancel")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> Cancel(int id, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        return FromResult(await _service.CancelAsync(id, CurrentUser, ct), NotFoundTitle);
    }

    [HttpPatch("{id}/complete")]
    [Authorize(Policy = Policies.ManageReservations)]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> Complete(int id, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        return FromResult(await _service.CompleteAsync(id, CurrentUser, ct), NotFoundTitle);
    }
}
