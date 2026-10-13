using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	private static readonly SourceControlContext Context = new("tenant-a", "actor-a");
	private static readonly string[] Admin = ["Admin"];

	[Fact]
	public async Task Assignment_preserves_binding_fields_and_increments_revision()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var access = await fixture.Store.FindAsync(Context.TenantId, fixture.Binding.Id, TestContext.Current.CancellationToken);
		Assert.NotNull(access);
		Assert.Equal(2, access.Revision);
		Assert.Equal(JsonSerializer.Serialize(fixture.Binding with { ConnectionProfileId = fixture.Profile.Id }),
			JsonSerializer.Serialize(access.Binding));
		Assert.False(await fixture.AssignAsync());
		Assert.Null(fixture.Binding.ConnectionProfileId);
		Assert.DoesNotContain("ConnectionProfileId", JsonSerializer.Serialize(fixture.Binding), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(SourceControlOperationState.Queued)]
	[InlineData(SourceControlOperationState.Running)]
	[InlineData(SourceControlOperationState.ResultUnknown)]
	[InlineData(SourceControlOperationState.Reconciling)]
	public async Task Active_or_unknown_legacy_operations_prevent_assignment(SourceControlOperationState state)
	{
		await using var fixture = await Fixture.OpenAsync();
		var job = fixture.Operation(state);
		fixture.Db.SourceControlOperations.Add(job);
		await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
		await RejectAsync(SourceControlErrorCode.RevisionConflict, () => fixture.AssignAsync());
		Assert.Null((await fixture.Store.FindAsync(Context.TenantId, fixture.Binding.Id,
			TestContext.Current.CancellationToken))!.Binding.ConnectionProfileId);
	}

	[Fact]
	public async Task Foreign_profile_wrong_role_stale_revision_and_revocation_cannot_be_assigned()
	{
		await using var fixture = await Fixture.OpenAsync();
		await RejectAsync(SourceControlErrorCode.Forbidden, () => fixture.Store.AssignConnectionProfileAsync(Context, [],
			fixture.Binding.Id, 1, fixture.Profile.Id, 1, TestContext.Current.CancellationToken));
		var foreignProfile = fixture.Profile with { Id = Guid.NewGuid(), TenantId = "tenant-b" };
		await fixture.Store.CreateConnectionProfileAsync(new("tenant-b", Context.ActorId), Admin, foreignProfile,
			TestContext.Current.CancellationToken);
		await RejectAsync(SourceControlErrorCode.NotFound, () => fixture.Store.AssignConnectionProfileAsync(Context, Admin,
			fixture.Binding.Id, 1, foreignProfile.Id, 1, TestContext.Current.CancellationToken));
		Assert.False(await fixture.Store.AssignConnectionProfileAsync(Context, Admin, fixture.Binding.Id, 1,
			fixture.Profile.Id, 2, TestContext.Current.CancellationToken));
		Assert.True(await fixture.Store.RevokeConnectionProfileAsync(Context, Admin, fixture.Profile.Id, 1,
			TestContext.Current.CancellationToken));
		await RejectAsync(SourceControlErrorCode.NotFound, () => fixture.AssignAsync());
	}

	[Fact]
	public async Task Captured_target_requires_grants_and_generic_binding_cannot_use_legacy_enqueue()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await fixture.CaptureAsync();
		Assert.Equal(2, target.BindingRevision);
		Assert.Equal(fixture.Profile, target.Profile);
		Assert.Equal(SourceControlCapability.Commit, target.RequiredCapabilities);
		await RejectAsync(SourceControlErrorCode.NotFound, () => fixture.Store.CaptureConnectionTargetAsync(
			new(Context.TenantId, "other-actor"), fixture.Binding.Id, [], SourceControlOperationKind.Commit,
			TestContext.Current.CancellationToken));
		await RejectAsync(SourceControlErrorCode.ProviderUnavailable, () => fixture.Store.EnqueueAsync(Context,
			fixture.Binding.Id, Admin, SourceControlOperationKind.Read, new("profile-read"), new byte[] { 1 },
			TestContext.Current.CancellationToken));
	}

	[Theory]
	[InlineData(1, false)]
	[InlineData(99, false)]
	[InlineData(-1, false)]
	[InlineData(0, true)]
	public async Task Unsupported_execution_contracts_cannot_be_claimed_or_change_receipts(int version, bool hasTarget)
	{
		await using var fixture = await Fixture.OpenAsync();
		var job = fixture.Operation(SourceControlOperationState.ResultUnknown);
		job.ExecutionContractVersion = version;
		job.ProtectedExecutionTarget = hasTarget ? "future-protected-target" : null;
		job.Fence = 8;
		job.ProtectedResult = "unchanged-receipt";
		fixture.Db.SourceControlOperations.Add(job);
		await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
		Assert.Null(await fixture.Store.TryClaimAsync(Context.TenantId, job.Id, "legacy-worker", DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
		var retained = await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
		Assert.Equal(8, retained.Fence);
		Assert.Equal((int)SourceControlOperationState.ResultUnknown, retained.State);
		Assert.Null(retained.LeaseOwner);
		Assert.Equal("unchanged-receipt", retained.ProtectedResult);
	}

	[Fact]
	public async Task Legacy_contract_remains_claimable()
	{
		await using var fixture = await Fixture.OpenAsync();
		var job = fixture.Operation(SourceControlOperationState.Queued);
		fixture.Db.SourceControlOperations.Add(job);
		await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
		Assert.Equal(1, await fixture.Store.TryClaimAsync(Context.TenantId, job.Id, "legacy-worker", DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
	}

	[Theory]
	[InlineData("unchanged")]
	[InlineData("updated")]
	[InlineData("revoked")]
	[InlineData("replayed")]
	public async Task Encrypted_target_survives_restart_and_rejects_profile_changes_or_other_operation(string action)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await fixture.CaptureAsync();
		var job = fixture.Operation(SourceControlOperationState.Queued);
		job.ExecutionContractVersion = 1;
		job.ProtectedExecutionTarget = fixture.ProtectTarget(job.Id, target);
		if (action == "replayed")
		{
			job.Id = Guid.NewGuid();
		}

		fixture.Db.SourceControlOperations.Add(job);
		await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
		if (action == "updated")
		{
			Assert.True(await fixture.Store.ReplaceConnectionProfileAsync(Context, Admin, 1,
				fixture.Profile with { Revision = 2, GitCredentialReference = "rotated-reference" }, TestContext.Current.CancellationToken));
		}
		else if (action == "revoked")
		{
			Assert.True(await fixture.Store.RevokeConnectionProfileAsync(Context, Admin, fixture.Profile.Id, 1,
				TestContext.Current.CancellationToken));
		}

		await using var restarted = fixture.NewContext();
		var store = fixture.NewStore(restarted);
		if (action == "unchanged")
		{
			Assert.Equal(target, await store.ReadAcceptedExecutionTargetAsync(Context, job.Id, Admin,
				TestContext.Current.CancellationToken));
		}
		else
		{
			var expected = action switch
			{
				"updated" => SourceControlErrorCode.RevisionConflict,
				"revoked" => SourceControlErrorCode.ProviderUnavailable,
				_ => SourceControlErrorCode.ContentUnsafe
			};
			await RejectAsync(expected, () => store.ReadAcceptedExecutionTargetAsync(Context, job.Id, Admin,
				TestContext.Current.CancellationToken));
		}
	}

	private static async Task RejectAsync(SourceControlErrorCode code, Func<Task> action)
	{
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(action);
		Assert.Equal(code, error.Code);
	}

	private sealed class Fixture : IAsyncDisposable
	{
		private readonly SqliteConnection connection = new("Data Source=:memory:");
		public BpmnDbContext Db { get; }
		private readonly IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
		public RepositoryBinding Binding { get; }
		public RepositoryConnectionProfile Profile { get; }

		private Fixture(Uri? remote = null)
		{
			Binding = new(Guid.NewGuid(), Context.TenantId, remote ?? new Uri("https://github.com/team/repository.git"),
				"legacy-reference", "main", "release", ["models"]);
			Db = NewContext();
			var ssh = Binding.Remote.Scheme == "ssh";
			Profile = new(Guid.NewGuid(), Context.TenantId, 1, ssh ? GitTransportKind.Ssh : GitTransportKind.Https,
				ssh ? GitAuthenticationKind.SshKey : GitAuthenticationKind.HttpBasic,
				new(Binding.Remote, "repository-network", ssh ? "repository-trust" : null), "new-reference",
				SourceControlCapability.Read | SourceControlCapability.Commit | SourceControlCapability.Push);
		}

		public PersistentSourceControlStore Store => NewStore(Db);

		public BpmnDbContext NewContext() => new(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
		public SourceControlWorkspace NewWorkspaces(BpmnDbContext context, SourceControlOptions configuration) => new(context, protection, Options.Create(configuration));
		public PersistentSourceControlStore NewStore(BpmnDbContext context, SourceControlOptions? configuration = null,
			IOptionsMonitor<SourceControlOptions>? monitor = null) => new(context, protection,
			Options.Create(configuration ?? new SourceControlOptions { Enabled = true, AllowedHosts = ["github.com"] }), monitor);
		public Task<bool> AssignAsync() => Store.AssignConnectionProfileAsync(Context, Admin, Binding.Id, 1,
			Profile.Id, 1, TestContext.Current.CancellationToken);
		public Task<SourceControlExecutionTarget> CaptureAsync() => Store.CaptureConnectionTargetAsync(Context,
			Binding.Id, Admin, SourceControlOperationKind.Commit, TestContext.Current.CancellationToken);
		public string ProtectTarget(Guid operationId, SourceControlExecutionTarget target) => Convert.ToBase64String(
			protection.CreateProtector("VertexBPMN.SourceControl.ExecutionTarget.v1", Context.TenantId,
				Binding.Id.ToString("N"), Context.ActorId, operationId.ToString("N"))
				.Protect(JsonSerializer.SerializeToUtf8Bytes(target)));

		public SourceControlOperationRecord Operation(SourceControlOperationState state) => new()
		{
			Id = Guid.NewGuid(),
			RepositoryId = Binding.Id,
			TenantId = Context.TenantId,
			ActorId = Context.ActorId,
			Kind = (int)SourceControlOperationKind.Commit,
			State = (int)state,
			IdempotencyKey = Guid.NewGuid().ToString("N"),
			RequestHash = new string('a', 64),
			ProtectedRequest = "legacy-protected-request"
		};

		public static async Task<Fixture> OpenAsync(Uri? remote = null)
		{
			var fixture = new Fixture(remote);
			try
			{
				await fixture.connection.OpenAsync(TestContext.Current.CancellationToken);
				await fixture.Db.Database.MigrateAsync(TestContext.Current.CancellationToken);
				if (remote is null)
				{
					await fixture.Store.CreateBindingAsync(Context, Admin, fixture.Binding, TestContext.Current.CancellationToken);
				}
				else
				{
					await fixture.Store.CreateConnectionProfileAsync(Context, Admin, fixture.Profile, TestContext.Current.CancellationToken);
					await fixture.Store.CreateProfiledBindingAsync(Context, Admin, fixture.Binding, fixture.Profile.Id, 1,
						TestContext.Current.CancellationToken);
				}
				var row = await fixture.Db.SourceControlBindings.SingleAsync(TestContext.Current.CancellationToken);
				row.GrantsJson = JsonSerializer.Serialize(new[] { new RepositoryGrant(Context.ActorId,
					RepositoryPermission.Read | RepositoryPermission.Manage | RepositoryPermission.Commit | RepositoryPermission.Push) });
				await fixture.Db.SaveChangesAsync(TestContext.Current.CancellationToken);
				if (remote is null)
				{
					await fixture.Store.CreateConnectionProfileAsync(Context, Admin, fixture.Profile, TestContext.Current.CancellationToken);
				}
				return fixture;
			}
			catch
			{
				await fixture.DisposeAsync();
				throw;
			}
		}

		public async ValueTask DisposeAsync()
		{
			await Db.DisposeAsync();
			await connection.DisposeAsync();
		}
	}
}
