using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace VertexBPMN.Studio.Services;

public sealed class StudioApiAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            var accessToken = await httpContext.GetTokenAsync("access_token");
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
            else
            {
                var developmentApiKey = configuration["StudioAuthentication:DevelopmentApiKey"];
                if (!string.IsNullOrWhiteSpace(developmentApiKey))
                    request.Headers.TryAddWithoutValidation("X-API-Key", developmentApiKey);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}