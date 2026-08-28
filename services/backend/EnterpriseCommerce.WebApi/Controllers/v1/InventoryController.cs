using Asp.Versioning;
using EnterpriseCommerce.Application.Inventory.Commands.ReserveInventory;
using EnterpriseCommerce.WebApi.Contracts.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseCommerce.WebApi.Controllers.v1;

[ApiVersion("1.0")]
[Authorize]
public class InventoryController : ApiControllerBase
{
    public InventoryController(ISender sender) : base(sender)
    {
    }

    [HttpPost("reserve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReserveInventory([FromBody] ReserveInventoryRequest request, CancellationToken cancellationToken)
    {
        var command = new ReserveInventoryCommand(request.ProductId, request.OrderId, request.Quantity);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }
}
