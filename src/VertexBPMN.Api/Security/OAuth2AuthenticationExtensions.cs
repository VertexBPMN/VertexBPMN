using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace VertexBPMN.Api.Security;

public static class OAuth2AuthenticationExtensions
{
    [Obsolete("Use AddProductionSecurity with IConfiguration so issuer, audience, authority, and signing keys are validated centrally.")]
    public static IServiceCollection AddOAuth2Authentication(this IServiceCollection services, Action<JwtBearerOptions>? configureOptions = null)
    {
        throw new InvalidOperationException("OAuth2 authentication must be registered through AddProductionSecurity(IConfiguration).");
    }
}
