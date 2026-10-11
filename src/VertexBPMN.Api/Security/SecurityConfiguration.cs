using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Security.Cryptography;
using VertexBPMN.ServiceDefaults.Security;

namespace VertexBPMN.Api.Security;
/// <summary>
/// Production-Grade Security Configuration
/// Olympic-level feature: Production-Grade Features - Security
/// </summary>
internal static class SecurityConfiguration
{
	public static IServiceCollection AddProductionSecurity(this IServiceCollection services, IConfiguration configuration)
	{
		var authority = configuration["Jwt:Authority"];
		var issuer = configuration["Jwt:Issuer"];
		var audience = configuration["Jwt:Audience"];
		var secretKey = configuration["Jwt:SecretKey"];
		var isDevelopment = string.Equals(configuration["OperationalMode"], "Development", StringComparison.OrdinalIgnoreCase) || string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
		var isOidcTest = string.Equals(configuration["OperationalMode"], "OidcTest", StringComparison.OrdinalIgnoreCase) || string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "OidcTest", StringComparison.OrdinalIgnoreCase);
		var useDevelopmentApiKey = isDevelopment && configuration.GetValue<bool>("Jwt:UseDevelopmentApiKey");
		var requireHttpsMetadata = configuration.GetValue<bool?>("Jwt:RequireHttpsMetadata") ?? !isDevelopment;
		var clockSkewSeconds = configuration.GetValue<int?>("Jwt:ClockSkewSeconds") ?? 30;
		var metadataRefreshIntervalSeconds = configuration.GetValue<int?>("Jwt:MetadataRefreshIntervalSeconds") ?? 30;
		var blockOnMetadataRefresh = configuration.GetValue<bool?>("Jwt:BlockOnMetadataRefresh") ?? true;
		var backchannelAuthority = configuration["Jwt:BackchannelAuthority"];
		LocalOidcBackchannelHandler.Validate(backchannelAuthority, authority, isOidcTest);
		if (clockSkewSeconds is < 0 or > 300)
		{
			throw new InvalidOperationException("Jwt:ClockSkewSeconds must be between 0 and 300 seconds.");
		}

		if (metadataRefreshIntervalSeconds is < 1 or > 300)
		{
			throw new InvalidOperationException("Jwt:MetadataRefreshIntervalSeconds must be between 1 and 300 seconds.");
		}

		if (!requireHttpsMetadata && !isDevelopment && !(isOidcTest && IsLoopbackAuthority(authority)))
		{
			throw new InvalidOperationException("Jwt:RequireHttpsMetadata may be disabled outside Development only for the OidcTest profile with a loopback Authority.");
		}

		if (string.IsNullOrWhiteSpace(audience))
		{
			throw new InvalidOperationException("Jwt:Audience must be configured.");
		}

		if (string.IsNullOrWhiteSpace(authority) && string.IsNullOrWhiteSpace(secretKey))
		{
			if (!isDevelopment)
			{
				throw new InvalidOperationException("Configure Jwt:Authority or Jwt:SecretKey before starting the API outside Development.");
			}

			secretKey = "development-only-vertexbpmn-signing-key-32-bytes!";
		}

		if (string.IsNullOrWhiteSpace(authority) && Encoding.UTF8.GetByteCount(secretKey!) < 32)
		{
			throw new InvalidOperationException("Jwt:SecretKey must contain at least 32 bytes.");
		}

