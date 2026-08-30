using Asp.Versioning;
using EnterpriseCommerce.Application.Identity.Commands.ResolveCustomerIdentity;
using EnterpriseCommerce.WebApi.Contracts.Identity;
using EnterpriseCommerce.WebApi.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace EnterpriseCommerce.WebApi.Controllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal/customer-identities")]
public class CustomerIdentitiesController : ApiControllerBase
{
    private readonly IConfiguration _configuration;

    public CustomerIdentitiesController(ISender sender, IConfiguration configuration) : base(sender)
    {
        _configuration = configuration;
    }

    [HttpPost("resolve")]
    [Authorize(Policy = AuthorizationPolicies.IdentityResolve)]
    [ProducesResponseType(typeof(CustomerIdentityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Resolve(
        [FromBody] ResolveCustomerIdentityRequest request,
        CancellationToken cancellationToken)
    {
        var rawAuthority = _configuration["Authentication:Authority"];
        if (string.IsNullOrWhiteSpace(rawAuthority) || !Uri.TryCreate(rawAuthority, UriKind.Absolute, out var authorityUri))
        {
            throw new InvalidOperationException("Authentication:Authority is not configured or invalid.");
        }

        var canonicalIssuer = authorityUri.AbsoluteUri;

        var command = new ResolveCustomerIdentityCommand(canonicalIssuer, request.Subject);
        var result = await Sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return HandleFailure(result);
        }

        return Ok(new CustomerIdentityResponse(result.Value));
    }
}
