using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Showroom.Api.Auth;
using Showroom.Api.DTOs;
using Showroom.Api.Services;

namespace Showroom.Api.Controllers;

[Route("api/customers")]
[Authorize(Policy = Policies.ManageCustomers)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class CustomersController : ApiControllerBase
{
    private const string NotFoundTitle = "Customer not found";
    private readonly ICustomerService _service;

    public CustomersController(ICustomerService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> GetAll([FromQuery] string? search, CancellationToken ct) =>
        Ok(await _service.GetAllAsync(search, ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(int id, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        return FromResult(await _service.GetByIdAsync(id, ct), NotFoundTitle);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        if (!result.Succeeded) return ErrorResult(result, NotFoundTitle);

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Update(int id, UpdateCustomerRequest request, CancellationToken ct)
    {
        if (id <= 0) return InvalidId();
        return FromResult(await _service.UpdateAsync(id, request, ct), NotFoundTitle);
    }
}
