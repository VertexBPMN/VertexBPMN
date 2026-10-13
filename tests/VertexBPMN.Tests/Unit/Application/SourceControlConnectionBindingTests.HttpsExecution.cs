using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Accepted_https_read_survives_database_context_restart_but_legacy_worker_cannot_claim_it()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await ReadTargetAsync(fixture);
		var ranges = new[] { "8.8.8.0/24" };
		var configuration = HttpsPreparationOptions(fixture, new("repository-network", Context.TenantId, fixture.Binding.Remote, ranges, []));
		var registry = ExecutionRegistry(() => configuration);
		var id = await AcceptReadAsync(fixture, target, registry);
		ranges[0] = "10.0.0.0/8";
		configuration = HttpsPreparationOptions(fixture, new("repository-network", Context.TenantId, fixture.Binding.Remote, ["8.8.8.0/24"], []));
		await using var restarted = fixture.NewContext();
		var store = fixture.NewStore(restarted);
		var accepted = await store.ReadAcceptedHttpsExecutionAsync(Context, id, Admin, registry, Token);
		Assert.Equal(target, accepted.Target);
		Assert.Equal("8.8.8.0/24", Assert.Single(accepted.Policy.Network.AllowedAddressRanges));
		Assert.Null(accepted.Policy.Trust);
		Assert.Null(await store.TryClaimAsync(Context.TenantId, id, "legacy-worker", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), Token));
		var row = await restarted.SourceControlOperations.SingleAsync(Token);
		Assert.Equal(2, row.ExecutionContractVersion);
		Assert.Equal(0, row.Fence);
		Assert.Null(row.LeaseOwner);
		Assert.DoesNotContain("repository-network", row.ProtectedExecutionTarget, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("request")]
	[InlineData("network")]
	[InlineData("revision")]
	public async Task Idempotency_key_cannot_accept_changed_content_or_administrative_context(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await ReadTargetAsync(fixture);
		var configuration = HttpsPreparationOptions(fixture);
		var registry = ExecutionRegistry(() => configuration);
		var id = await AcceptReadAsync(fixture, target, registry);
		Assert.Equal(id, await AcceptReadAsync(fixture, target, registry));
		if (change != "request")
		{
			var policy = configuration.HttpsNetworkPolicies[0];
			configuration = HttpsPreparationOptions(fixture, change == "revision"
				? policy with { Revision = 2 } : policy with { DeniedAddressRanges = ["8.8.8.0/24"] });
		}
		await RejectAsync(SourceControlErrorCode.IdempotencyConflict, () => AcceptReadAsync(fixture, target, registry,
			change == "request" ? new byte[] { 2 } : null));
		Assert.Equal(1, await fixture.Db.SourceControlOperations.CountAsync(Token));
	}

	[Theory]
	[InlineData("policy", SourceControlErrorCode.RevisionConflict)]
	[InlineData("revision", SourceControlErrorCode.RevisionConflict)]
	[InlineData("revoked-network", SourceControlErrorCode.ProviderUnavailable)]
	[InlineData("missing-network", SourceControlErrorCode.ProviderUnavailable)]
	[InlineData("profile", SourceControlErrorCode.RevisionConflict)]
	[InlineData("revoked-profile", SourceControlErrorCode.ProviderUnavailable)]
	[InlineData("grants", SourceControlErrorCode.NotFound)]
	[InlineData("role", SourceControlErrorCode.NotFound)]
	[InlineData("disabled", SourceControlErrorCode.Disabled)]
	public async Task Recovery_revalidates_current_grants_profile_and_full_administrative_policy(string change, SourceControlErrorCode expected)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await ReadTargetAsync(fixture);
		var configuration = HttpsPreparationOptions(fixture);
		var registry = ExecutionRegistry(() => configuration);
		var id = await AcceptReadAsync(fixture, target, registry);
		configuration = await ChangeAcceptedReadContextAsync(fixture, configuration, change);
		await using var restarted = fixture.NewContext();
		await RejectAsync(expected, () => fixture.NewStore(restarted).ReadAcceptedHttpsExecutionAsync(Context, id,
			change == "role" ? [] : Admin, registry, Token));
	}

	[Theory]
	[InlineData("operation")]
	[InlineData("repository")]
	[InlineData("kind")]
	[InlineData("key")]
	[InlineData("hash")]
	[InlineData("ciphertext")]
	[InlineData("version")]
	[InlineData("request")]
	public async Task Protected_execution_rejects_row_retargeting_and_request_substitution(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await ReadTargetAsync(fixture);
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		var id = await AcceptReadAsync(fixture, target, registry);
		var row = await fixture.Db.SourceControlOperations.SingleAsync(Token);
		switch (change)
		{
			case "operation":
				fixture.Db.SourceControlOperations.Remove(row);
				await fixture.Db.SaveChangesAsync(Token);
				row.Id = Guid.NewGuid();
				id = row.Id;
				fixture.Db.SourceControlOperations.Add(row);
				break;
			case "kind":
				row.Kind = (int)SourceControlOperationKind.Commit;
				break;
			case "repository":
				row.RepositoryId = Guid.NewGuid();
				break;
			case "key":
				row.IdempotencyKey = "substituted-key";
				break;
			case "hash":
				row.RequestHash = new string('f', 64);
				break;
			case "ciphertext":
				row.ProtectedExecutionTarget = "invalid";
				break;
			case "version":
				row.ExecutionContractVersion = 1;
				break;
			case "request":
				var other = await fixture.Store.EnqueueProfiledReadAsync(Context, target, Admin, new("other-read"), new byte[] { 2 }, registry, Token);
				row.ProtectedRequest = (await fixture.Db.SourceControlOperations.SingleAsync(candidate => candidate.Id == other, Token)).ProtectedRequest;
				break;
		}
		await fixture.Db.SaveChangesAsync(Token);
		await using var restarted = fixture.NewContext();
		await RejectAsync(SourceControlErrorCode.ContentUnsafe, () => fixture.NewStore(restarted)
			.ReadAcceptedHttpsExecutionAsync(Context, id, Admin, registry, Token));
	}

	[Theory]
	[InlineData("unchanged")]
	[InlineData("content")]
	[InlineData("revision")]
	[InlineData("revoked")]
	public async Task Accepted_ca_bundle_is_durable_and_cannot_be_replaced_during_recovery(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var profile = fixture.Profile with { Revision = 2, GitEndpoint = fixture.Profile.GitEndpoint with { TrustProfileReference = "repository-ca" } };
		Assert.True(await fixture.Store.ReplaceConnectionProfileAsync(Context, Admin, 1, profile, Token));
		var target = await ReadTargetAsync(fixture);
		using var key = RSA.Create(2048);
		var request = new CertificateRequest("CN=Vertex durable CA test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
		using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
		var trust = new GitHttpsTrustProfile("repository-ca", Context.TenantId, fixture.Binding.Remote, certificate.ExportCertificatePem());
		var configuration = new SourceControlOptions
		{
			Enabled = true,
			HttpsNetworkPolicies = HttpsPreparationOptions(fixture).HttpsNetworkPolicies,
			HttpsTrustProfiles = [trust]
		};
		var registry = ExecutionRegistry(() => configuration);
		var id = await AcceptReadAsync(fixture, target, registry);
		var changed = change switch
		{
			"content" => trust with { CertificateAuthorityPem = trust.CertificateAuthorityPem + "\n" },
			"revision" => trust with { Revision = 2 },
			"revoked" => trust with { IsRevoked = true },
			_ => trust
		};
		configuration = new() { Enabled = true, HttpsNetworkPolicies = configuration.HttpsNetworkPolicies, HttpsTrustProfiles = [changed] };
		await using var restarted = fixture.NewContext();
		var store = fixture.NewStore(restarted);
		if (change == "unchanged")
		{
			var accepted = await store.ReadAcceptedHttpsExecutionAsync(Context, id, Admin, registry, Token);
			Assert.Equal(trust, accepted.Policy.Trust);
		}
		else
		{
			await RejectAsync(change == "revoked" ? SourceControlErrorCode.ProviderUnavailable : SourceControlErrorCode.RevisionConflict,
				() => store.ReadAcceptedHttpsExecutionAsync(Context, id, Admin, registry, Token));
		}
	}

	[Theory]
	[InlineData("tenant-b", "actor-a")]
	[InlineData("tenant-a", "actor-b")]
	public async Task Recovery_does_not_disclose_another_tenant_or_actors_job(string tenant, string actor)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await ReadTargetAsync(fixture);
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		var id = await AcceptReadAsync(fixture, target, registry);
		await RejectAsync(SourceControlErrorCode.NotFound, () => fixture.Store.ReadAcceptedHttpsExecutionAsync(new(tenant, actor), id, Admin, registry, Token));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(3145729)]
	public async Task Read_acceptance_rejects_unbounded_or_empty_requests_without_a_job(int length)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await ReadTargetAsync(fixture);
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		await RejectAsync(SourceControlErrorCode.PayloadTooLarge, () => AcceptReadAsync(fixture, target, registry, new byte[length]));
		Assert.Equal(0, await fixture.Db.SourceControlOperations.CountAsync(Token));
	}

	[Fact]
	public async Task Read_acceptance_cannot_promote_a_commit_target()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await fixture.CaptureAsync();
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		await RejectAsync(SourceControlErrorCode.ContentUnsafe, () => AcceptReadAsync(fixture, target, registry));
		Assert.Equal(0, await fixture.Db.SourceControlOperations.CountAsync(Token));
	}

	[Fact]
	public async Task Cancelled_acceptance_creates_no_job()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await ReadTargetAsync(fixture);
		var registry = ExecutionRegistry(() => HttpsPreparationOptions(fixture));
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
		await cancellation.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Store.EnqueueProfiledReadAsync(
			Context, target, Admin, new("cancelled-read"), new byte[] { 1 }, registry, cancellation.Token));
		Assert.Equal(0, await fixture.Db.SourceControlOperations.CountAsync(Token));
	}

	private static Task<SourceControlExecutionTarget> ReadTargetAsync(Fixture fixture) => fixture.Store.CaptureConnectionTargetAsync(
		Context, fixture.Binding.Id, Admin, SourceControlOperationKind.Read, Token);

	private static Task<Guid> AcceptReadAsync(Fixture fixture, SourceControlExecutionTarget target, GitHttpsPolicyRegistry registry,
		byte[]? request = null) => fixture.Store.EnqueueProfiledReadAsync(Context, target, Admin, new("accepted-profile-read"), request ?? [1], registry, Token);

	private static GitHttpsPolicyRegistry ExecutionRegistry(Func<SourceControlOptions> configuration)
	{
		var monitor = new Mock<IOptionsMonitor<SourceControlOptions>>();
		monitor.SetupGet(value => value.CurrentValue).Returns(configuration);
		return new(monitor.Object);
	}

	private static async Task<SourceControlOptions> ChangeAcceptedReadContextAsync(Fixture fixture, SourceControlOptions configuration, string change)
	{
		var policy = configuration.HttpsNetworkPolicies[0];
		switch (change)
		{
			case "policy":
				return HttpsPreparationOptions(fixture, policy with { AllowedAddressRanges = ["10.0.0.0/8"] });
			case "revision":
				return HttpsPreparationOptions(fixture, policy with { Revision = 2 });
			case "revoked-network":
				return HttpsPreparationOptions(fixture, policy with { IsRevoked = true });
			case "missing-network":
				return new() { Enabled = true };
			case "disabled":
				return new() { Enabled = false };
			case "profile":
				Assert.True(await fixture.Store.ReplaceConnectionProfileAsync(Context, Admin, 1,
					fixture.Profile with { Revision = 2, GitCredentialReference = "rotated-reference" }, Token));
				break;
			case "revoked-profile":
				Assert.True(await fixture.Store.RevokeConnectionProfileAsync(Context, Admin, fixture.Profile.Id, 1, Token));
				break;
			case "grants":
				Assert.True(await fixture.Store.ReplaceGrantsAsync(Context, fixture.Binding.Id, Admin, 2, [], Token));
				break;
		}
		return configuration;
	}
}
