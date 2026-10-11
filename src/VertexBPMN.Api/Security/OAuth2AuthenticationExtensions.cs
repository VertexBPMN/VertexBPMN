using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace VertexBPMN.Api.Security;

internal static class OAuth2AuthenticationExtensions
{
    [Obsolete("Use AddProductionSecurity with IConfiguration so issuer, audience, authority, and signing keys are validated centrally.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "S1133", Justification = "Fail-closed compatibility entry point prevents older callers from registering incomplete token validation. Removal requires migration of those callers, not silently accepting insecure registration.")]
    public static IServiceCollection AddOAuth2Authentication(this IServiceCollection services, Action<JwtBearerOptions>? configureOptions = null)
    {
        throw new InvalidOperationException("OAuth2 authentication must be registered through AddProductionSecurity(IConfiguration).");
    }
}
