using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Theory]
	[InlineData("invalid")]
	[InlineData("expired")]
	[InlineData("near-expiry")]
	public async Task Hosting_credentials_reject_unusable_expiry_before_resolving_the_token(string expiry)
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		var execution = await HostingCredentialExecutionAsync(fixture, setup);
		var credentials = HostingCredentials();
		var values = (Dictionary<string, string>)credentials.Values!;
		var expiryOffset = expiry == "expired" ? -60 : 10;
		values["expiresAt"] = expiry == "invalid" ? "not-an-expiry"
			: DateTimeOffset.UtcNow.AddSeconds(expiryOffset).ToString("O", CultureInfo.InvariantCulture);
		await RejectAsync(SourceControlErrorCode.CredentialUnavailable, () => HostingBroker(setup, credentials)
			.IssueGitHubAsync(Context, execution, HostingApproval(execution), Token));
		Assert.Equal("api-credential/expiresAt", Assert.Single(credentials.Requests));
	}

	[Fact]
	public async Task Hosting_credentials_reject_another_API_repository_before_accessing_credentials()
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		var execution = await HostingCredentialExecutionAsync(fixture, setup);
		var credentials = HostingCredentials();
		var approved = new ApprovedGitHttpsTarget(new Uri("https://api.github.com/repos/team/other"),
			HostingHttpsContext.ApiProfile(execution.Target.Profile), [IPAddress.Parse("8.8.8.8")])
		{ AdministrativePolicy = execution.ApiPolicy };
		await RejectAsync(SourceControlErrorCode.ContentUnsafe, () => HostingBroker(setup, credentials)
			.IssueGitHubAsync(Context, execution, approved, Token));
		Assert.Equal(0, credentials.MetadataCalls);
		Assert.Empty(credentials.Requests);
	}

	[Theory]
	[InlineData("id")]
	[InlineData("tenant")]
	[InlineData("type")]
	[InlineData("keys")]
	public async Task Hosting_credentials_reject_invalid_metadata_before_reading_secrets(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		var execution = await HostingCredentialExecutionAsync(fixture, setup);
		var credentials = HostingCredentials();
		credentials.Metadata = change switch
		{
			"id" => credentials.Metadata with { Id = "other-reference" },
			"tenant" => credentials.Metadata with { TenantId = "other-tenant" },
			"type" => credentials.Metadata with { Type = "GitHttpBearer" },
			_ => credentials.Metadata with { SecretKeys = ["token"] }
		};
		await RejectAsync(SourceControlErrorCode.CredentialUnavailable, () => HostingBroker(setup, credentials)
			.IssueGitHubAsync(Context, execution, HostingApproval(execution), Token));
		Assert.Empty(credentials.Requests);
	}

	[Fact]
	public async Task Hosting_credentials_use_only_the_API_reference_and_bound_the_lease()
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		var execution = await HostingCredentialExecutionAsync(fixture, setup);
		var credentials = HostingCredentials();
		using var lease = await HostingBroker(setup, credentials).IssueGitHubAsync(Context, execution, HostingApproval(execution), Token);
		Assert.Equal("synthetic-hosting-token", lease.Authorization().Parameter);
		Assert.Collection(credentials.Requests,
			request => Assert.Equal("api-credential/expiresAt", request),
			request => Assert.Equal("api-credential/token", request));
		Assert.True(lease.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(2));
		lease.Dispose();
		Assert.Throws<SourceControlSecurityException>(() => lease.Authorization());
	}

	[Theory]
	[InlineData("rotation")]
	[InlineData("revocation")]
	[InlineData("roles")]
	public async Task Hosting_credentials_recheck_authority_after_secret_resolution(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		var execution = await HostingCredentialExecutionAsync(fixture, setup);
		var credentials = HostingCredentials();
		var actors = new CredentialActors { Roles = ["Admin"] };
		credentials.OnSecret = async key =>
		{
			if (key != "token")
			{
				return;
			}
			if (change == "rotation")
			{
				credentials.Metadata = credentials.Metadata with { LastModified = DateTime.UnixEpoch.AddSeconds(1) };
			}
			else if (change == "revocation")
			{
				Assert.True(await setup.Store.RevokeConnectionProfileAsync(Context, Admin, setup.Profile.Id, 2, Token));
			}
			else
			{
				actors.Roles = [];
			}
		};
		var broker = new ProfiledHostingCredentialBroker(setup.Store, credentials, actors, Options.Create(setup.Configuration));
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => broker.IssueGitHubAsync(Context, execution, HostingApproval(execution), Token));
		Assert.Contains("api-credential/token", credentials.Requests, StringComparer.Ordinal);
	}

	[Fact]
	public async Task Hosting_credentials_preserve_caller_cancellation_and_sanitize_backend_errors()
	{
		await using var fixture = await Fixture.OpenAsync();
		var setup = await HostedPushAsync(fixture);
		var execution = await HostingCredentialExecutionAsync(fixture, setup);
		var credentials = HostingCredentials();
		credentials.OnSecret = _ => throw new InvalidOperationException("synthetic-secret-error");
		var broker = HostingBroker(setup, credentials);
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => broker.IssueGitHubAsync(Context, execution, HostingApproval(execution), Token));
		Assert.Equal(SourceControlErrorCode.CredentialUnavailable, error.Code);
		Assert.DoesNotContain("synthetic-secret-error", error.ToString(), StringComparison.Ordinal);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
		credentials.OnSecret = _ => cancellation.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => broker.IssueGitHubAsync(Context, execution, HostingApproval(execution), cancellation.Token));
	}

	private static ProfileCredentials HostingCredentials() => new()
	{
		Metadata = new("api-credential", Context.TenantId, "hosting", "HostingHttpBearer", null,
			["token", "expiresAt"], DateTime.UnixEpoch, DateTime.UnixEpoch, null),
		Values = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["token"] = "synthetic-hosting-token",
			["expiresAt"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O", CultureInfo.InvariantCulture)
		}
	};

	private static ProfiledHostingCredentialBroker HostingBroker(HostedSetup setup, ProfileCredentials credentials) =>
		new(setup.Store, credentials, new CredentialActors { Roles = ["Admin"] }, Options.Create(setup.Configuration));

	private static ApprovedGitHttpsTarget HostingApproval(AcceptedHostingExecution execution) =>
		new(execution.HostingTarget.ApiRepositoryUri, HostingHttpsContext.ApiProfile(execution.Target.Profile), [IPAddress.Parse("8.8.8.8")])
		{ AdministrativePolicy = execution.ApiPolicy };

	private static async Task<AcceptedHostingExecution> HostingCredentialExecutionAsync(Fixture fixture, HostedSetup setup)
	{
		var id = await AcceptHostingAsync(fixture, setup);
		Assert.Equal(1, await setup.Store.TryClaimHostingAsync(Context, id, "credentials", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, Token));
		return (await setup.Store.ReadPullRequestWorkAsync(Context, id, "credentials", 1, Admin, Token)).Hosting!;
	}
}
