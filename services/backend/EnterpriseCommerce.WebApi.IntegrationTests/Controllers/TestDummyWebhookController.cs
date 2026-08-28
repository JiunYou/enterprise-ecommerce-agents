using EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;
using EnterpriseCommerce.WebApi.Controllers.v1;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Controllers;

public record DummyWebhookPayload(
    Guid PaymentAttemptId,
    string ProviderEventId,
    string ProviderTransactionId,
    decimal Amount,
    string Currency,
    bool IsSuccess);

[ApiController]
[Route("api/v1/payments")]
public class TestDummyWebhookController : ControllerBase
{
    private readonly ISender _sender;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public TestDummyWebhookController(ISender sender, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _sender = sender;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("webhook/dummy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessDummyWebhook(
        [FromBody] DummyWebhookPayload payload,
        [FromHeader(Name = "X-Dummy-Signature")] string? signature,
        CancellationToken cancellationToken)
    {
        if (_configuration["EnableDummyWebhook"] != "true")
        {
            return NotFound();
        }

        // For MVP dummy provider, enforce a static signature to prove trust boundary concept
        if (signature != "dummy-secret-123")
        {
            return Unauthorized();
        }

        var command = new ProcessPaymentWebhookCommand(
            payload.PaymentAttemptId,
            "dummy_provider",
            payload.ProviderEventId,
            payload.ProviderTransactionId,
            payload.Amount,
            payload.Currency,
            payload.IsSuccess);
        var result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = result.Error.Message
            };
            return BadRequest(problemDetails);
        }

        return Ok();
    }
}
