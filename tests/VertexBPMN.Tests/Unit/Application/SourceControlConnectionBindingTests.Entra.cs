using Azure.Core;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Explicit_Entra_application_issues_a_repository_bound_bearer_lease()
	{
		await using var fixture = await Fixture.OpenAsync(new Uri("https://dev.azure.com/team/project/_git/repository"));
		await AssignBearerAsync(fixture);
		var credentials = EntraCredentials();
		var issuer = new EntraIssuer();
		var target = await fixture.CaptureAsync();
		using var lease = await EntraBroker(fixture, credentials, new(), issuer).IssueAsync(Context, target, await ApproveAsync(fixture), Token);
		Assert.NotNull(lease);
		Assert.Equal(1, issuer.Calls);
		Assert.Equal(["new-reference/tenantId", "new-reference/clientId", "new-reference/clientSecret"], credentials.Requests);
		Assert.Equal($":vertex-bearer-v1:{Environment.NewLine}synthetic-entra-token{Environment.NewLine}", await ReadLeaseAsync(lease, target.Remote));
	}

	[Theory]
	[InlineData("https://github.com/team/repository.git")]
	[InlineData("https://dev.azure.com.evil.example/team/project/_git/repository")]
	[InlineData("https://dev.azure.com:8443/team/project/_git/repository")]
	public async Task Entra_cloud_credentials_are_not_released_to_other_endpoints(string remote)
	{
		await using var fixture = await Fixture.OpenAsync(new Uri(remote));
		await AssignBearerAsync(fixture);
		var credentials = EntraCredentials();
		var issuer = new EntraIssuer();
		var target = await fixture.CaptureAsync();
		var approved = await ApproveAsync(fixture);
		await RejectAsync(SourceControlErrorCode.CredentialUnavailable,
			() => EntraBroker(fixture, credentials, new(), issuer).IssueAsync(Context, target, approved, Token));
		Assert.Empty(credentials.Requests);
		Assert.Equal(0, issuer.Calls);
	}

	[Theory]
	[InlineData("rotation", SourceControlErrorCode.CredentialUnavailable)]
	[InlineData("role", SourceControlErrorCode.Forbidden)]
	[InlineData("profile", SourceControlErrorCode.RevisionConflict)]
	public async Task Changes_during_Entra_token_acquisition_cannot_release_a_lease(string change, SourceControlErrorCode expected)
	{
		await using var fixture = await Fixture.OpenAsync(new Uri("https://dev.azure.com/team/project/_git/repository"));
		await AssignBearerAsync(fixture);
		var target = await fixture.CaptureAsync();
		var approved = await ApproveAsync(fixture);
		var credentials = EntraCredentials();
		var actors = new CredentialActors();
		var issuer = new EntraIssuer
		{
			OnIssue = async () =>
			{
				if (change == "rotation")
				{
					credentials.Metadata = credentials.Metadata with { LastModified = DateTime.UtcNow };
				}
				else if (change == "role")
				{
					actors.Roles = ["ReadOnly"];
				}
				else
				{
					Assert.True(await fixture.Store.ReplaceConnectionProfileAsync(Context, Admin, 2, target.Profile with { Revision = 3 }, Token));
				}
			}
		};
		await RejectAsync(expected, () => EntraBroker(fixture, credentials, actors, issuer).IssueAsync(Context, target, approved, Token));
		Assert.Equal(1, issuer.Calls);
	}

	[Theory]
	[InlineData("expired")]
	[InlineData("failure")]
	[InlineData("cancel")]
	public async Task Invalid_Entra_token_or_acquisition_failure_releases_no_material(string outcome)
	{
		await using var fixture = await Fixture.OpenAsync(new Uri("https://dev.azure.com/team/project/_git/repository"));
		await AssignBearerAsync(fixture);
		var target = await fixture.CaptureAsync();
		var approved = await ApproveAsync(fixture);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
		var issuer = new EntraIssuer
		{
			ExpiresOn = outcome == "expired" ? DateTimeOffset.UtcNow.AddSeconds(-1) : DateTimeOffset.UtcNow.AddHours(1),
			OnIssue = () => outcome switch
			{
				"cancel" => cancellation.CancelAsync(),
				"failure" => throw new InvalidOperationException("synthetic-secret-detail"),
				_ => Task.CompletedTask
			}
		};
		var broker = EntraBroker(fixture, EntraCredentials(), new(), issuer);
		if (outcome == "cancel")
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => broker.IssueAsync(Context, target, approved, cancellation.Token));
		}
		else
		{
			var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => broker.IssueAsync(Context, target, approved, Token));
			Assert.DoesNotContain("synthetic-secret-detail", error.ToString(), StringComparison.Ordinal);
		}
	}

	private static ProfiledGitCredentialBroker EntraBroker(Fixture fixture, ProfileCredentials credentials, CredentialActors actors, EntraIssuer issuer)
		=> new(fixture.Store, credentials, actors, Options.Create(new SourceControlOptions { Enabled = true }), issuer);

	[Theory]
	[InlineData("tenantId", "common")]
	[InlineData("tenantId", "00000000-0000-0000-0000-000000000000")]
	[InlineData("clientId", "invalid-client")]
	public async Task Invalid_Entra_identity_releases_no_client_secret(string field, string value)
	{
		await using var fixture = await Fixture.OpenAsync(new Uri("https://dev.azure.com/team/project/_git/repository"));
		await AssignBearerAsync(fixture);
		var credentials = EntraCredentials(field, value);
		var issuer = new EntraIssuer();
		var target = await fixture.CaptureAsync();
		var approved = await ApproveAsync(fixture);
		await RejectAsync(SourceControlErrorCode.CredentialUnavailable,
			() => EntraBroker(fixture, credentials, new(), issuer).IssueAsync(Context, target, approved, Token));
		Assert.DoesNotContain("new-reference/clientSecret", credentials.Requests, StringComparer.Ordinal);
		Assert.Equal(0, issuer.Calls);
	}

	[Fact]
	public async Task Entra_secret_rotation_prevents_token_acquisition_with_mixed_credentials()
	{
		await using var fixture = await Fixture.OpenAsync(new Uri("https://dev.azure.com/team/project/_git/repository"));
		await AssignBearerAsync(fixture);
		var credentials = EntraCredentials();
		credentials.OnSecret = _ =>
		{
			credentials.Metadata = credentials.Metadata with { LastModified = DateTime.UtcNow };
			return Task.CompletedTask;
		};
		var issuer = new EntraIssuer();
		var target = await fixture.CaptureAsync();
		var approved = await ApproveAsync(fixture);
		await RejectAsync(SourceControlErrorCode.CredentialUnavailable,
			() => EntraBroker(fixture, credentials, new(), issuer).IssueAsync(Context, target, approved, Token));
		Assert.Equal(0, issuer.Calls);
	}

	private static ProfileCredentials EntraCredentials(string? field = null, string? value = null) => new()
	{
		Metadata = new("new-reference", Context.TenantId, "test Entra", "GitAzureDevOpsEntra", null,
			["tenantId", "clientId", "clientSecret"], DateTime.UnixEpoch, DateTime.UnixEpoch, null),
		Values = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["tenantId"] = field == "tenantId" ? value! : "11111111-1111-1111-1111-111111111111",
			["clientId"] = field == "clientId" ? value! : "22222222-2222-2222-2222-222222222222",
			["clientSecret"] = "synthetic-client-secret"
		}
	};

	private sealed class EntraIssuer : IAzureDevOpsTokenIssuer
	{
		public int Calls { get; private set; }
		public Func<Task>? OnIssue { get; init; }
		public DateTimeOffset ExpiresOn { get; init; } = DateTimeOffset.UtcNow.AddHours(1);
		public async Task<AccessToken> IssueAsync(Guid tenantId, Guid clientId, string clientSecret, CancellationToken cancellationToken)
		{
			Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), tenantId);
			Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), clientId);
			Assert.Equal("synthetic-client-secret", clientSecret);
			Calls++;
			if (OnIssue is not null)
			{
				await OnIssue();
			}
			cancellationToken.ThrowIfCancellationRequested();
			return new("synthetic-entra-token", ExpiresOn);
		}
	}
}
