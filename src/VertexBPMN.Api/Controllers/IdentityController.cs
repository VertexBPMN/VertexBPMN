using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/identity")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public class IdentityController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly IConfiguration _configuration;

    public IdentityController(IIdentityService identityService, IConfiguration configuration)
    {
        _identityService = identityService;
        _configuration = configuration;
    }

    [HttpGet("list-tenants")]
    [Authorize(Policy = "AdminOnly")]
    public IAsyncEnumerable<TenantInfo> ListTenants()
        => _identityService.ListTenantsAsync();

    [HttpGet("validate-user")]
    public ActionResult<UserInfo> ValidateUser([FromQuery] string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new ProblemDetails { Title = "A username is required." });
        }

        if (Request.Query.ContainsKey("password"))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Password query parameters are not supported",
                Detail = "Do not send passwords to VertexBPMN. Manage user passwords through the external OIDC provider."
            });
        }

        return StatusCode(StatusCodes.Status501NotImplemented,
            new ProblemDetails
            {
                Title = "Password validation is unavailable",
                Detail = "Authentication is delegated to the configured external identity provider. No password is accepted or stored by VertexBPMN."
            });
    }

    [HttpGet("password-management")]
    public ActionResult<PasswordManagementInfo> PasswordManagement()
    {
        var managementUrl = _configuration["Identity:PasswordManagementUrl"];
        return Ok(new PasswordManagementInfo(
            Mode: "external-oidc",
            LocalPasswordValidation: false,
            ManagementUrl: string.IsNullOrWhiteSpace(managementUrl) ? null : managementUrl));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type PasswordManagementInfo retained for exposed HTTP action signatures and controller activation (IdentityController).")]
    [method: System.Text.Json.Serialization.JsonConstructor]
    public sealed record PasswordManagementInfo(string Mode, bool LocalPasswordValidation, string? ManagementUrl)
    {
        public PasswordManagementInfo(string Mode, bool LocalPasswordValidation, Uri ManagementUrl)
            : this(Mode, LocalPasswordValidation, ManagementUrl.AbsoluteUri)
        {
        }
    }
}
