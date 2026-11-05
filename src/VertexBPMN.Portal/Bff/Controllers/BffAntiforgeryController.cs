using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VertexBPMN.Portal.Bff.Controllers;

/// <summary>
/// Issues an antiforgery request token (cookie + header value) for the portal BFF's
/// cookie-authenticated state-changing requests. The portal host requires the request
/// token on every non-safe BFF action.
/// </summary>
[ApiController]
[Route("api/bff/antiforgery")]
[AllowAnonymous]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public sealed class BffAntiforgeryController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("token")]
    [IgnoreAntiforgeryToken]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "S4502", Justification = "This endpoint issues the antiforgery token; it is intentionally token-free and anonymous so the portal client can obtain the CSRF handshake.")]
    public IActionResult Token()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { requestToken = tokens.RequestToken, cookieName = tokens.CookieToken });
    }
}
