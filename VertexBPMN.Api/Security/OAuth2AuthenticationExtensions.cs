using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace VertexBPMN.Api.Security;

public static class OAuth2AuthenticationExtensions
{
    public static IServiceCollection AddOAuth2Authentication(this IServiceCollection services, Action<JwtBearerOptions>? configureOptions = null)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // TODO: Replace with real OIDC authority and audience
                options.Authority = "https://your-oidc-provider";
                options.Audience = "vertexbpmn-api";
                options.RequireHttpsMetadata = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    // For dev: Accept all tokens signed with this key (replace in prod)
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("dev-signing-key-please-change"))
                };
                configureOptions?.Invoke(options);
            });
        return services;
    }
}
