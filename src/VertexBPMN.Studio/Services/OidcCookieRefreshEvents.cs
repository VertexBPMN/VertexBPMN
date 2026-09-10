using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace VertexBPMN.Studio.Services;

public sealed class OidcCookieRefreshEvents(OidcSessionTokenStore tokenStore)
    : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (context.Principal?.Identity?.IsAuthenticated != true)
            return;

        try
        {
            var renewed = await tokenStore.SynchronizeTicketAsync(
                context.Principal,
                context.Properties,
                context.HttpContext.RequestAborted);
            var currentPrincipal = tokenStore.GetCurrentPrincipal(context.Principal);
            if (renewed || !ClaimsEqual(context.Principal, currentPrincipal))
            {
                context.ReplacePrincipal(currentPrincipal);
                context.ShouldRenew = true;
                context.HttpContext.Items[OidcSessionTokenStore.SessionRenewedItem] = true;
            }
        }
        catch (OidcSessionExpiredException)
        {
            tokenStore.Remove(context.Principal);
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    public override Task SigningOut(CookieSigningOutContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            tokenStore.Remove(context.HttpContext.User);
        return Task.CompletedTask;
    }

    private static bool ClaimsEqual(ClaimsPrincipal left, ClaimsPrincipal right) =>
        left.Claims.Select(ClaimKey).Order(StringComparer.Ordinal)
            .SequenceEqual(right.Claims.Select(ClaimKey).Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static string ClaimKey(System.Security.Claims.Claim claim) =>
        $"{claim.Type}\u001f{claim.Value}\u001f{claim.Issuer}";
}
