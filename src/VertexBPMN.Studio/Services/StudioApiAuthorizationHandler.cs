using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace VertexBPMN.Studio.Services;

public sealed class StudioApiAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    IHostEnvironment environment,
    OidcSessionTokenStore tokenStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            string? accessToken = null;
            if (httpContext.User.HasClaim(claim => claim.Type == OidcSessionTokenStore.SessionIdClaim))
            {
                var authentication = await httpContext.AuthenticateAsync();
                accessToken = await tokenStore.GetAccessTokenAsync(
                    httpContext.User,
                    authentication.Properties,
                    cancellationToken);
            }
            else
            {
                accessToken = await httpContext.GetTokenAsync("access_token");
            }
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
            else
            {
                var developmentApiKey = configuration["StudioAuthentication:DevelopmentApiKey"];
                if (!string.IsNullOrWhiteSpace(developmentApiKey)
                    && environment.IsDevelopment()
                    && configuration.GetValue<bool>("StudioAuthentication:LocalDevelopmentEnabled")
                    && !request.Headers.Contains("X-API-Key"))
                    request.Headers.TryAddWithoutValidation("X-API-Key", developmentApiKey);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
