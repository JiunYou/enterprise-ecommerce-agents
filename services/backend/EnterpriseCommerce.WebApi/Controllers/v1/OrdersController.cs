using Asp.Versioning;
using EnterpriseCommerce.Application.Orders.Commands.CreateOrder;
using EnterpriseCommerce.Application.Orders.Commands.SubmitOrder;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Orders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseCommerce.WebApi.Controllers.v1;

[ApiVersion("1.0")]
[Authorize]
public class OrdersController : ApiControllerBase
{
    public OrdersController(ISender sender) : base(sender)
    {
    }

    [HttpGet("{id:guid}", Name = nameof(GetOrderById))]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var query = new GetOrderByIdQuery(id, customerId);
        var result = await Sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var command = new CreateOrderCommand(customerId, request.Currency);
        var result = await Sender.Send(command, cancellationToken);
        
        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return CreatedAtAction(nameof(GetOrderById), new { id = result.Value }, result.Value);
    }

    [HttpPost("{id:guid}/items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddOrderItem(
        Guid id,
        [FromBody] AddOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var command = new EnterpriseCommerce.Application.Orders.Commands.AddOrderItem.AddOrderItemCommand(
            id,
            customerId,
            request.ProductId,
            request.Quantity);

        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }

    [HttpPut("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelOrder(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var command = new EnterpriseCommerce.Application.Orders.Commands.CancelOrder.CancelOrderCommand(id, customerId);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }

    [HttpDelete("{id:guid}/items/{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveOrderItem(Guid id, Guid productId, CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var command = new EnterpriseCommerce.Application.Orders.Commands.RemoveOrderItem.RemoveOrderItemCommand(id, customerId, productId);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }

    [HttpPut("{id:guid}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitOrder(
        Guid id,
        [FromBody] SubmitOrderRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (request?.ShippingAddress is null)
        {
            return HandleFailure(Result.Failure(EnterpriseCommerce.Domain.Orders.OrderErrors.ShippingAddressRequired));
        }

        var addressDto = new ShippingAddressDto(
            request.ShippingAddress.RecipientName,
            request.ShippingAddress.Phone,
            request.ShippingAddress.CountryCode,
            request.ShippingAddress.PostalCode,
            request.ShippingAddress.City,
            request.ShippingAddress.AddressLine1,
            request.ShippingAddress.AddressLine2);

        var command = new SubmitOrderCommand(id, customerId, addressDto);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }

    [HttpPut("{id:guid}/pay")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PayOrder(Guid id, CancellationToken cancellationToken)
    {
        var command = new EnterpriseCommerce.Application.Orders.Commands.MarkOrderAsPaid.MarkOrderAsPaidCommand(id);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }

    [HttpPut("{id:guid}/ship")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ShipOrder(Guid id, CancellationToken cancellationToken)
    {
        var command = new EnterpriseCommerce.Application.Orders.Commands.ShipOrder.ShipOrderCommand(id);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }
}

