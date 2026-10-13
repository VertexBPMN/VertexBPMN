using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Profile_credentials_use_the_profile_reference_and_exact_approved_repository()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await fixture.CaptureAsync();
		var credentials = new ProfileCredentials();
		var actors = new CredentialActors();
		using var lease = await Broker(fixture, credentials, actors).IssueAsync(Context, target, await ApproveAsync(fixture), Token);
		Assert.NotNull(lease);
		Assert.Equal(["new-reference/username", "new-reference/password"], credentials.Requests);
		Assert.Equal(3, actors.Calls);
		await using var bytes = new MemoryStream();
		await using var writer = new StreamWriter(bytes, new UTF8Encoding(false), leaveOpen: true);
		await lease.WriteCredentialAsync(target.Remote, writer, Token);
		await writer.FlushAsync(Token);
		Assert.Equal($"service-account{Environment.NewLine}synthetic-test-password{Environment.NewLine}", Encoding.UTF8.GetString(bytes.ToArray()));
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => lease.WriteCredentialAsync(
			new Uri("https://github.com/team/other.git"), writer, Token));
	}

	[Theory]
	[InlineData("tenant")]
	[InlineData("type")]
	[InlineData("id")]
	[InlineData("keys")]
	public async Task Unmatched_credential_metadata_releases_no_secret(string mismatch)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await fixture.CaptureAsync();
		var credentials = new ProfileCredentials();
		credentials.Metadata = mismatch switch
		{
			"tenant" => credentials.Metadata with { TenantId = "foreign-tenant" },
			"type" => credentials.Metadata with { Type = "GitHubApp" },
			"id" => credentials.Metadata with { Id = "legacy-reference" },
			_ => credentials.Metadata with { SecretKeys = ["username"] }
		};
		var approved = await ApproveAsync(fixture);
		await RejectAsync(SourceControlErrorCode.CredentialUnavailable, () => Broker(fixture, credentials, new()).IssueAsync(
			Context, target, approved, Token));
		Assert.Empty(credentials.Requests);
	}

	[Theory]
	[InlineData("profile", SourceControlErrorCode.ProviderUnavailable)]
	[InlineData("revision", SourceControlErrorCode.RevisionConflict)]
	[InlineData("grants", SourceControlErrorCode.NotFound)]
	[InlineData("role", SourceControlErrorCode.Forbidden)]
	[InlineData("rotation", SourceControlErrorCode.CredentialUnavailable)]
	[InlineData("delete", SourceControlErrorCode.CredentialUnavailable)]
	public async Task Changes_during_credential_resolution_cannot_mint_a_lease(string change, SourceControlErrorCode expected)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await fixture.CaptureAsync();
		var approved = await ApproveAsync(fixture);
		var actors = new CredentialActors();
		var credentials = new ProfileCredentials
		{
			OnSecret = async key =>
			{
				if (key != "username")
				{
					return;
				}
				switch (change)
				{
					case "profile":
						Assert.True(await fixture.Store.RevokeConnectionProfileAsync(Context, Admin, fixture.Profile.Id, 1, Token));
						break;
					case "revision":
						Assert.True(await fixture.Store.ReplaceConnectionProfileAsync(Context, Admin, 1,
							fixture.Profile with { Revision = 2, GitCredentialReference = "changed-reference" }, Token));
						break;
					case "grants":
						Assert.True(await fixture.Store.ReplaceGrantsAsync(Context, fixture.Binding.Id, Admin, 2, [], Token));
						break;
					case "role":
						actors.Roles = ["ReadOnly"];
						break;
				}
			}
		};
		if (change == "rotation")
		{
			credentials.OnSecret = _ => { credentials.Metadata = credentials.Metadata with { LastModified = DateTime.UtcNow }; return Task.CompletedTask; };
		}
		else if (change == "delete")
		{
			credentials.OnSecret = _ => { credentials.Deleted = true; return Task.CompletedTask; };
		}
		await RejectAsync(expected, () => Broker(fixture, credentials, actors).IssueAsync(Context, target, approved, Token));
	}

	[Fact]
	public async Task Anonymous_read_authorizes_current_grants_without_credential_lookup()
	{
		await using var fixture = await Fixture.OpenAsync();
		var profile = fixture.Profile with
		{
			Revision = 2,
			Authentication = GitAuthenticationKind.Anonymous,
			GitCredentialReference = null,
			GitCapabilities = SourceControlCapability.Read
		};
		Assert.True(await fixture.Store.ReplaceConnectionProfileAsync(Context, Admin, 1, profile, Token));
		Assert.True(await fixture.Store.AssignConnectionProfileAsync(Context, Admin, fixture.Binding.Id, 1, profile.Id, 2, Token));
		var target = await fixture.Store.CaptureConnectionTargetAsync(Context, fixture.Binding.Id, Admin, SourceControlOperationKind.Read, Token);
		var credentials = new ProfileCredentials();
		var lease = await Broker(fixture, credentials, new()).IssueAsync(Context, target,
			new(target.Remote, target.Profile, [IPAddress.Parse("8.8.8.8")]), Token);
		Assert.Null(lease);
		Assert.Equal(0, credentials.MetadataCalls);
		Assert.Empty(credentials.Requests);
	}

	[Fact]
	public async Task Disabled_or_wrong_approved_target_cannot_release_credentials()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await fixture.CaptureAsync();
		var credentials = new ProfileCredentials();
		await RejectAsync(SourceControlErrorCode.Disabled, () => new ProfiledGitCredentialBroker(fixture.Store, credentials,
			new CredentialActors(), Options.Create(new SourceControlOptions())).IssueAsync(Context, target,
			new(target.Remote, target.Profile, [IPAddress.Parse("8.8.8.8")]), Token));
		await RejectAsync(SourceControlErrorCode.InvalidInput, () => Broker(fixture, credentials, new()).IssueAsync(Context,
			target, new(new Uri("https://github.com/team/other.git"), target.Profile, [IPAddress.Parse("8.8.8.8")]), Token));
		await RejectAsync(SourceControlErrorCode.InvalidInput, () => Broker(fixture, credentials, new()).IssueAsync(Context,
			target, new(target.Remote, target.Profile with { Revision = 2 }, [IPAddress.Parse("8.8.8.8")]), Token));
		Assert.Equal(0, credentials.MetadataCalls);
		Assert.Empty(credentials.Requests);
	}

	[Fact]
	public async Task Protected_credential_service_rotation_is_observed_by_the_next_issuance()
	{
		await using var fixture = await Fixture.OpenAsync();
		var credentials = new PersistentCredentialService(fixture.Db, new EphemeralDataProtectionProvider(),
			Mock.Of<IAuditLogService>(), NullLogger<PersistentCredentialService>.Instance);
		var metadata = await credentials.CreateAsync(Context.TenantId, new("test basic", "GitHttpBasic", null,
			new Dictionary<string, string>(StringComparer.Ordinal) { ["username"] = "service-account", ["password"] = "test-first-password" }), Token);
		var profile = fixture.Profile with { Revision = 2, GitCredentialReference = metadata.Id };
		Assert.True(await fixture.Store.ReplaceConnectionProfileAsync(Context, Admin, 1, profile, Token));
		Assert.True(await fixture.Store.AssignConnectionProfileAsync(Context, Admin, fixture.Binding.Id, 1, profile.Id, 2, Token));
		var target = await fixture.CaptureAsync();
		var approved = await ApproveAsync(fixture);
		var broker = Broker(fixture, credentials, new());
		using var first = await broker.IssueAsync(Context, target, approved, Token);
		Assert.NotNull(first);
		Assert.Equal($"service-account{Environment.NewLine}test-first-password{Environment.NewLine}", await ReadLeaseAsync(first, target.Remote));
		Assert.True(await credentials.RotateSecretAsync(Context.TenantId, metadata.Id, new("password", "test-rotated-password"), Token));
		using var second = await broker.IssueAsync(Context, target, approved, Token);
		Assert.NotNull(second);
		Assert.Equal($"service-account{Environment.NewLine}test-rotated-password{Environment.NewLine}", await ReadLeaseAsync(second, target.Remote));
		var persisted = await fixture.Db.Credentials.FindAsync([metadata.Id], Token);
		Assert.NotNull(persisted);
		Assert.DoesNotContain("test-rotated-password", persisted.ProtectedValues, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Backend_failure_is_sanitized_and_caller_cancellation_is_preserved()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await fixture.CaptureAsync();
		var approved = await ApproveAsync(fixture);
		var credentials = new ProfileCredentials { OnSecret = _ => throw new InvalidOperationException("synthetic-secret-error") };
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => Broker(fixture, credentials, new()).IssueAsync(Context, target, approved, Token));
		Assert.Equal(SourceControlErrorCode.CredentialUnavailable, error.Code);
		Assert.DoesNotContain("synthetic-secret-error", error.ToString(), StringComparison.Ordinal);
		using var cancellation = new CancellationTokenSource();
		credentials.OnSecret = _ => cancellation.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Broker(fixture, credentials, new()).IssueAsync(Context,
			target, approved, cancellation.Token));
	}

	private static async Task<string> ReadLeaseAsync(IGitCredentialLease lease, Uri remote)
	{
		await using var bytes = new MemoryStream();
		await using var writer = new StreamWriter(bytes, new UTF8Encoding(false), leaveOpen: true);
		await lease.WriteCredentialAsync(remote, writer, Token);
		await writer.FlushAsync(Token);
		return Encoding.UTF8.GetString(bytes.ToArray());
	}

	private static CancellationToken Token => TestContext.Current.CancellationToken;
	private static ProfiledGitCredentialBroker Broker(Fixture fixture, ICredentialService credentials, CredentialActors actors)
		=> new(fixture.Store, credentials, actors, Options.Create(new SourceControlOptions { Enabled = true }));
	private static async Task<ApprovedGitHttpsTarget> ApproveAsync(Fixture fixture)
	{
		var binding = (await fixture.Store.FindAsync(Context.TenantId, fixture.Binding.Id, Token))!.Binding;
		var profile = (await fixture.CaptureAsync()).Profile;
		var approved = await GitHttpsNetworkBoundary.ResolveAsync(Context, binding, profile,
			new("repository-network", Context.TenantId, binding.Remote, [], []),
			(_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }), Token);
		return new(approved.Remote, approved.Profile, approved.CopyAddresses()) { RequiredCapabilities = SourceControlCapability.Commit };
	}

	private sealed class CredentialActors : ISourceControlActorResolver
	{
		public string[] Roles { get; set; } = ["ProcessManager"];
		public int Calls { get; private set; }
		public Task<IReadOnlyCollection<string>> ResolveAsync(SourceControlContext context, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Calls++;
			return Task.FromResult<IReadOnlyCollection<string>>(Roles);
		}
	}

	private sealed class ProfileCredentials : ICredentialService
	{
		public CredentialMetadata Metadata { get; set; } = new("new-reference", Context.TenantId, "test credential", "GitHttpBasic",
			null, ["username", "password"], DateTime.UnixEpoch, DateTime.UnixEpoch, null);
		public bool Deleted { get; set; }
		public int MetadataCalls { get; private set; }
		public List<string> Requests { get; } = [];
		public Func<string, Task>? OnSecret { get; set; }
		public IReadOnlyDictionary<string, string>? Values { get; init; }
		public Task<CredentialMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			MetadataCalls++;
			return Task.FromResult(Deleted ? null : Metadata);
		}
		public async Task<string?> ResolveSecretAsync(string tenantId, string id, string key, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Requests.Add(id + "/" + key);
			if (OnSecret is not null)
			{
				await OnSecret(key);
			}
			if (Values is not null)
			{
				return Values.GetValueOrDefault(key);
			}
			return key == "username" ? "service-account" : "synthetic-test-password";
		}
		public Task<IReadOnlyList<CredentialMetadata>> ListAsync(string tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<CredentialMetadata> CreateAsync(string tenantId, CredentialWriteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<bool> UpdateMetadataAsync(string tenantId, string id, CredentialMetadataUpdate request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<bool> RotateSecretAsync(string tenantId, string id, CredentialSecretRotation request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
	}
}
