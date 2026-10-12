using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	private async Task<AcceptedHostingExecution> CaptureHostingExecutionAsync(SourceControlContext context,
		RepositoryAccessSnapshot access, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		var target = await CaptureConnectionTargetAsync(context, access.Binding.Id, roles, SourceControlOperationKind.PullRequest, cancellationToken);
		if (target.BindingRevision != access.Revision)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
		var hosting = (hostingTargets ?? new RepositoryHostingTargetPolicy()).Resolve(context, access.Binding, target.Profile);
		var policy = GitHttpsPolicyRegistry.Resolve(context, HostingHttpsContext.ApiBinding(access.Binding, hosting),
			HostingHttpsContext.ApiProfile(target.Profile), liveOptions?.CurrentValue ?? options.Value);
		// The first profiled adapter uses GitHub's public API with the normal system trust store.
		if (policy.Trust is not null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		return new(target, hosting, policy);
	}

	private static string HostingFingerprint(AcceptedHostingExecution execution) => Convert.ToHexStringLower(
		SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(execution with { RequestDigest = null })));

	internal async Task DemandCurrentHostingAsync(SourceControlContext context, AcceptedHostingExecution execution,
		IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		if (execution.SchemaVersion != AcceptedHostingExecution.ContractVersion || execution.Target.RequiredCapabilities != SourceControlCapability.PullRequest)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		var currentRoles = roles.ToArray();
		var access = await FindAsync(context.TenantId, execution.Target.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, currentRoles, access.Grants, RepositoryPermission.PullRequest);
		var current = await CaptureHostingExecutionAsync(context, access, currentRoles, cancellationToken);
		SourceControlExecutionTargetPolicy.DemandCurrent(context, access, current.Target.Profile, execution.Target);
		if (execution.HostingTarget != current.HostingTarget
			|| execution.HostingTarget.ApiRepositoryUri.AbsoluteUri != current.HostingTarget.ApiRepositoryUri.AbsoluteUri
			|| execution.HostingTarget.WebRepositoryUri.AbsoluteUri != current.HostingTarget.WebRepositoryUri.AbsoluteUri
			|| !execution.ApiPolicy.Matches(current.ApiPolicy))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
	}

	private async Task<AcceptedHostingExecution> DemandHostingRowAsync(SourceControlContext context, SourceControlOperationRecord row,
		IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		var execution = ReadHistoricalHostingExecution(context, row);
		await DemandCurrentHostingAsync(context, execution, roles, cancellationToken);
		return execution;
	}

	private AcceptedHostingExecution ReadHistoricalHostingExecution(SourceControlContext context, SourceControlOperationRecord row)
	{
		// The 16 Ki-character description can expand sixfold in JSON, then again in protected Base64.
		if (row.TenantId != context.TenantId || row.ActorId != context.ActorId
			|| row.Kind != (int)SourceControlOperationKind.PullRequest || row.ProtectedRequest.Length > 256 * 1024)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		var execution = new HostingExecutionProtection(protection).Unprotect(row);
		var request = Unprotect(context, row.RepositoryId, row.ProtectedRequest);
		if (execution.Target.RepositoryId != row.RepositoryId || execution.Target.RequiredCapabilities != SourceControlCapability.PullRequest
			|| execution.RequestDigest != Convert.ToHexStringLower(SHA256.HashData(request))
			|| row.RequestHash != RequestHash(execution.Target.BindingRevision, request))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		try
		{
			var accepted = JsonSerializer.Deserialize<AcceptedPullRequest>(request);
			if (accepted is null || accepted.SchemaVersion != 2 || accepted.ExecutionFingerprint != HostingFingerprint(execution))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
		}
		catch (JsonException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		return execution;
	}

	internal async Task<long?> TryClaimHostingAsync(SourceControlContext context, Guid id, string worker, DateTimeOffset now,
		TimeSpan duration, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		Relational();
		ValidateHttpsReadClaim(worker, duration);
		var currentRoles = roles.ToArray();
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		try
		{
			var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == id
				&& candidate.TenantId == context.TenantId && candidate.ActorId == context.ActorId, cancellationToken);
			if (row is null || row.Kind != (int)SourceControlOperationKind.PullRequest || row.ExecutionContractVersion != AcceptedHostingExecution.ContractVersion)
			{
				return null;
			}
			await DemandHostingRowAsync(context, row, currentRoles, cancellationToken);
			if (!HttpsReadCanBeClaimed(row, now) || !await HasHttpsReadSlotAsync(context.TenantId, now, cancellationToken))
			{
				return null;
			}
			var changed = await ClaimProfiledWriteRowAsync(row, worker, now, duration, cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return changed == 1 ? row.Fence + 1 : null;
		}
		catch (Exception error) when (IsClaimContention(error)) { return null; }
	}

	private async Task LockHostingProfileAsync(SourceControlContext context, AcceptedHostingExecution execution, CancellationToken cancellationToken)
	{
		var profile = execution.Target.Profile;
		if (await db.SourceControlConnectionProfiles.Where(row => row.TenantId == context.TenantId
			&& row.Id == profile.Id && row.Revision == profile.Revision && !row.IsRevoked)
			.ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, row => row.Revision), cancellationToken) != 1)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
	}
}
