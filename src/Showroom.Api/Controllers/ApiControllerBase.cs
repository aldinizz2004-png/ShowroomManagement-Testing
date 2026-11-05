using Microsoft.AspNetCore.Mvc;
using Showroom.Api.Common;

namespace Showroom.Api.Controllers;

[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult FromResult<T>(ServiceResult<T> result, string notFoundTitle) =>
        result.Succeeded ? Ok(result.Value) : ErrorResult(result, notFoundTitle);

    protected ActionResult ErrorResult<T>(ServiceResult<T> result, string notFoundTitle)
    {
        var message = result.Errors.Values.SelectMany(m => m).FirstOrDefault();

        switch (result.ErrorType)
        {
            case ServiceErrorType.NotFound:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: notFoundTitle, detail: message);
            case ServiceErrorType.Forbidden:
                return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: message);
        }

        ModelState.Clear();
        foreach (var (field, messages) in result.Errors)
            foreach (var m in messages)
                ModelState.AddModelError(field, m);
        return ValidationProblem(ModelState);
    }

    protected ActionResult InvalidId()
    {
        ModelState.AddModelError("id", "Id must be a positive integer.");
        return ValidationProblem(ModelState);
    }
}
