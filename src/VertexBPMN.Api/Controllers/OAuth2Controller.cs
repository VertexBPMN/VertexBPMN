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
        var subject = Subject();
        if (subject is null) return Forbid();
        if (request.BrowserProof is null || request.BrowserProof.Length is < 43 or > 256)
            return BadRequest(new ProblemDetails { Title = "A per-browser OAuth2 proof is required." });

        try
        {
            var start = await flowService.StartAuthorizationAsync(tenant, request.CredentialId, request.Config, cancellationToken,
                new OAuth2FlowBinding(subject, request.BrowserProof));
            return Ok(start);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid OAuth2 authorization.", Detail = exception.Message });
        }
    }

    [HttpPost("callback")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Callback(
        [FromBody] OAuth2CallbackRequest request,
        CancellationToken cancellationToken)
    {
        var subject = Subject();
        if (subject is null) return Forbid();
        if (string.IsNullOrWhiteSpace(request.BrowserProof) || request.BrowserProof.Length > 256) return Unauthorized();
        var completed = await flowService.CompleteAuthorizationAsync(request.State, request.Code, cancellationToken,
            new OAuth2FlowBinding(subject, request.BrowserProof));
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

    private string? Subject()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.Name);
        return claim is null || string.IsNullOrWhiteSpace(claim.Value) ? null
            : System.Text.Json.JsonSerializer.Serialize(new[] { claim.Issuer, claim.Value, User.FindFirstValue("tenant_id") });
    }

    public sealed record OAuth2CallbackRequest(string State, string Code, string BrowserProof);

    public sealed record OAuth2AuthorizeRequest(
        string? TenantId,
        string CredentialId,
        OAuth2AuthorizationConfig Config,
        string? BrowserProof = null);
}
