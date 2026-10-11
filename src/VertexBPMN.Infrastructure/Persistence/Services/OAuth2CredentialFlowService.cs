using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.Services;

/// <summary>
/// OAuth2-Authorization-Code-Flow (RFC 6749 §4.1).
/// Persistente Metadaten in <see cref="OAuth2FlowStateRecord"/> (tenant-isoliert),
/// Tokens als verschlüsselte Secrets im Credential-Store ("access_token", "refresh_token",
/// "expires_at", "token_url", "client_id", "client_secret").
/// </summary>
public sealed class OAuth2CredentialFlowService(
	BpmnDbContext db,
	ICredentialService credentialService,
	IHttpClientFactory httpClientFactory,
	IAuditLogService auditLogService,
	ILogger<OAuth2CredentialFlowService> logger) : IOAuth2CredentialFlowService
{
	private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);
	private static readonly TimeSpan AccessTokenSkew = TimeSpan.FromSeconds(60);

	public async Task<OAuth2AuthorizationStart> StartAuthorizationAsync(
		string tenantId, string credentialId, OAuth2AuthorizationConfig config, OAuth2FlowBinding? binding = null, CancellationToken cancellationToken = default)
	{
		if (!Uri.TryCreate(config.AuthorizationUrl, UriKind.Absolute, out var authorizationUri)
			|| authorizationUri.Scheme != "https" || !string.IsNullOrEmpty(authorizationUri.Fragment))
		{
			throw new ArgumentException("OAuth2 authorization URL must be an absolute HTTPS URL without a fragment.");
		}

		var credential = await credentialService.GetAsync(tenantId, credentialId, cancellationToken);
		if (credential is null)
		{
			throw new ArgumentException("The credential does not exist.");
		}

		var now = DateTime.UtcNow;
		await PruneExpiredAsync(tenantId, now, cancellationToken);

		string state;
		bool exists;
		do
		{
			state = CreateState();
			exists = await db.OAuth2FlowStates.AnyAsync(
				s => s.TenantId == tenantId && s.State == state, cancellationToken);
		} while (exists);

		db.OAuth2FlowStates.Add(CreateStateRecord(tenantId, credentialId, config, state, now));
		if (binding is not null)
		{
			if (string.IsNullOrWhiteSpace(binding.Subject) || binding.BrowserProof.Length is < 43 or > 256)
			{
				throw new ArgumentException("OAuth2 requires an authenticated subject and a per-browser proof.");
			}

			db.RuntimeInbox.Add(new RuntimeInboxMessage
			{
				Id = Guid.NewGuid(),
				Operation = "oauth2-browser-binding",
				IdempotencyKey = state,
				TenantId = tenantId,
				TenantScope = tenantId,
				ReceivedAt = now,
				Result = JsonSerializer.Serialize(new OAuth2FlowBinding(binding.Subject, HashProof(binding.BrowserProof)))
			});
		}
		await db.SaveChangesAsync(cancellationToken);

		var redirectUrl = BuildRedirectUrl(config, state);

#pragma warning disable CA1873 // Logging is guarded by IsEnabled to bound evaluation when disabled; plain variable args.
		if (logger.IsEnabled(LogLevel.Information))
		{
			Logger().LogInformation("OAuth2 state issued for credential {CredentialId} in tenant {TenantId}", credentialId, tenantId);
		}
#pragma warning restore CA1873

		return new OAuth2AuthorizationStart(redirectUrl, state);
	}

	private static OAuth2FlowStateRecord CreateStateRecord(
		string tenantId, string credentialId, OAuth2AuthorizationConfig config, string state, DateTime now)
		=> new()
		{
			State = state,
			TenantId = tenantId,
			CredentialId = credentialId,
			AuthorizationUrl = config.AuthorizationUrl,
			TokenUrl = config.TokenUrl,
			ClientId = config.ClientId,
			RedirectUri = config.RedirectUri,
			Scopes = config.Scopes,
			CreatedAt = now,
			ExpiresAt = now.Add(StateTtl)
		};

	private static string BuildRedirectUrl(OAuth2AuthorizationConfig config, string state)
		=> $"{config.AuthorizationUrl}" +
			$"{(config.AuthorizationUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?")}response_type=code" +
			$"&client_id={Uri.EscapeDataString(config.ClientId)}" +
			$"&redirect_uri={Uri.EscapeDataString(config.RedirectUri)}" +
			$"&scope={Uri.EscapeDataString(config.Scopes)}" +
			$"&state={Uri.EscapeDataString(state)}";

	public async Task<bool> CompleteAuthorizationAsync(
		string state, string code, OAuth2FlowBinding? binding = null, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(code))
		{
			return false;
		}

		var record = await db.OAuth2FlowStates.FirstOrDefaultAsync(s => s.State == state, cancellationToken);
		if (record is null || record.ExpiresAt <= DateTime.UtcNow)
		{
			if (record is not null)
			{
				db.OAuth2FlowStates.Remove(record);
				await db.SaveChangesAsync(cancellationToken);
			}

			Logger().LogWarning("OAuth2 callback with invalid or expired state");
			return false;
		}

		var browserRecord = await db.RuntimeInbox.SingleOrDefaultAsync(x =>
			x.Operation == "oauth2-browser-binding" && x.IdempotencyKey == state && x.TenantId == record.TenantId, cancellationToken);
		var expectedBinding = browserRecord?.Result is { } serialized ? JsonSerializer.Deserialize<OAuth2FlowBinding>(serialized) : null;
		if (!VerifyBinding(expectedBinding, binding))
		{
			return false;
		}

		var clientSecret = await credentialService.ResolveSecretAsync(
			record.TenantId, record.CredentialId, "client_secret", cancellationToken);
		if (clientSecret is null)
		{
			Logger().LogWarning("OAuth2 credential {CredentialId} has no client_secret", record.CredentialId);
			return false;
		}

		var http = httpClientFactory.CreateClient("VertexBPMN.PublicEndpoints");
		return await CompleteTokenExchangeAsync(http, record, browserRecord, code, clientSecret, cancellationToken);
	}

	private async Task<bool> CompleteTokenExchangeAsync(
		HttpClient http, OAuth2FlowStateRecord record, RuntimeInboxMessage? browserRecord, string code, string clientSecret, CancellationToken cancellationToken)
	{
		var form = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["grant_type"] = "authorization_code",
			["code"] = code,
			["redirect_uri"] = record.RedirectUri,
			["client_id"] = record.ClientId,
			["client_secret"] = clientSecret
		};

		var tokenEndpoint = await TryGetPublicEndpointAsync(record.TokenUrl, cancellationToken);
		if (tokenEndpoint is null)
		{
			Logger().LogWarning("OAuth2 token URL for credential {CredentialId} is not a valid public destination", record.CredentialId);
			return false;
		}

		// Consume before contacting the provider. EF checks affected rows on DELETE:
		// concurrent callbacks cannot both win, across API replicas as well.
		db.OAuth2FlowStates.Remove(record);
		if (browserRecord is not null)
		{
			db.RuntimeInbox.Remove(browserRecord);
		}

		try
		{ await db.SaveChangesAsync(cancellationToken); }
		catch (DbUpdateConcurrencyException) { return false; }

		var tokens = await PostAuthorizationCodeTokenAsync(http, tokenEndpoint, form, record.CredentialId, cancellationToken);
		if (tokens is null)
		{
			return false;
		}

		await PersistTokensAsync(record.TenantId, record.CredentialId, record.TokenUrl, record.ClientId, tokens, cancellationToken);

