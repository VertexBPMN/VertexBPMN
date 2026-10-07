using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>No actor/role cache, no browser token persistence, no local-user fallback.</summary>
public sealed class KeycloakSourceControlActorResolver(ICredentialService credentials,
	IConfiguration configuration, IOptions<SourceControlOptions> options) : ISourceControlActorResolver
{
	public async Task<IReadOnlyCollection<string>> ResolveAsync(SourceControlContext context, CancellationToken cancellationToken)
	{
		using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false };
		using var client = new HttpClient(handler) { Timeout = options.Value.Limits.ReadTimeout };
		return await ResolveAsync(context, client, cancellationToken);
	}

	internal async Task<IReadOnlyCollection<string>> ResolveAsync(SourceControlContext context, HttpClient client,
		CancellationToken cancellationToken)
	{
		if (!options.Value.Enabled) throw new SourceControlSecurityException(SourceControlErrorCode.Disabled);
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.ReadTimeout);
		try
		{
			var authority = Authority();
			var reference = options.Value.IdentityCredentialReference;
			if (string.IsNullOrWhiteSpace(reference)) Fail(SourceControlErrorCode.CredentialUnavailable);
			var metadata = await credentials.GetAsync(context.TenantId, reference!, deadline.Token);
			if (metadata?.TenantId != context.TenantId || metadata.Type != "KeycloakAdmin") Fail(SourceControlErrorCode.CredentialUnavailable);
			var clientId = await credentials.ResolveSecretAsync(context.TenantId, reference!, "clientId", deadline.Token);
			var secret = await credentials.ResolveSecretAsync(context.TenantId, reference!, "clientSecret", deadline.Token);
			if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret)) Fail(SourceControlErrorCode.CredentialUnavailable);
			using var exchange = new HttpRequestMessage(HttpMethod.Post, authority.AbsoluteUri.TrimEnd('/') + "/protocol/openid-connect/token")
			{
				Content = new FormUrlEncodedContent(new Dictionary<string, string>
				{
					["grant_type"] = "client_credentials", ["client_id"] = clientId!, ["client_secret"] = secret!
				})
			};
			using var tokenResponse = await SendJsonAsync(client, exchange, deadline.Token);
			var token = tokenResponse.RootElement.GetProperty("access_token").GetString();
			if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsControl)) Fail(SourceControlErrorCode.CredentialUnavailable);
			var marker = authority.AbsolutePath.LastIndexOf("/realms/", StringComparison.Ordinal);
			var admin = authority.GetLeftPart(UriPartial.Authority) + authority.AbsolutePath[..marker] + "/admin" + authority.AbsolutePath[marker..];
			var actor = Uri.EscapeDataString(context.ActorId);
			using var user = await GetAsync(client, admin + "/users/" + actor, token!, deadline.Token);
			var value = user.RootElement;
			if (value.GetProperty("id").GetString() != context.ActorId || !value.GetProperty("enabled").GetBoolean()) Fail(SourceControlErrorCode.Forbidden);
			var tenants = value.GetProperty("attributes").GetProperty("tenant_id");
			if (tenants.ValueKind != JsonValueKind.Array || tenants.GetArrayLength() != 1 || tenants[0].GetString() != context.TenantId)
				Fail(SourceControlErrorCode.NotFound);
			var audience = configuration["Jwt:Audience"];
			if (string.IsNullOrWhiteSpace(audience)) Fail(SourceControlErrorCode.ProviderUnavailable);
			using var clients = await GetAsync(client, admin + "/clients?clientId=" + Uri.EscapeDataString(audience!), token!, deadline.Token);
			if (clients.RootElement.ValueKind != JsonValueKind.Array || clients.RootElement.GetArrayLength() != 1
				|| clients.RootElement[0].GetProperty("clientId").GetString() != audience) Fail(SourceControlErrorCode.ProviderUnavailable);
			var apiClient = clients.RootElement[0].GetProperty("id").GetString();
			if (string.IsNullOrWhiteSpace(apiClient)) Fail(SourceControlErrorCode.ProviderUnavailable);
			using var roles = await GetAsync(client, admin + "/users/" + actor + "/role-mappings/clients/"
				+ Uri.EscapeDataString(apiClient!) + "/composite", token!, deadline.Token);
			if (roles.RootElement.ValueKind != JsonValueKind.Array) Fail(SourceControlErrorCode.ProviderUnavailable);
			return roles.RootElement.EnumerateArray().Select(role => role.GetProperty("name").GetString())
				.Where(role => role is "Admin" or "ProcessManager" or "ReadOnly").Select(role => role!)
				.Distinct(StringComparer.Ordinal).ToArray();
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut); }
		catch (OperationCanceledException) { throw; }
		catch (SourceControlSecurityException) { throw; }
		catch { throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable); }
	}

	private Uri Authority()
	{
		if (!Uri.TryCreate(configuration["Jwt:Authority"], UriKind.Absolute, out var authority)) Fail(SourceControlErrorCode.ProviderUnavailable);
		if (options.Value.IdentityAuthority != authority!.AbsoluteUri.TrimEnd('/')) Fail(SourceControlErrorCode.ProviderUnavailable);
		var localTest = configuration["OperationalMode"] == "OidcTest";
		if (authority!.UserInfo.Length != 0 || authority.Query.Length != 0 || authority.Fragment.Length != 0
			|| authority.Scheme != "https" && !(localTest && authority.Scheme == "http" && authority.IsLoopback)
			|| !authority.AbsolutePath.Contains("/realms/", StringComparison.Ordinal)
			|| authority.AbsolutePath[(authority.AbsolutePath.LastIndexOf("/realms/", StringComparison.Ordinal) + 8)..].Trim('/').Contains('/'))
			Fail(SourceControlErrorCode.ProviderUnavailable);
		var realm = authority.AbsolutePath[(authority.AbsolutePath.LastIndexOf("/realms/", StringComparison.Ordinal) + 8)..].Trim('/');
		if (realm.Length == 0 || realm.Contains('%') || realm is "." or "..") Fail(SourceControlErrorCode.ProviderUnavailable);
		return new(authority.AbsoluteUri.TrimEnd('/'));
	}

	private static async Task<JsonDocument> GetAsync(HttpClient client, string uri, string token, CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, uri);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return await SendJsonAsync(client, request, cancellationToken);
	}

	private static async Task<JsonDocument> SendJsonAsync(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
	{
		using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
		if (!response.IsSuccessStatusCode) Fail(SourceControlErrorCode.ProviderUnavailable);
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var buffer = new MemoryStream();
		var bytes = new byte[4096];
		int read;
		while ((read = await stream.ReadAsync(bytes, cancellationToken)) > 0)
		{
			if (buffer.Length + read > 128 * 1024) Fail(SourceControlErrorCode.PayloadTooLarge);
			buffer.Write(bytes, 0, read);
		}
		return JsonDocument.Parse(buffer.ToArray());
	}

	private static void Fail(SourceControlErrorCode code) => throw new SourceControlSecurityException(code);
}
