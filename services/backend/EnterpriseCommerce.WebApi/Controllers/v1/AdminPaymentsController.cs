using Asp.Versioning;
using EnterpriseCommerce.Application.Payments.Commands.AdminRefundPayment;
using EnterpriseCommerce.WebApi.Contracts.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseCommerce.WebApi.Controllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/payments")]
[Authorize(Roles = "Admin")]
public sealed class AdminPaymentsController : ApiControllerBase
{
    public AdminPaymentsController(ISender sender) : base(sender)
    {
    }

    [HttpPost("{paymentAttemptId:guid}/refund")]
    [ProducesResponseType(typeof(AdminRefundPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RefundPayment(
        Guid paymentAttemptId,
        [FromBody] AdminRefundPaymentRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetAdminActor(out var issuer, out var subject))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Missing trusted actor identity."
            });
        }

        var command = new AdminRefundPaymentCommand(
            paymentAttemptId,
            issuer,
            subject,
            request?.Reason);

        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(new AdminRefundPaymentResponse(
            result.Value.PaymentAttemptId,
            result.Value.Outcome.ToString(),
            result.Value.RefundStatus?.ToString()));
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
