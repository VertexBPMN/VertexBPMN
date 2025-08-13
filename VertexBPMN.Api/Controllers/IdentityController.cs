    using Microsoft.AspNetCore.Mvc;
    using VertexBPMN.Core.Services;

    namespace VertexBPMN.Api.Controllers;

    [ApiController]
    [Route("api/[controller]")]
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
    public async Task<ActionResult<UserInfo>> ValidateUser([FromQuery] string username, [FromQuery] string password)
        {
            var user = await _identityService.ValidateUserAsync(username, password);
            if (user is null) return NotFound();
            return user;
        }
    }
