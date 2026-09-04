using Asp.Versioning;
using EnterpriseCommerce.Application.Payments.Commands.InitiatePayment;
using EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;
using EnterpriseCommerce.WebApi.Contracts.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseCommerce.WebApi.Controllers.v1;

[ApiVersion("1.0")]
public class PaymentsController : ApiControllerBase
{
    private readonly EnterpriseCommerce.Infrastructure.Payments.ECPay.IECPayPaymentNotificationService _ecpayNotificationService;

    public PaymentsController(
        ISender sender,
        EnterpriseCommerce.Infrastructure.Payments.ECPay.IECPayPaymentNotificationService ecpayNotificationService) : base(sender)
    {
        _ecpayNotificationService = ecpayNotificationService;
    }

    [Authorize]
    [HttpPost("initiate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var command = new InitiatePaymentCommand(request.OrderId, request.IdempotencyKey, customerId);

        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpPost("webhooks/ecpay")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessECPayWebhook(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> formFields;
        try
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in form.Keys)
            {
                dict[key] = form[key].ToString();
            }
            formFields = dict;
        }
        catch (Exception ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Form Payload",
                Detail = ex.Message
            });
        }

        ProcessPaymentWebhookCommand? command;
        try
        {
            command = _ecpayNotificationService.VerifyAndParseNotification(formFields);
        }
        catch (EnterpriseCommerce.Infrastructure.Payments.ECPay.ECPayNotificationValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "ECPay ReturnURL Validation Error",
                Detail = ex.Message
            });
        }

        if (command is null)
        {
            // Authenticated notification is simulated (SimulatePaid=1) or non-success status; acknowledge with exact 1|OK without domain mutation.
            return Content("1|OK", "text/plain");
        }

        var result = await Sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Content("1|OK", "text/plain");
    }
}
