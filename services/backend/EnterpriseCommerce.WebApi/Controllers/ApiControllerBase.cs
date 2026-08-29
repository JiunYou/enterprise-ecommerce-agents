using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseCommerce.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected readonly ISender Sender;

    protected ApiControllerBase(ISender sender)
    {
        Sender = sender;
    }

    protected bool TryGetCustomerId(out Guid customerId)
    {
        var claim = HttpContext.User.FindFirst(CustomerClaimTypes.CustomerId);
        if (claim != null && Guid.TryParse(claim.Value, out customerId) && customerId != Guid.Empty)
        {
            return true;
        }

        customerId = Guid.Empty;
        return false;
    }

    protected IActionResult HandleFailure(Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot handle failure for a successful result.");
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Detail = result.Error.Message
        };

        if (result.Error.Code == "Domain.NotFound" || result.Error.Code.Contains("NotFound"))
        {
            problemDetails.Status = StatusCodes.Status404NotFound;
            problemDetails.Title = "Not Found";
            problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4";
        }
        else if (result.Error.Code.Contains("Conflict") || result.Error.Code.Contains("AlreadyExists"))
        {
            problemDetails.Status = StatusCodes.Status409Conflict;
            problemDetails.Title = "Conflict";
            problemDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8";
        }

        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }
}
