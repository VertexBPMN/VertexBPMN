using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VertexBPMN.Portal.Bff.Controllers;

/// <summary>
/// Session controls for the portal client: ends the local cookie session (which
/// drops the server-side OIDC session from the shared store) and, when live OIDC
/// is configured, forwards to the IdP end-session endpoint.
/// </summary>
[ApiController]
[Route("api/bff")]
[Authorize]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public sealed class BffSessionController : ControllerBase
{
    /// <summary>POST api/bff/logout — ends the authenticated session (cookie + server-side store).</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // Signing out the cookie scheme triggers OidcCookieRefreshEvents.SigningOut,
        // which removes the server-side OIDC session from the shared store.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
        return NoContent();
    }
}
