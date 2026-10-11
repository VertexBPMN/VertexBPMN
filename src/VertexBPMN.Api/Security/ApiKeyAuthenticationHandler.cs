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
/// API Key Authentication Handler
/// </summary>
internal class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
	private const string ApiKeyHeaderName = "X-API-Key";
	private readonly IConfiguration _configuration;
	public ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration) : base(options, logger, encoder)
	{
		_configuration = configuration;
	}

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValues))
		{
			return Task.FromResult(AuthenticateResult.Fail("API Key missing"));
		}

		var apiKey = apiKeyValues.FirstOrDefault();
		var validApiKeys = _configuration.GetSection("ApiKeys").Get<string[]>() ?? Array.Empty<string>();
		if (string.IsNullOrEmpty(apiKey) || !validApiKeys.Any(configuredKey => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(configuredKey), Encoding.UTF8.GetBytes(apiKey))))
		{
			return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
		}

		var claims = new List<Claim>
		{
			new(ClaimTypes.Name, "ApiUser"),
			new(ClaimTypes.Role, "ApiAccess")
		};
		var isDevelopment = string.Equals(_configuration["OperationalMode"], "Development", StringComparison.OrdinalIgnoreCase) || string.Equals(_configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
		if (isDevelopment)
		{
			foreach (var role in (_configuration.GetSection("ApiKeyAuthentication:DevelopmentRoles").Get<string[]>() ?? Array.Empty<string>()).Where(role => !string.IsNullOrWhiteSpace(role)))
			{
				claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
			}
		}

		var identity = new ClaimsIdentity(claims, Scheme.Name);
		var principal = new ClaimsPrincipal(identity);
		var ticket = new AuthenticationTicket(principal, Scheme.Name);
		return Task.FromResult(AuthenticateResult.Success(ticket));
	}
}
