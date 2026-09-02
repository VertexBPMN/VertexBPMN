using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/credentials")]
[Authorize(Policy = "ReadOnly")]
public sealed class CredentialController(ICredentialService credentialService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CredentialMetadata>>> List(
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(tenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });

        return Ok(await credentialService.ListAsync(tenant, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CredentialMetadata>> Get(
        string id,
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(tenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });

        var credential = await credentialService.GetAsync(tenant, id, cancellationToken);
        return credential is null ? NotFound() : Ok(credential);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(CredentialMetadata), StatusCodes.Status201Created)]
    public async Task<ActionResult<CredentialMetadata>> Create(
        [FromBody] CreateCredentialRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });

        try
        {
            var credential = await credentialService.CreateAsync(tenant, request.ToWriteRequest(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = credential.Id, tenantId = tenant }, credential);
        }
        catch (CredentialConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Credential already exists.", Detail = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid credential.", Detail = exception.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateMetadata(
        string id,
        [FromBody] UpdateCredentialRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });

        try
        {
            return await credentialService.UpdateMetadataAsync(tenant, id, request.ToMetadataUpdate(), cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (CredentialConflictException exception)
        {
            return Conflict(new ProblemDetails { Title = "Credential already exists.", Detail = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid credential.", Detail = exception.Message });
        }
    }

    [HttpPut("{id}/secret")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RotateSecret(
        string id,
        [FromBody] RotateCredentialSecretRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });

        try
        {
            return await credentialService.RotateSecretAsync(tenant, id, request.ToSecretRotation(), cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (CredentialProtectionException exception)
        {
            return UnprocessableEntity(new ProblemDetails { Title = "Credential payload cannot be decrypted.", Detail = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid credential secret.", Detail = exception.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(
        string id,
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(tenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });

        return await credentialService.DeleteAsync(tenant, id, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    private string? ResolveTenant(string? requestedTenantId, out bool forbidden)
    {
        var requested = string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();
        var claimTenant = User.FindFirstValue("tenant_id");
        forbidden = !User.IsInRole("Admin") && requested is not null && !string.Equals(requested, claimTenant, StringComparison.Ordinal);
        return User.IsInRole("Admin") ? requested ?? claimTenant : claimTenant;
    }

    public sealed record CreateCredentialRequest(
        string? TenantId,
        string Name,
        string Type,
        string? Description,
        IReadOnlyDictionary<string, string> Secrets)
    {
        public CredentialWriteRequest ToWriteRequest() => new(Name, Type, Description, Secrets);
    }

    public sealed record UpdateCredentialRequest(string? TenantId, string Name, string Type, string? Description)
    {
        public CredentialMetadataUpdate ToMetadataUpdate() => new(Name, Type, Description);
    }

    public sealed record RotateCredentialSecretRequest(string? TenantId, string Key, string Value)
    {
        public CredentialSecretRotation ToSecretRotation() => new(Key, Value);
    }
}