#pragma warning disable CA1873 // Logging is guarded by IsEnabled to bound evaluation when disabled; plain variable args.
		if (logger.IsEnabled(LogLevel.Information))
		{
			Logger().LogInformation("OAuth2 token stored for credential {CredentialId}", record.CredentialId);
		}
#pragma warning restore CA1873

		return true;
	}

	private async Task PersistTokensAsync(
		string tenantId, string credentialId, string tokenUrl, string clientId, OAuth2TokenResponse tokens, CancellationToken cancellationToken)
	{
		await RotateAsync(tenantId, credentialId, "access_token", tokens.AccessToken, cancellationToken);
		await RotateAsync(tenantId, credentialId, "expires_at", tokens.ExpiresAt, cancellationToken);
		// token_url + client_id werden für den Refresh-Flow dauerhaft im Credential-Store abgelegt.
		await RotateAsync(tenantId, credentialId, "token_url", tokenUrl, cancellationToken);
		await RotateAsync(tenantId, credentialId, "client_id", clientId, cancellationToken);
		if (tokens.RefreshToken is not null)
		{
			await RotateAsync(tenantId, credentialId, "refresh_token", tokens.RefreshToken, cancellationToken);
		}

		await auditLogService.RecordAsync(new AuditLog
		{
			Timestamp = DateTimeOffset.UtcNow,
			Action = "credential.oauth2_completed",
			Resource = "credential",
			ResourceId = credentialId,
			TenantId = tenantId,
			StatusCode = 200
		}, cancellationToken);
	}

	private static string HashProof(string proof) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(proof)));

	public async Task<string?> ResolveValidAccessTokenAsync(
		string tenantId,
		string credentialId,
		CancellationToken cancellationToken = default)
	{
		var accessToken = await credentialService.ResolveSecretAsync(tenantId, credentialId, "access_token", cancellationToken);
		var expiresAtRaw = await credentialService.ResolveSecretAsync(tenantId, credentialId, "expires_at", cancellationToken);
		if (accessToken is not null
			&& DateTime.TryParse(expiresAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt)
			&& expiresAt > DateTime.UtcNow.Add(AccessTokenSkew))
		{
			return accessToken;
		}

		var context = await BuildRefreshContextAsync(tenantId, credentialId, cancellationToken);
		if (context is null)
		{
			return null;
		}

		var tokens = await PostRefreshTokenAsync(context.Http, context.Endpoint, context.Form, credentialId, cancellationToken);
		if (tokens is null)
		{
			return null;
		}

		await RotateAsync(tenantId, credentialId, "access_token", tokens.AccessToken, cancellationToken);
		await RotateAsync(tenantId, credentialId, "expires_at", tokens.ExpiresAt, cancellationToken);
		if (tokens.RefreshToken is not null)
		{
			await RotateAsync(tenantId, credentialId, "refresh_token", tokens.RefreshToken, cancellationToken);
		}

#pragma warning disable CA1873 // Logging is guarded by IsEnabled to bound evaluation when disabled; plain variable args.
		if (logger.IsEnabled(LogLevel.Information))
		{
			Logger().LogInformation("OAuth2 access token refreshed for credential {CredentialId}", credentialId);
		}
#pragma warning restore CA1873

		return tokens.AccessToken;
	}

	private async Task<RefreshContext?> BuildRefreshContextAsync(string tenantId, string credentialId, CancellationToken cancellationToken)
	{
		var refreshToken = await credentialService.ResolveSecretAsync(tenantId, credentialId, "refresh_token", cancellationToken);
		var tokenUrl = await credentialService.ResolveSecretAsync(tenantId, credentialId, "token_url", cancellationToken);
		if (refreshToken is null || tokenUrl is null)
		{
			return null;
		}

		var clientId = await credentialService.ResolveSecretAsync(tenantId, credentialId, "client_id", cancellationToken);
		var clientSecret = await credentialService.ResolveSecretAsync(tenantId, credentialId, "client_secret", cancellationToken);
		if (clientId is null || clientSecret is null)
		{
			return null;
		}

		var refreshEndpoint = await TryGetPublicEndpointAsync(tokenUrl, cancellationToken);
		if (refreshEndpoint is null)
		{
			Logger().LogWarning("OAuth2 token URL for credential {CredentialId} is not a valid public destination", credentialId);
			return null;
		}

		var http = httpClientFactory.CreateClient("VertexBPMN.PublicEndpoints");
		var form = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["grant_type"] = "refresh_token",
			["refresh_token"] = refreshToken,
			["client_id"] = clientId,
			["client_secret"] = clientSecret
		};

		return new RefreshContext(http, form, refreshEndpoint);
	}

	private sealed record RefreshContext(HttpClient Http, Dictionary<string, string> Form, Uri Endpoint);

	private static bool VerifyBinding(OAuth2FlowBinding? expectedBinding, OAuth2FlowBinding? binding)
	{
		if (expectedBinding is null && binding is null)
		{
			return true;
		}

		return expectedBinding is not null
			&& binding is not null
			&& string.Equals(expectedBinding.Subject, binding.Subject, StringComparison.Ordinal)
			&& CryptographicOperations.FixedTimeEquals(
				Convert.FromHexString(expectedBinding.BrowserProof),
				Convert.FromHexString(HashProof(binding.BrowserProof)));
	}

	private async Task<OAuth2TokenResponse?> PostAuthorizationCodeTokenAsync(
		HttpClient http,
		Uri endpoint,
		IEnumerable<KeyValuePair<string, string>> form,
		string credentialId,
		CancellationToken cancellationToken)
	{
		using var content = new FormUrlEncodedContent(form);
		HttpResponseMessage response;
		try
		{
			response = await http.PostAsync(endpoint, content, cancellationToken);
		}
		catch (Exception exception)
		{
			Logger().LogWarning(exception, "OAuth2 token request failed for credential {CredentialId}", credentialId);
			return null;
		}

		using var responseLifetime = response;
		if (!response.IsSuccessStatusCode)
		{
			Logger().LogWarning("OAuth2 token endpoint returned {Status} for credential {CredentialId}", (int)response.StatusCode, credentialId);
			return null;
		}

		return ParseTokenResponse(await response.Content.ReadAsStringAsync(cancellationToken));
	}

	private async Task<OAuth2TokenResponse?> PostRefreshTokenAsync(
		HttpClient http,
		Uri endpoint,
		IEnumerable<KeyValuePair<string, string>> form,
		string credentialId,
		CancellationToken cancellationToken)
	{
		using var content = new FormUrlEncodedContent(form);
		HttpResponseMessage response;
		try
		{
			response = await http.PostAsync(endpoint, content, cancellationToken);
		}
		catch (Exception exception)
		{
			Logger().LogWarning(exception, "OAuth2 refresh failed for credential {CredentialId}", credentialId);
			return null;
		}

		using var responseLifetime = response;
		if (!response.IsSuccessStatusCode)
		{
			Logger().LogWarning("OAuth2 refresh endpoint returned {Status} for credential {CredentialId}", (int)response.StatusCode, credentialId);
			return null;
		}

		return ParseTokenResponse(await response.Content.ReadAsStringAsync(cancellationToken));
	}

	private static OAuth2TokenResponse? ParseTokenResponse(string json)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return null;
		}

		var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
		var expiresIn = root.TryGetProperty("expires_in", out var ei) && ei.ValueKind == JsonValueKind.Number
			? ei.GetInt32()
			: 3600;
		var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn).ToString("o", CultureInfo.InvariantCulture);
		return new OAuth2TokenResponse(accessToken, refreshToken, expiresAt);
	}

	private sealed record OAuth2TokenResponse(string AccessToken, string? RefreshToken, string ExpiresAt);

	private static async Task<Uri?> TryGetPublicEndpointAsync(string url, CancellationToken cancellationToken)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || !string.IsNullOrEmpty(endpoint.UserInfo))
		{
			return null;
		}

		try
		{
			await ConnectorDestinationPolicy.ThrowIfForbiddenAsync(endpoint.Host, cancellationToken, rejectUnresolvable: false);
			return endpoint;
		}
		catch (ServiceTaskExecutionException)
		{
			return null;
		}
	}

	private async Task RotateAsync(
		string tenantId,
		string credentialId,
		string key,
		string value,
		CancellationToken cancellationToken)
	{
		await credentialService.RotateSecretAsync(
			tenantId,
			credentialId,
			new CredentialSecretRotation(key, value),
			cancellationToken);
	}

	private async Task PruneExpiredAsync(string tenantId, DateTime now, CancellationToken cancellationToken)
	{
		var expired = await db.OAuth2FlowStates
			.Where(s => s.TenantId == tenantId && s.ExpiresAt <= now)
			.ToListAsync(cancellationToken);
		if (expired.Count == 0)
		{
			return;
		}

		db.OAuth2FlowStates.RemoveRange(expired);
		var expiredStates = expired.Select(x => x.State).ToArray();
		var bindings = await db.RuntimeInbox.Where(x => x.Operation == "oauth2-browser-binding"
			&& x.TenantId == tenantId && expiredStates.Contains(x.IdempotencyKey)).ToListAsync(cancellationToken);
		db.RuntimeInbox.RemoveRange(bindings);
		await db.SaveChangesAsync(cancellationToken);
	}

	private static string CreateState()
	{
		Span<byte> bytes = stackalloc byte[32];
		RandomNumberGenerator.Fill(bytes);
		return Convert.ToBase64String(bytes)
			.Replace("+", "-", StringComparison.Ordinal)
			.Replace("/", "_", StringComparison.Ordinal)
			.TrimEnd('=');
	}

	private ILogger<OAuth2CredentialFlowService> Logger() => logger;
}
