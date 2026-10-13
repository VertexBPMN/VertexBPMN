using System.Net;
using Microsoft.Extensions.Options;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	private const string SshSyntheticKey = "-----BEGIN OPENSSH PRIVATE KEY-----\nAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n-----END OPENSSH PRIVATE KEY-----\n";

	[Fact]
	public async Task Ssh_credentials_use_current_profile_and_separate_username_and_private_key()
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var credentials = SshCredentials();
		var actors = new CredentialActors();
		var approved = await ApproveSshAsync(fixture);
		using var lease = await SshBroker(fixture, credentials, actors, approved).IssueAsync(Context, approved, Token);
		Assert.Equal("git", lease.Username);
		Assert.Equal(["new-reference/username", "new-reference/privateKey"], credentials.Requests);
		Assert.Equal(3, actors.Calls);
		Assert.DoesNotContain("AAAAAAAA", lease.ToString(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("rotation", SourceControlErrorCode.CredentialUnavailable)]
	[InlineData("role", SourceControlErrorCode.Forbidden)]
	[InlineData("profile", SourceControlErrorCode.ProviderUnavailable)]
	[InlineData("trust", SourceControlErrorCode.RevisionConflict)]
	public async Task Ssh_changes_during_key_resolution_cannot_mint_a_lease(string change, SourceControlErrorCode expected)
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var credentials = SshCredentials();
		var actors = new CredentialActors();
		var approved = await ApproveSshAsync(fixture);
		var trustProfiles = new[] { approved.Trust };
		var configuration = new SourceControlOptions
		{ Enabled = true, SshNetworkPolicies = [approved.Network], SshTrustProfiles = trustProfiles };
		credentials.OnSecret = async key =>
		{
			if (key != "username")
			{
				return;
			}
			switch (change)
			{
				case "rotation":
					credentials.Metadata = credentials.Metadata with { LastModified = DateTime.UtcNow };
					break;
				case "role":
					actors.Roles = ["ReadOnly"];
					break;
				case "profile":
					Assert.True(await fixture.Store.RevokeConnectionProfileAsync(Context, Admin, fixture.Profile.Id, 1, Token));
					break;
				default:
					trustProfiles[0] = approved.Trust with { Revision = 2 };
					break;
			}
		};
		var broker = new ProfiledSshCredentialBroker(fixture.Store, credentials, actors, Options.Create(configuration));
		await RejectAsync(expected, () => broker.IssueAsync(Context, approved, Token));
		if (change != "rotation")
		{
			Assert.Equal(["new-reference/username"], credentials.Requests);
		}
	}

	[Theory]
	[InlineData("tenant")]
	[InlineData("type")]
	[InlineData("keys")]
	public async Task Ssh_credential_metadata_mismatch_releases_no_secret(string mismatch)
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var credentials = SshCredentials();
		credentials.Metadata = mismatch switch
		{
			"tenant" => credentials.Metadata with { TenantId = "foreign" },
			"type" => credentials.Metadata with { Type = "GitHttpBasic" },
			_ => credentials.Metadata with { SecretKeys = ["username"] }
		};
		var approved = await ApproveSshAsync(fixture);
		await RejectAsync(SourceControlErrorCode.CredentialUnavailable,
			() => SshBroker(fixture, credentials, new(), approved).IssueAsync(Context, approved, Token));
		Assert.Empty(credentials.Requests);
	}

	private static ProfileCredentials SshCredentials() => new()
	{
		Metadata = new("new-reference", Context.TenantId, "test SSH credential", "GitSshKey", null,
			["username", "privateKey"], DateTime.UnixEpoch, DateTime.UnixEpoch, null),
		Values = new Dictionary<string, string>(StringComparer.Ordinal) { ["username"] = "git", ["privateKey"] = SshSyntheticKey }
	};

	private static SourceControlOptions SshConfiguration(ApprovedGitSshTarget approved) => new()
	{ Enabled = true, SshNetworkPolicies = [approved.Network], SshTrustProfiles = [approved.Trust] };

	private static ProfiledSshCredentialBroker SshBroker(Fixture fixture, ProfileCredentials credentials,
		CredentialActors actors, ApprovedGitSshTarget approved) => new(fixture.Store, credentials, actors, Options.Create(SshConfiguration(approved)));

	private static async Task<ApprovedGitSshTarget> ApproveSshAsync(Fixture fixture)
	{
		var binding = (await fixture.Store.FindAsync(Context.TenantId, fixture.Binding.Id, Token))!.Binding;
		var target = await fixture.CaptureAsync();
		return await GitSshNetworkBoundary.ResolveAsync(Context, binding, target,
			new("repository-network", Context.TenantId, binding.Remote, [], []),
			new("repository-trust", Context.TenantId, binding.Remote, ["ssh-ed25519 YWFhYWFhYWFhYWFhYWFhYQ=="]),
			(_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }), Token);
	}
}
