using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	private async Task<AcceptedGitWriteExecution> CaptureWriteExecutionAsync(SourceControlContext context, RepositoryAccessSnapshot access,
		IReadOnlyCollection<string> roles, SourceControlOperationKind kind, CancellationToken cancellationToken)
	{
		if (kind is not (SourceControlOperationKind.Commit or SourceControlOperationKind.Push))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		var target = await CaptureConnectionTargetAsync(context, access.Binding.Id, roles, kind, cancellationToken);
		if (target.BindingRevision != access.Revision)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		var configuration = liveOptions?.CurrentValue ?? options.Value;
		if (target.Profile.Transport == GitTransportKind.Ssh)
		{
			return new(null, new(target, GitSshPolicyRegistry.Resolve(context, access.Binding, target.Profile, configuration)));
		}
		if (target.Profile.Transport != GitTransportKind.Https || target.Profile.Authentication is not (GitAuthenticationKind.HttpBasic or GitAuthenticationKind.HttpBearer))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		return new(new(target, GitHttpsPolicyRegistry.Resolve(context, access.Binding, target.Profile, configuration)), null);
	}

	private static string WriteExecutionFingerprint(AcceptedGitWriteExecution execution) =>
		Convert.ToHexStringLower(SHA256.HashData(execution.FingerprintBytes()));

	private static void DemandWriteRequestFingerprint(SourceControlOperationKind kind, byte[] request, AcceptedGitWriteExecution execution)
	{
		try
		{
			var fingerprint = kind switch
			{
				SourceControlOperationKind.Commit => JsonSerializer.Deserialize<AcceptedCommit>(request)?.ExecutionFingerprint,
				SourceControlOperationKind.Push => JsonSerializer.Deserialize<AcceptedPush>(request)?.ExecutionFingerprint,
				_ => null
			};
			if (fingerprint != WriteExecutionFingerprint(execution))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
		}
		catch (JsonException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}

	private async Task<AcceptedGitWriteExecution?> DemandWriteExecutionAsync(SourceControlContext context, SourceControlOperationRecord row,
		IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		var currentRoles = roles.ToArray();
		var access = await FindAsync(context.TenantId, row.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, currentRoles, access.Grants, ResolvePermission((SourceControlOperationKind)row.Kind));
		return await DemandWriteMetadataAsync(context, row, access, cancellationToken);
	}

	private async Task<AcceptedGitWriteExecution?> DemandWriteMetadataAsync(SourceControlContext context, SourceControlOperationRecord row,
		RepositoryAccessSnapshot access, CancellationToken cancellationToken)
	{
		if (row.ExecutionContractVersion == 0)
		{
			DemandLegacyExecutionContract(row);
			DemandLegacyBinding(access.Binding);
			return null;
		}
		if (row.ExecutionContractVersion is not (AcceptedGitHttpsExecution.ContractVersion or AcceptedGitSshWriteExecution.ContractVersion) || access.Binding.ConnectionProfileId is null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		if (row.Kind is not ((int)SourceControlOperationKind.Commit or (int)SourceControlOperationKind.Push)
			|| row.ActorId != context.ActorId || row.TenantId != context.TenantId || row.ProtectedRequest.Length > 6 * 1024 * 1024)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		var execution = ReadProtectedWriteExecution(row);
		var request = Unprotect(context, row.RepositoryId, row.ProtectedRequest);
		if (execution.Target.RepositoryId != row.RepositoryId
			|| execution.Target.RequiredCapabilities != RequiredCapability((SourceControlOperationKind)row.Kind)
			|| execution.WriteRequestDigest != Convert.ToHexStringLower(SHA256.HashData(request)))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		DemandWriteRequestFingerprint((SourceControlOperationKind)row.Kind, request, execution);
		var profile = await ReadAssignedProfileAsync(context, access.Binding, cancellationToken);
		SourceControlExecutionTargetPolicy.DemandCurrent(context, access, profile, execution.Target);
		var configuration = liveOptions?.CurrentValue ?? options.Value;
		if (execution.Ssh is { } ssh)
		{
			if (!ssh.Policy.Matches(GitSshPolicyRegistry.Resolve(context, access.Binding, profile, configuration)))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
			}
		}
		else
		{
			var current = GitHttpsPolicyRegistry.Resolve(context, access.Binding, profile, configuration);
			if (profile.Transport != GitTransportKind.Https || profile.Authentication is not (GitAuthenticationKind.HttpBasic or GitAuthenticationKind.HttpBearer)
				|| !execution.Https!.Policy.Matches(current))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
			}
		}
		return execution;
	}

	internal async Task<AcceptedGitWriteExecution> ReadClaimedWriteExecutionAsync(SourceControlContext context, Guid id, string worker,
		long fence, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		Relational();
		var currentRoles = roles.ToArray();
		var now = DateTimeOffset.UtcNow.UtcTicks;
		var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == id
			&& candidate.TenantId == context.TenantId && candidate.ActorId == context.ActorId && candidate.LeaseOwner == worker
			&& candidate.Fence == fence && candidate.LeaseUntilUtcTicks > now
			&& (candidate.State == (int)SourceControlOperationState.Running || candidate.State == (int)SourceControlOperationState.Reconciling), cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		return await DemandWriteExecutionAsync(context, row, currentRoles, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
	}

	private AcceptedGitWriteExecution ReadProtectedWriteExecution(SourceControlOperationRecord row) => row.ExecutionContractVersion switch
	{
		AcceptedGitHttpsExecution.ContractVersion => new(new GitHttpsExecutionProtection(protection).Unprotect(row), null),
		AcceptedGitSshWriteExecution.ContractVersion => new(null, new GitSshWriteExecutionProtection(protection).Unprotect(row)),
		_ => throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe)
	};

	private async Task LockWriteProfileAsync(SourceControlContext context, Guid id, CancellationToken cancellationToken)
	{
		var row = await db.SourceControlOperations.AsNoTracking().SingleAsync(candidate => candidate.Id == id
			&& candidate.TenantId == context.TenantId && candidate.ActorId == context.ActorId, cancellationToken);
		var access = await FindAsync(context.TenantId, row.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		var execution = await DemandWriteMetadataAsync(context, row, access, cancellationToken);
		if (execution is null)
		{
			return;
		}
		var profile = execution.Target.Profile;
		if (await db.SourceControlConnectionProfiles.Where(candidate => candidate.TenantId == context.TenantId
			&& candidate.Id == profile.Id && candidate.Revision == profile.Revision && !candidate.IsRevoked)
			.ExecuteUpdateAsync(update => update.SetProperty(candidate => candidate.Revision, candidate => candidate.Revision), cancellationToken) != 1)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
	}
}
