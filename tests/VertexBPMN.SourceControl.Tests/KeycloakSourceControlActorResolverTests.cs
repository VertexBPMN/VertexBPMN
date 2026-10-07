using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class KeycloakSourceControlActorResolverTests
{
	[Fact]
	public async Task Roles_are_reloaded_and_only_the_configured_client_is_authoritative()
	{
		using var handler = new IdentityHandler();
		using var client = new HttpClient(handler);
		var resolver = CreateResolver();
		var context = new SourceControlContext("tenant", "actor");
		Assert.Equal(["ProcessManager"], await resolver.ResolveAsync(context, client, TestContext.Current.CancellationToken));
		handler.Roles = "[]";
		Assert.Empty(await resolver.ResolveAsync(context, client, TestContext.Current.CancellationToken));
		Assert.Equal(8, handler.Requests.Count);
		Assert.EndsWith("/users/actor/role-mappings/clients/api-id/composite", handler.Requests[3]);
		Assert.EndsWith("/clients?clientId=vertexbpmn-api", handler.Requests[2]);
	}

	[Theory]
	[InlineData(false, "tenant", SourceControlErrorCode.Forbidden)]
	[InlineData(true, "other-tenant", SourceControlErrorCode.NotFound)]
	public async Task Disabled_or_moved_users_are_rejected_before_role_lookup(bool enabled, string tenant, SourceControlErrorCode code)
	{
		using var handler = new IdentityHandler { Enabled = enabled, Tenant = tenant };
		using var client = new HttpClient(handler);
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => CreateResolver().ResolveAsync(
			new("tenant", "actor"), client, TestContext.Current.CancellationToken));
		Assert.Equal(code, error.Code);
		Assert.Equal(2, handler.Requests.Count);
	}

	[Theory]
	[InlineData("https://identity.example/realms/")]
	[InlineData("http://identity.example/realms/vertex")]
	[InlineData("https://identity.example/realms/vertex/nested")]
	public async Task Invalid_authorities_are_rejected_without_network_calls(string authority)
	{
		using var handler = new IdentityHandler();
		using var client = new HttpClient(handler);
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => CreateResolver(authority).ResolveAsync(
			new("tenant", "actor"), client, TestContext.Current.CancellationToken));
		Assert.Equal(SourceControlErrorCode.ProviderUnavailable, error.Code);
		Assert.Empty(handler.Requests);
	}

	private static KeycloakSourceControlActorResolver CreateResolver(string authority = "https://identity.example/realms/vertex") => new(
		new Credentials(), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["Jwt:Authority"] = authority, ["Jwt:Audience"] = "vertexbpmn-api"
		}).Build(), Options.Create(new SourceControlOptions
		{
			Enabled = true, IdentityAuthority = authority, IdentityCredentialReference = "identity-reader"
		}));

	private sealed class IdentityHandler : HttpMessageHandler
	{
		public List<string> Requests { get; } = [];
		public bool Enabled { get; init; } = true;
		public string Tenant { get; init; } = "tenant";
		public string Roles { get; set; } = "[{\"name\":\"ProcessManager\"},{\"name\":\"realm-admin\"},{\"name\":\"ProcessManager\"}]";
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var uri = request.RequestUri!.AbsoluteUri;
			Requests.Add(uri);
			string json;
			if (uri.EndsWith("/token", StringComparison.Ordinal))
			{
				Assert.Equal(HttpMethod.Post, request.Method);
				var form = await request.Content!.ReadAsStringAsync(cancellationToken);
				Assert.Contains("grant_type=client_credentials", form);
				json = "{\"access_token\":\"fixture-token\"}";
			}
			else
			{
				Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
				Assert.Equal("fixture-token", request.Headers.Authorization?.Parameter);
				json = uri.Contains("/clients?", StringComparison.Ordinal) ? "[{\"id\":\"api-id\",\"clientId\":\"vertexbpmn-api\"}]"
					: uri.EndsWith("/composite", StringComparison.Ordinal) ? Roles
					: System.Text.Json.JsonSerializer.Serialize(new { id = "actor", enabled = Enabled, attributes = new { tenant_id = new[] { Tenant } } });
			}
			return new(HttpStatusCode.OK) { Content = new StringContent(json) };
		}
	}

	private sealed class Credentials : ICredentialService
	{
		public Task<CredentialMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default) =>
			Task.FromResult<CredentialMetadata?>(new(id, tenantId, "Reader", "KeycloakAdmin", null,
				["clientId", "clientSecret"], DateTime.UnixEpoch, DateTime.UnixEpoch, null));
		public Task<string?> ResolveSecretAsync(string tenantId, string id, string key, CancellationToken cancellationToken = default) =>
			Task.FromResult<string?>(key == "clientId" ? "fixture-client" : "fixture-secret");
		public Task<IReadOnlyList<CredentialMetadata>> ListAsync(string tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<CredentialMetadata> CreateAsync(string tenantId, CredentialWriteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<bool> UpdateMetadataAsync(string tenantId, string id, CredentialMetadataUpdate request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<bool> RotateSecretAsync(string tenantId, string id, CredentialSecretRotation request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
	}
}