		// IdentityModel 8 refreshes unknown signing keys in the background by default. That
		// makes the first request after an IdP key rotation fail even when the new key is
		// already published. Blocking only the rate-limited metadata refresh preserves the
		// rotation overlap window; operators can opt out if availability is preferred over
		// a seamless first request while their identity provider is unavailable.
		AppContext.SetSwitch("Switch.Microsoft.IdentityModel.UpdateConfigAsBlocking", blockOnMetadataRefresh);
		ConfigureAuthentication(services, new AuthenticationSettings(authority, issuer, audience, secretKey, useDevelopmentApiKey, requireHttpsMetadata, isOidcTest, clockSkewSeconds, metadataRefreshIntervalSeconds, backchannelAuthority));
		ConfigureAuthorizationAndCors(services, configuration);
		// Security Headers
		return services;
	}

	private static readonly string[] DefaultCorsOrigins = ["https://localhost:5001"];

	private sealed record AuthenticationSettings(string? Authority, string? Issuer, string Audience, string? SecretKey, bool UseDevelopmentApiKey, bool RequireHttpsMetadata, bool IsOidcTest, int ClockSkewSeconds, int MetadataRefreshIntervalSeconds, string? BackchannelAuthority);

	private static void ConfigureAuthentication(IServiceCollection services, AuthenticationSettings settings)
	{
		services.AddAuthentication(options =>
		{
			options.DefaultAuthenticateScheme = settings.UseDevelopmentApiKey ? "ApiKey" : JwtBearerDefaults.AuthenticationScheme;
			options.DefaultChallengeScheme = options.DefaultAuthenticateScheme;
		}).AddJwtBearer(options =>
		{
			options.MapInboundClaims = false;
			options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
			if (!string.IsNullOrWhiteSpace(settings.BackchannelAuthority))
			{
				options.BackchannelHttpHandler = new LocalOidcBackchannelHandler(settings.Authority!, settings.BackchannelAuthority, settings.IsOidcTest, new HttpClientHandler());
			}

			options.RefreshOnIssuerKeyNotFound = true;
			options.RefreshInterval = TimeSpan.FromSeconds(settings.MetadataRefreshIntervalSeconds);
			if (!string.IsNullOrWhiteSpace(settings.Authority))
			{
				options.Authority = settings.Authority;
			}

			options.TokenValidationParameters = new TokenValidationParameters
			{
				ValidateIssuer = !string.IsNullOrWhiteSpace(settings.Issuer) || !string.IsNullOrWhiteSpace(settings.Authority),
				ValidateAudience = true,
				ValidateLifetime = true,
				ValidateIssuerSigningKey = string.IsNullOrWhiteSpace(settings.Authority),
				ValidIssuer = settings.Issuer,
				ValidAudience = settings.Audience,
				IssuerSigningKey = string.IsNullOrWhiteSpace(settings.Authority) ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey!)) : null,
				NameClaimType = "preferred_username",
				RoleClaimType = ClaimTypes.Role,
				ClockSkew = TimeSpan.FromSeconds(settings.ClockSkewSeconds)
			};
			if (!string.IsNullOrWhiteSpace(settings.Authority))
			{
				options.Events = new JwtBearerEvents
				{
					OnTokenValidated = context =>
					{
						if (!VertexOidcClaims.TryNormalize(context.Principal!, out var error))
						{
							context.Fail(error);
						}

						return Task.CompletedTask;
					}
				};
			}
		}).AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", _ =>
		{
		});
	}

	private static void ConfigureAuthorizationAndCors(IServiceCollection services, IConfiguration configuration)
	{
		// Authorization Policies
		services.AddAuthorization(options =>
		{
			options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
			options.AddPolicy("AdminOnly", policy => policy.RequireClaim(ClaimTypes.Role, "Admin"));
			options.AddPolicy("ProcessManager", policy => policy.RequireClaim(ClaimTypes.Role, "Admin", "ProcessManager"));
			options.AddPolicy("ReadOnly", policy => policy.RequireClaim(ClaimTypes.Role, "Admin", "ProcessManager", "ReadOnly"));
			AddTenantReadOnlyPolicy(options);
			options.AddPolicy("ProcessViewer", policy => policy.RequireClaim(ClaimTypes.Role, "Admin", "ProcessManager", "ReadOnly"));
			options.AddPolicy("ApiKeyRequired", policy => policy.RequireAuthenticatedUser().AddAuthenticationSchemes("ApiKey"));
			options.AddPolicy("ExternalTaskWorker", policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireRole("ExternalTaskWorker").RequireClaim(VertexOidcClaims.SubjectClaimType).RequireClaim(VertexOidcClaims.TenantClaimType).RequireClaim(VertexOidcClaims.ExternalTaskTopicClaimType));
		});
		// CORS
		services.AddCors(options =>
		{
			options.AddPolicy("Production", policy =>
			{
				policy.WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? DefaultCorsOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
			});
		});
	}

	private static bool IsLoopbackAuthority(string? authority) => Uri.TryCreate(authority, UriKind.Absolute, out var uri) && uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback;
	public static void AddTenantReadOnlyPolicy(Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
	{
		options.AddPolicy("TenantReadOnly", policy => policy.RequireAuthenticatedUser().RequireClaim(ClaimTypes.Role, "Admin", "ProcessManager", "ReadOnly").RequireAssertion(context => context.User.IsInRole("Admin") || !string.IsNullOrWhiteSpace(context.User.FindFirstValue("tenant_id"))));
	}
}
