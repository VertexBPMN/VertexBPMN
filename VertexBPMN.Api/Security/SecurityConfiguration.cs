using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace VertexBPMN.Api.Security;

/// <summary>
/// Production-Grade Security Configuration
/// Olympic-level feature: Production-Grade Features - Security
/// </summary>
public static class SecurityConfiguration
{
    public static IServiceCollection AddProductionSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        // JWT Authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"] ?? "default-secret-key-for-development"))
                };
            });

        // API Key Authentication
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", options => { });

        // Authorization Policies
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => 
                policy.RequireClaim(ClaimTypes.Role, "Admin"));
            
            options.AddPolicy("ProcessManager", policy => 
                policy.RequireClaim(ClaimTypes.Role, "Admin", "ProcessManager"));
            
            options.AddPolicy("ReadOnly", policy => 
                policy.RequireClaim(ClaimTypes.Role, "Admin", "ProcessManager", "ReadOnly"));

            options.AddPolicy("ApiKeyRequired", policy =>
                policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ApiKey"));
        });

        // CORS
        services.AddCors(options =>
        {
            options.AddPolicy("Production", policy =>
            {
                policy.WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "https://localhost:5001" })
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        // Security Headers
        services.AddScoped<SecurityHeadersMiddleware>();

        return services;
    }
}

/// <summary>
/// API Key Authentication Handler
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey(ApiKeyHeaderName))
        {
            return Task.FromResult(AuthenticateResult.Fail("API Key missing"));
        }

        var apiKey = Request.Headers[ApiKeyHeaderName].FirstOrDefault();
        var validApiKeys = _configuration.GetSection("ApiKeys").Get<string[]>() ?? Array.Empty<string>();

        if (string.IsNullOrEmpty(apiKey) || !validApiKeys.Contains(apiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "ApiUser"),
            new Claim(ClaimTypes.Role, "ApiAccess")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Security Headers Middleware
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Security headers
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        await _next(context);
    }
}
