using Asp.Versioning;
using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Application.Marketing.Coupons.Commands.CreateCoupon;
using EnterpriseCommerce.Application.Marketing.Coupons.Commands.DeactivateCoupon;
using EnterpriseCommerce.Application.Marketing.Coupons.Queries.GetCoupons;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.WebApi.Contracts.Marketing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.WebApi.Controllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/coupons")]
[Authorize(Roles = "Admin")]
public class AdminCouponsController : ApiControllerBase
{
    public AdminCouponsController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CouponResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCoupons(CancellationToken cancellationToken)
    {
        var query = new GetCouponsQuery();
        var result = await Sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CouponResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCoupon(
        [FromBody] CreateCouponRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCouponCommand(
            request.Code,
            request.DiscountAmount,
            request.Currency,
            request.StartsAt,
            request.ExpiresAt);

        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == CouponErrors.Conflict)
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Coupon Conflict",
                    Status = StatusCodes.Status409Conflict,
                    Detail = result.Error.Message
                });
            }

            return HandleFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/deactivate")]
    [HttpPut("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeactivateCoupon(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateCouponCommand(id);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok();
    }
}
