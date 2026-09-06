using Asp.Versioning;
using EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderById;
using EnterpriseCommerce.Application.Orders.Queries.GetAdminOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseCommerce.WebApi.Controllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/orders")]
[Authorize(Roles = "Admin")]
public sealed class AdminOrdersController : ApiControllerBase
{
    public AdminOrdersController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminOrderPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        [FromQuery] Guid? orderId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminOrdersQuery(page, pageSize, status, orderId);
        var result = await Sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminOrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken cancellationToken = default)
    {
        var query = new GetAdminOrderByIdQuery(id);
        var result = await Sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelOrder(
        Guid id,
        [FromBody] EnterpriseCommerce.WebApi.Contracts.Orders.AdminCancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = "Request body is required."
            });
        }

        if (!TryGetAdminActor(out var issuer, out var subject))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Missing trusted actor identity."
            });
        }

        var command = new EnterpriseCommerce.Application.Orders.Commands.AdminCancelOrder.AdminCancelOrderCommand(
            id,
            issuer,
            subject,
            request.Reason);

        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }

    private bool TryGetAdminActor(out string issuer, out string subject)
    {
        issuer = string.Empty;
        subject = string.Empty;

        var subjectClaim = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? HttpContext.User.FindFirst("sub");

        if (subjectClaim is null || string.IsNullOrWhiteSpace(subjectClaim.Value))
        {
            return false;
        }

        var issuerClaim = HttpContext.User.FindFirst("iss");
        var resolvedIssuer = !string.IsNullOrWhiteSpace(issuerClaim?.Value)
            ? issuerClaim.Value
            : (!string.IsNullOrWhiteSpace(subjectClaim.Issuer) && subjectClaim.Issuer != "LOCAL AUTHORITY" ? subjectClaim.Issuer : null);

        if (string.IsNullOrWhiteSpace(resolvedIssuer))
        {
            return false;
        }

        issuer = resolvedIssuer;
        subject = subjectClaim.Value;
        return true;
    }
}
