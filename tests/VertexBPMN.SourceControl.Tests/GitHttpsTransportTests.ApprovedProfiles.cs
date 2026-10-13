using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed partial class GitHttpsTransportTests
{
	[Theory]
	[InlineData("service-account")]
	[InlineData(null)]
	public async Task Approved_profile_branches_and_history_use_the_same_peer_and_trust_snapshot(string? username)
	{
		await using var fixture = await HttpsFixture.CreateAsync(username: username, hostname: "git.vertex.invalid");
		var (binding, approved) = await ApproveFixtureAsync(fixture, username);
		using var lease = username is null ? null : new GitHttpBasicCredentialLease(binding.Remote, username, fixture.Token,
			DateTimeOffset.UtcNow.AddMinutes(2));
		var token = TestContext.Current.CancellationToken;
		var branches = await fixture.Runner.ListApprovedProfileBranchesAsync(fixture.Workspace, new("tenant-test", "actor-test"),
			binding, approved, lease, 100, null, token);
		var branch = Encoding.UTF8.GetString(await fixture.GitAsync(["--git-dir=" + Path.Combine(fixture.Root, "remote", "models.git"),
			"symbolic-ref", "--short", "HEAD"])).Trim();
		Assert.Contains(branch, branches.Items, StringComparer.Ordinal);
		var selected = await fixture.Runner.ResolveApprovedProfileBranchAsync(fixture.Workspace, new("tenant-test", "actor-test"),
			binding, approved, lease, branch, token);
		Assert.Equal(await FixtureRevisionAsync(fixture), selected.Commit);
		await fixture.Runner.FetchApprovedProfileHistoryAsync(fixture.Workspace, new("tenant-test", "actor-test"), binding,
			approved, selected.Commit, lease, token);
		var history = await fixture.Runner.ReadModelHistoryAsync(fixture.Workspace, binding,
			new("models/example.bpmn", selected.Commit, 100, null), token);
		Assert.Contains(history.Items, item => item.Commit == selected.Commit);
		Assert.Empty(Directory.EnumerateFiles(fixture.Workspace.ControlDirectory, "ca-*.pem"));
	}

	[Theory]
	[InlineData("service-account")]
	[InlineData(null)]
	public async Task Approved_profile_fetch_uses_pinned_peer_and_ca_snapshot_with_exact_bytes(string? username)
	{
		await using var fixture = await HttpsFixture.CreateAsync(username: username, hostname: "git.vertex.invalid");
		var (binding, approved) = await ApproveFixtureAsync(fixture, username);
		using var lease = username is null ? null : new GitHttpBasicCredentialLease(binding.Remote, username, fixture.Token,
			DateTimeOffset.UtcNow.AddMinutes(2));
		var revision = await FixtureRevisionAsync(fixture);
		await fixture.Runner.FetchApprovedProfileRevisionAsync(fixture.Workspace, new("tenant-test", "actor-test"), binding,
			approved, revision, lease, TestContext.Current.CancellationToken);
		Assert.Equal(fixture.ModelBytes, await fixture.GitAsync(["--git-dir=" + Path.Combine(fixture.Workspace.Directory, "repository.git"),
			"show", "refs/heads/vertex-source:models/example.bpmn"]));
		Assert.Empty(Directory.EnumerateFiles(fixture.Workspace.ControlDirectory, "ca-*.pem"));
		Assert.True(username is null ? fixture.AnonymousRequests > 0 : fixture.AuthenticatedRequests > 0);
		foreach (var file in Directory.EnumerateFiles(fixture.Workspace.Directory, "*", SearchOption.AllDirectories))
		{
			Assert.DoesNotContain(fixture.Token, Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file,
				TestContext.Current.CancellationToken)), StringComparison.Ordinal);
		}
	}

	[Theory]
	[InlineData("ca")]
	[InlineData("hostname")]
	public async Task Approved_profile_tls_rejection_sends_no_credentials_and_cleans_ca_snapshot(string invalid)
	{
		await using var fixture = await HttpsFixture.CreateAsync(username: "service-account", hostname: "git.vertex.invalid");
		var (binding, approved) = await ApproveFixtureAsync(fixture, "service-account");
		if (invalid == "ca")
		{
			using var key = RSA.Create(2048);
			var request = new CertificateRequest("CN=untrusted isolated CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
			using var otherCa = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
			approved = new(approved.Remote, approved.Profile, approved.CopyAddresses())
			{
				AdministrativePolicy = approved.AdministrativePolicy! with
				{
					Trust = approved.AdministrativePolicy.Trust! with { CertificateAuthorityPem = otherCa.ExportCertificatePem() }
				}
			};
		}
		else
		{
			var remote = new UriBuilder(binding.Remote) { Host = "wrong.vertex.invalid" }.Uri;
			var profile = approved.Profile with { GitEndpoint = approved.Profile.GitEndpoint with { BaseUri = remote } };
			binding = binding with { Remote = remote };
			approved = new(remote, profile, approved.CopyAddresses())
			{
				AdministrativePolicy = new(approved.AdministrativePolicy!.Network with { RepositoryScope = remote },
					approved.AdministrativePolicy.Trust! with { RepositoryScope = remote })
			};
		}
		using var lease = new GitHttpBasicCredentialLease(binding.Remote, "service-account", fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
		var revision = await FixtureRevisionAsync(fixture);
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Runner.FetchApprovedProfileRevisionAsync(fixture.Workspace,
			new("tenant-test", "actor-test"), binding, approved, revision, lease, TestContext.Current.CancellationToken));
		Assert.Equal(SourceControlErrorCode.ProviderUnavailable, error.Code);
		Assert.Equal(0, fixture.AuthenticatedRequests);
		Assert.Equal(0, fixture.Challenges);
		Assert.Empty(Directory.EnumerateFiles(fixture.Workspace.ControlDirectory, "ca-*.pem"));
	}

	[Fact]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2025", Justification = "The fetch is canceled and awaited in finally before lease/token disposal, including assertion-failure paths; the analyzer does not model this finally flow.")]
	public async Task Cancelled_approved_profile_fetch_removes_its_operation_ca_snapshot()
	{
		await using var fixture = await HttpsFixture.CreateAsync(stall: true, username: "service-account", hostname: "git.vertex.invalid");
		var (binding, approved) = await ApproveFixtureAsync(fixture, "service-account");
		using var lease = new GitHttpBasicCredentialLease(binding.Remote, "service-account", fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		var revision = await FixtureRevisionAsync(fixture);
		var fetch = fixture.Runner.FetchApprovedProfileRevisionAsync(fixture.Workspace, new("tenant-test", "actor-test"), binding,
			approved, revision, lease, cancellation.Token);
		try
		{
			await fixture.RequestStalled.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
			Assert.Single(Directory.EnumerateFiles(fixture.Workspace.ControlDirectory, "ca-*.pem"));
		}
		finally
		{
			await cancellation.CancelAsync();
			try
			{
				await fetch;
			}
			catch (OperationCanceledException)
			{
				// Expected after the caller cancels; the completed task is asserted below.
			}
		}
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetch);
		await fixture.RequestClosed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
		Assert.Empty(Directory.EnumerateFiles(fixture.Workspace.ControlDirectory, "ca-*.pem"));
	}

	private static async Task<GitCommitId> FixtureRevisionAsync(HttpsFixture fixture) => new(Encoding.UTF8.GetString(
		await fixture.GitAsync(["--git-dir=" + Path.Combine(fixture.Root, "remote", "models.git"), "rev-parse", "HEAD"])).Trim());

	private static async Task<(RepositoryBinding Binding, ApprovedGitHttpsTarget Approved)> ApproveFixtureAsync(HttpsFixture fixture, string? username)
	{
		var network = new GitHttpsNetworkPolicy("fixture-network", "tenant-test", fixture.Remote, [], []);
		var trust = new GitHttpsTrustProfile("fixture-ca", network.TenantId, fixture.Remote,
			await File.ReadAllTextAsync(fixture.CaFile, TestContext.Current.CancellationToken));
		var profile = new RepositoryConnectionProfile(Guid.NewGuid(), network.TenantId, 1, GitTransportKind.Https,
			username is null ? GitAuthenticationKind.Anonymous : GitAuthenticationKind.HttpBasic,
			new(fixture.Remote, network.Id, trust.Id), username is null ? null : "test-reference", SourceControlCapability.Read);
		var binding = new RepositoryBinding(Guid.NewGuid(), network.TenantId, fixture.Remote, null, "master", "release", ["models"])
		{
			ConnectionProfileId = profile.Id
		};
		// Isolated peer injection is test-only: the production network boundary rejects loopback.
		return (binding, new(fixture.Remote, profile, [IPAddress.Loopback]) { AdministrativePolicy = new(network, trust) });
	}
}
