using Asp.Versioning;
using EnterpriseCommerce.Application.Orders.Commands.AddItemToCart;
using EnterpriseCommerce.Application.Orders.Commands.RemoveCartItem;
using EnterpriseCommerce.Application.Orders.Commands.UpdateCartItemQuantity;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.WebApi.Contracts.Cart;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseCommerce.WebApi.Controllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cart")]
[Authorize]
public class CartController : ApiControllerBase
{
    public CartController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var query = new GetCartQuery(customerId);
        var result = await Sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("items")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var command = new AddItemToCartCommand(customerId, request.ProductId, request.Quantity);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPut("items/{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateItemQuantity(
        Guid productId,
        [FromBody] UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var command = new UpdateCartItemQuantityCommand(customerId, productId, request.Quantity);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }

    [HttpDelete("items/{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveItem(
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var command = new RemoveCartItemCommand(customerId, productId);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }
}
