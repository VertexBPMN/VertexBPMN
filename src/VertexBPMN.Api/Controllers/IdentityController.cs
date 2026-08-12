using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/identity")]
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
    public IAsyncEnumerable<TenantInfo> ListTenants()
        => _identityService.ListTenantsAsync();

    [HttpGet("validate-user")]
    public ActionResult<UserInfo> ValidateUser([FromQuery] string username)
    {
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

    public sealed record PasswordManagementInfo(string Mode, bool LocalPasswordValidation, string? ManagementUrl);
}
