using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeployFlow.API.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected readonly IMediator Mediator;

    protected BaseController(IMediator mediator) => Mediator = mediator;

    protected IActionResult ToResponse<T>(Result<T> result)
    {
        if (!result.IsSuccess)
        {
            var code = int.TryParse(result.ErrorCode, out var sc) ? sc : 500;
            return code switch
            {
                404 => NotFound(new { error = result.Error }),
                403 => Forbid(),
                400 => BadRequest(new { error = result.Error }),
                401 => Unauthorized(new { error = result.Error }),
                _ => StatusCode(code, new { error = result.Error })
            };
        }
        return Ok(result.Value);
    }

    protected IActionResult ToResponse(Result result)
    {
        if (!result.IsSuccess)
        {
            var code = int.TryParse(result.ErrorCode, out var sc) ? sc : 500;
            return code switch
            {
                404 => NotFound(new { error = result.Error }),
                403 => Forbid(),
                400 => BadRequest(new { error = result.Error }),
                401 => Unauthorized(new { error = result.Error }),
                _ => StatusCode(code, new { error = result.Error })
            };
        }
        return NoContent();
    }
}