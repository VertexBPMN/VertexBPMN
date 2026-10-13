using System.Net;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Theory]
	[InlineData("unchanged")]
	[InlineData("network")]
	[InlineData("trust")]
	[InlineData("role")]
	[InlineData("expiry")]
	[InlineData("cancel")]
	public async Task Ssh_preparation_rechecks_claim_and_authority_after_DNS_before_credentials(string change)
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var configuration = SshConfigurationOptions(fixture);
		var registry = SshExecutionRegistry(() => configuration);
		var id = await fixture.Store.EnqueueSshReadAsync(Context, await ReadTargetAsync(fixture), Admin, new("ssh-read"), new(ReadRevision), registry, Token);
		Assert.Equal(1, await fixture.Store.TryClaimSshReadAsync(Context, id, "dns", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Admin, registry, Token));
		var actors = new CredentialActors();
		var credentials = SshCredentials();
		var options = Options.Create(configuration);
		var transport = new ProfiledGitSshTransport(fixture.Store, actors, registry,
			new(fixture.Store, credentials, actors, options), new(options), options);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
		async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
		{
			Assert.Equal("git.example", host);
			cancellationToken.ThrowIfCancellationRequested();
			switch (change)
			{
				case "network":
					configuration = new() { Enabled = true, SshNetworkPolicies = [configuration.SshNetworkPolicies[0] with { Revision = 2 }], SshTrustProfiles = configuration.SshTrustProfiles };
					break;
				case "trust":
					configuration = new() { Enabled = true, SshNetworkPolicies = configuration.SshNetworkPolicies, SshTrustProfiles = [configuration.SshTrustProfiles[0] with { Revision = 2 }] };
					break;
				case "role":
					actors.Roles = [];
					break;
				case "expiry":
					await ExpireReadAsync(fixture.Db, id);
					break;
				case "cancel":
					await cancellation.CancelAsync();
					break;
			}
			return [IPAddress.Parse("8.8.8.8")];
		}
		if (change == "unchanged")
		{
			var prepared = await transport.PrepareClaimedReadAsync(Context, id, "dns", 1, ResolveAsync, cancellation.Token);
			Assert.Equal(ReadRevision, prepared.Work.Request.Revision);
			Assert.Equal("8.8.8.8", Assert.Single(prepared.Approved.CopyAddresses()).ToString());
		}
		else if (change == "cancel")
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.PrepareClaimedReadAsync(Context, id, "dns", 1, ResolveAsync, cancellation.Token));
		}
		else
		{
			await Assert.ThrowsAsync<SourceControlSecurityException>(() => transport.PrepareClaimedReadAsync(Context, id, "dns", 1, ResolveAsync, cancellation.Token));
		}
		Assert.Equal(0, credentials.MetadataCalls);
		Assert.Empty(credentials.Requests);
	}

	[Theory]
	[InlineData(GitReadQueryKind.Branches)]
	[InlineData(GitReadQueryKind.ResolveBranch)]
	public async Task Ssh_remote_queries_are_durable_typed_requests(GitReadQueryKind kind)
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var registry = SshExecutionRegistry(() => SshConfigurationOptions(fixture));
		var query = new AcceptedGitReadQuery(kind) { Path = kind == GitReadQueryKind.ResolveBranch ? "main" : null, PageSize = kind == GitReadQueryKind.Branches ? 10 : 0 };
		var id = await fixture.Store.EnqueueSshReadAsync(Context, await ReadTargetAsync(fixture), Admin, new("ssh-query"), new(null) { Query = query }, registry, Token);
		await using var restarted = fixture.NewContext();
		var work = await fixture.NewStore(restarted).ReadAcceptedSshReadAsync(Context, id, Admin, registry, Token);
		Assert.Null(work.Request.Revision);
		Assert.Equal(query, work.Request.Query);
	}
}
