using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace VertexBPMN.Studio.Services;

/// <summary>Revalidates long-lived circuits without discarding editor drafts.</summary>
public class OidcCircuitAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    OidcSessionTokenStore tokenStore) : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(15);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        try
        {
            await tokenStore.GetAccessTokenAsync(authenticationState.User, null, cancellationToken);
            var current = tokenStore.GetCurrentPrincipal(authenticationState.User);
            if (!ClaimsEqual(authenticationState.User, current))
                SetAuthenticationState(Task.FromResult(new AuthenticationState(current)));
            return true;
        }
        catch (OidcSessionExpiredException)
        {
            return false;
        }
    }

    private static bool ClaimsEqual(ClaimsPrincipal left, ClaimsPrincipal right) =>
        left.Claims.Select(claim => (claim.Type, claim.Value, claim.Issuer)).OrderBy(claim => claim)
            .SequenceEqual(right.Claims.Select(claim => (claim.Type, claim.Value, claim.Issuer)).OrderBy(claim => claim));
}
