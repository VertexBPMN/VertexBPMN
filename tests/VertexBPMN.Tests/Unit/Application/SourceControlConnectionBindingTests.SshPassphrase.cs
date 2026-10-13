using System.Globalization;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Ssh_passphrase_is_separately_resolved_and_disposal_rejects_further_output()
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var credentials = PassphraseCredentials("isolated-test-passphrase");
		var approved = await ApproveSshAsync(fixture);
		using var lease = await SshBroker(fixture, credentials, new(), approved).IssueAsync(Context, approved, Token);
		Assert.True(lease.HasPassphrase);
		Assert.Equal(["new-reference/username", "new-reference/privateKey", "new-reference/passphrase"], credentials.Requests);
		await using var writer = new StringWriter(CultureInfo.InvariantCulture);
		await lease.WritePassphraseAsync(writer, Token);
		Assert.Equal("isolated-test-passphrase" + Environment.NewLine, writer.ToString());
		lease.Dispose();
		await RejectAsync(SourceControlErrorCode.CredentialUnavailable, () => lease.WritePassphraseAsync(writer, Token));
		Assert.DoesNotContain("isolated-test-passphrase", lease.ToString(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("missing")]
	[InlineData("rotation")]
	[InlineData("role")]
	public async Task Ssh_passphrase_resolution_rechecks_metadata_and_current_authority(string change)
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var credentials = PassphraseCredentials(change == "missing" ? "" : "isolated-test-passphrase");
		var actors = new CredentialActors();
		credentials.OnSecret = key =>
		{
			if (key == "passphrase")
			{
				if (change == "rotation")
				{
					credentials.Metadata = credentials.Metadata with { LastModified = DateTime.UtcNow };
				}
				if (change == "role")
				{
					actors.Roles = [];
				}
			}
			return Task.CompletedTask;
		};
		var approved = await ApproveSshAsync(fixture);
		await RejectAsync(change == "role" ? SourceControlErrorCode.NotFound : SourceControlErrorCode.CredentialUnavailable,
			() => SshBroker(fixture, credentials, actors, approved).IssueAsync(Context, approved, Token));
	}
	private static ProfileCredentials PassphraseCredentials(string value) => new()
	{
		Metadata = new("new-reference", Context.TenantId, "test SSH credential", "GitSshKey", null,
			["username", "privateKey", "passphrase"], DateTime.UnixEpoch, DateTime.UnixEpoch, null),
		Values = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["username"] = "git",
			["privateKey"] = SshSyntheticKey,
			["passphrase"] = value
		}
	};

}
