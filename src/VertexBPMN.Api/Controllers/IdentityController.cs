    using Microsoft.AspNetCore.Mvc;
    using VertexBPMN.Domain.Interfaces;

    namespace VertexBPMN.Api.Controllers;

    [ApiController]
    [Route("api/identity")]
    public class IdentityController : ControllerBase
    {
        private readonly IIdentityService _identityService;

        public IdentityController(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        [HttpGet("list-tenants")]
        public IAsyncEnumerable<TenantInfo> ListTenants()
            => _identityService.ListTenantsAsync();

        [HttpGet("validate-user")]
        public ActionResult<UserInfo> ValidateUser([FromQuery] string username, [FromQuery] string password)
            => StatusCode(StatusCodes.Status501NotImplemented,
                new ProblemDetails
                {
                    Title = "Password validation is unavailable",
                    Detail = "Authentication is delegated to the configured external identity provider."
                });
    }
