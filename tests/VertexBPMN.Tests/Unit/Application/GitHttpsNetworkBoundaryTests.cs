using System.Net;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class GitHttpsNetworkBoundaryTests
{
	private static readonly SourceControlContext Actor = new("tenant-a", "actor-a");
	private static readonly RepositoryConnectionProfile Profile = new(Guid.NewGuid(), Actor.TenantId, 1,
		GitTransportKind.Https, GitAuthenticationKind.HttpBasic,
		new(new Uri("https://git.example.com:8443/team"), "private-git"), "credential-reference", SourceControlCapability.Read);
	private static readonly RepositoryBinding Binding = new(Guid.NewGuid(), Actor.TenantId,
		new Uri("https://git.example.com:8443/team/models.git"), null, "master", "release", ["models"])
	{
		ConnectionProfileId = Profile.Id
	};
	private static readonly GitHttpsNetworkPolicy Policy = new("private-git", Actor.TenantId,
		new Uri("https://git.example.com:8443/team"), [], []);

	[Fact]
	public async Task Profiled_fetch_rejects_missing_credentials_unsupported_authentication_and_unresolved_trust()
	{
		var runner = new ControlledGitProcess(Options.Create(new SourceControlOptions { GitExecutablePath = "not-a-git-executable" }));
		var workspace = new GitWorkspace("unused", "unused", Guid.NewGuid(), 1);
		var revision = new GitCommitId(new string('a', 40));
		Assert.Equal(SourceControlErrorCode.CredentialUnavailable, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
			runner.FetchProfileRevisionAsync(workspace, Actor, Binding, Profile, Policy, revision, null,
				TestContext.Current.CancellationToken))).Code);
		foreach (var profile in new[]
		{
			Profile with { Authentication = GitAuthenticationKind.HttpBearer },
			Profile with { Authentication = GitAuthenticationKind.GitHubApp },
			Profile with { GitEndpoint = Profile.GitEndpoint with { TrustProfileReference = "not-resolved" } }
		})
		{
			Assert.Equal(SourceControlErrorCode.ProviderUnavailable, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
				runner.FetchProfileRevisionAsync(workspace, Actor, Binding, profile, Policy, revision, null,
					TestContext.Current.CancellationToken))).Code);
		}
	}

	[Theory]
	[InlineData("10.20.30.40", "10.20.0.0/16")]
	[InlineData("172.20.1.2", "172.20.0.0/16")]
	[InlineData("192.168.1.2", "192.168.0.0/16")]
	[InlineData("fd12:3456::1", "fd12:3456::/48")]
	[InlineData("::ffff:10.20.30.40", "10.20.0.0/16")]
	public async Task Private_addresses_require_explicit_matching_ranges(string address, string range)
	{
		Assert.Equal(SourceControlErrorCode.InvalidInput, (await Assert.ThrowsAsync<SourceControlSecurityException>(
			() => ResolveAsync(Policy, address))).Code);
		var approved = await ResolveAsync(Policy with { AllowedAddressRanges = [range] }, address);
		Assert.Equal(Binding.Remote, approved.Remote);
		Assert.Equal(IPAddress.Parse(address), Assert.Single(approved.CopyAddresses()));
	}

	[Theory]
	[InlineData("127.0.0.1")]
	[InlineData("169.254.169.254")]
	[InlineData("168.63.129.16")]
	[InlineData("100.100.100.200")]
	[InlineData("0.0.0.0")]
	[InlineData("224.0.0.1")]
	[InlineData("192.0.2.1")]
	[InlineData("::1")]
	[InlineData("fe80::1")]
	[InlineData("fd00:ec2::254")]
	[InlineData("fd20:ce::254")]
	[InlineData("2001:db8::1")]
	[InlineData("::ffff:127.0.0.1")]
	[InlineData("::ffff:10.20.30.40%2")]
	public async Task Broad_allow_ranges_cannot_approve_infrastructure_or_reserved_addresses(string address)
	{
		var policy = Policy with { AllowedAddressRanges = ["0.0.0.0/0", "::/0"] };
		Assert.Equal(SourceControlErrorCode.InvalidInput, (await Assert.ThrowsAsync<SourceControlSecurityException>(
			() => ResolveAsync(policy, address))).Code);
	}

	[Fact]
	public async Task Denials_override_allow_ranges_and_every_dns_answer_must_be_safe()
	{
		var policy = Policy with { AllowedAddressRanges = ["10.0.0.0/8"], DeniedAddressRanges = ["10.20.0.0/16"] };
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => ResolveAsync(policy, "10.20.30.40"));
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => ResolveAsync(Policy, "8.8.8.8", "10.20.30.40"));
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => ResolveAsync(Policy));
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => ResolveAsync(Policy,
			Enumerable.Repeat("8.8.8.8", 33).ToArray()));
	}

	[Fact]
	public async Task Approval_copies_dns_answers_and_does_not_expose_mutable_addresses()
	{
		var addresses = new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("8.8.8.8") };
		var calls = 0;
		var approved = await GitHttpsNetworkBoundary.ResolveAsync(Actor, Binding, Profile, Policy, (host, token) =>
		{
			Assert.Equal("git.example.com", host);
			Assert.Equal(TestContext.Current.CancellationToken, token);
			calls++;
			return Task.FromResult(addresses);
		}, TestContext.Current.CancellationToken);
		addresses[0] = IPAddress.Loopback;
		var first = approved.CopyAddresses();
		first[0] = IPAddress.Loopback;
		Assert.Equal(IPAddress.Parse("8.8.8.8"), Assert.Single(approved.CopyAddresses()));
		Assert.Equal(1, calls);
	}

	[Theory]
	[InlineData("https://other.example.com:8443/team")]
	[InlineData("https://git.example.com/team")]
	[InlineData("https://git.example.com:8443/team/other")]
	public async Task Network_scope_is_checked_before_dns(string scope)
	{
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => GitHttpsNetworkBoundary.ResolveAsync(Actor,
			Binding, Profile, Policy with { RepositoryScope = new Uri(scope) },
			(_, _) => throw new InvalidOperationException("DNS must not run."), TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task Foreign_tenants_references_and_invalid_ranges_are_rejected_before_dns()
	{
		foreach (var policy in new[]
		{
			Policy with { TenantId = "tenant-b" }, Policy with { Id = "other-policy" },
			Policy with { AllowedAddressRanges = ["invalid"] }, Policy with { DeniedAddressRanges = [" 10.0.0.0/8"] },
			Policy with { AllowedAddressRanges = Enumerable.Repeat("10.0.0.0/8", 65).ToArray() }
		})
		{
			await Assert.ThrowsAsync<SourceControlSecurityException>(() => GitHttpsNetworkBoundary.ResolveAsync(Actor,
				Binding, Profile, policy, (_, _) => throw new InvalidOperationException("DNS must not run."),
				TestContext.Current.CancellationToken));
		}
	}

	[Fact]
	public async Task Cancellation_is_propagated_before_dns_and_after_resolution()
	{
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitHttpsNetworkBoundary.ResolveAsync(Actor,
			Binding, Profile, Policy, (_, _) => throw new InvalidOperationException("DNS must not run."), cancellation.Token));
		using var duringResolution = new CancellationTokenSource();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitHttpsNetworkBoundary.ResolveAsync(Actor,
			Binding, Profile, Policy, async (_, token) =>
			{
				Assert.Equal(duringResolution.Token, token);
				await duringResolution.CancelAsync();
				return [IPAddress.Parse("8.8.8.8")];
			}, duringResolution.Token));
	}

	private static Task<ApprovedGitHttpsTarget> ResolveAsync(GitHttpsNetworkPolicy policy, params string[] addresses)
		=> GitHttpsNetworkBoundary.ResolveAsync(Actor, Binding, Profile, policy,
			(_, _) => Task.FromResult(addresses.Select(IPAddress.Parse).ToArray()), TestContext.Current.CancellationToken);
}
