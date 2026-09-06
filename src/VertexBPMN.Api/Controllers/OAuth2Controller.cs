using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/oauth2")]
public sealed class OAuth2Controller(IOAuth2CredentialFlowService flowService) : ControllerBase
{
    [HttpPost("authorize")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(OAuth2AuthorizationStart), StatusCodes.Status200OK)]
    public async Task<ActionResult<OAuth2AuthorizationStart>> StartAuthorization(
        [FromBody] OAuth2AuthorizeRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });

        try
        {
            var start = await flowService.StartAuthorizationAsync(tenant, request.CredentialId, request.Config, cancellationToken);
            return Ok(start);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid OAuth2 authorization.", Detail = exception.Message });
        }
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string state,
        [FromQuery] string code,
        CancellationToken cancellationToken)
    {
        var completed = await flowService.CompleteAuthorizationAsync(state, code, cancellationToken);
        if (!completed)
            return Unauthorized(new ProblemDetails { Title = "Invalid or expired OAuth2 authorization." });

        return Content("Authorization completed. You may close this tab.", "text/plain");
    }

    private string? ResolveTenant(string? requestedTenantId, out bool forbidden)
    {
        var requested = string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();
        var claimTenant = User.FindFirstValue("tenant_id");
        forbidden = !User.IsInRole("Admin") && requested is not null && !string.Equals(requested, claimTenant, StringComparison.Ordinal);
        return User.IsInRole("Admin") ? requested ?? claimTenant : claimTenant;
    }

    public sealed record OAuth2AuthorizeRequest(
        string? TenantId,
        string CredentialId,
        OAuth2AuthorizationConfig Config);
}
