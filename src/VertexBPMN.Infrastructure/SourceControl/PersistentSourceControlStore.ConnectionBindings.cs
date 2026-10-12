using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	/// <summary>Assign a profile without changing the repository identity, branches or credentials.</summary>
	public async Task<bool> AssignConnectionProfileAsync(SourceControlContext context,
		IReadOnlyCollection<string> authenticatedRoles, Guid repositoryId, long expectedBindingRevision,
		Guid profileId, long expectedProfileRevision, CancellationToken cancellationToken)
	{
		DemandProfileAdministration(authenticatedRoles);
		ValidateProfileRevision(expectedBindingRevision);
		ValidateProfileRevision(expectedProfileRevision);
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		var access = await FindAsync(context.TenantId, repositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, authenticatedRoles, access.Grants, RepositoryPermission.Manage);
		if (access.Revision != expectedBindingRevision)
		{
			return false;
		}

		var profile = await FindConnectionProfileAsync(context, authenticatedRoles, profileId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		if (profile.Revision != expectedProfileRevision)
		{
			return false;
		}

		RepositoryConnectionProfilePolicy.DemandRepository(context, access.Binding, profile);
		if (await db.SourceControlOperations.AnyAsync(x => x.TenantId == context.TenantId && x.RepositoryId == repositoryId
			&& (x.State == (int)SourceControlOperationState.Queued || x.State == (int)SourceControlOperationState.Running
				|| x.State == (int)SourceControlOperationState.ResultUnknown || x.State == (int)SourceControlOperationState.Reconciling),
			cancellationToken))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}

		var json = JsonSerializer.Serialize(access.Binding with { ConnectionProfileId = profile.Id });
		var changed = await db.SourceControlBindings.Where(x => x.TenantId == context.TenantId && x.Id == repositoryId
			&& x.Revision == expectedBindingRevision).ExecuteUpdateAsync(update => update
				.SetProperty(x => x.BindingJson, json).SetProperty(x => x.Revision, expectedBindingRevision + 1), cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return changed == 1;
	}

	internal async Task<SourceControlExecutionTarget> CaptureConnectionTargetAsync(SourceControlContext context,
		Guid repositoryId, IReadOnlyCollection<string> currentRoles, SourceControlOperationKind kind,
		CancellationToken cancellationToken)
	{
		Relational();
		var access = await FindAsync(context.TenantId, repositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, currentRoles, access.Grants, ResolvePermission(kind));
		var profile = await ReadAssignedProfileAsync(context, access.Binding, cancellationToken);
		return SourceControlExecutionTargetPolicy.Capture(context, access, profile, RequiredCapability(kind));
	}

	/// <summary>Revalidate current profile, capabilities and grants for an already frozen target.</summary>
	internal async Task DemandConnectionTargetAsync(SourceControlContext context, SourceControlExecutionTarget target,
		IReadOnlyCollection<string> currentRoles, SourceControlOperationKind kind, CancellationToken cancellationToken)
	{
		Relational();
		if (target.RequiredCapabilities != RequiredCapability(kind))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		var access = await FindAsync(context.TenantId, target.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, currentRoles, access.Grants, ResolvePermission(kind));
		var profile = await ReadAssignedProfileAsync(context, access.Binding, cancellationToken);
		SourceControlExecutionTargetPolicy.DemandCurrent(context, access, profile, target);
	}

	/// <summary>Decode future accepted targets without allowing today's worker to claim their jobs.</summary>
	internal async Task<SourceControlExecutionTarget> ReadAcceptedExecutionTargetAsync(SourceControlContext context,
		Guid operationId, IReadOnlyCollection<string> currentRoles, CancellationToken cancellationToken)
	{
		Relational();
		var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == context.TenantId
			&& x.ActorId == context.ActorId && x.Id == operationId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		var kind = (SourceControlOperationKind)row.Kind;
		var access = await FindAsync(context.TenantId, row.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, currentRoles, access.Grants, ResolvePermission(kind));
		var accepted = DecodeExecutionTarget(context, row);
		if (accepted.RepositoryId != row.RepositoryId || accepted.RequiredCapabilities != RequiredCapability(kind))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}

		var profile = await ReadAssignedProfileAsync(context, access.Binding, cancellationToken);
		SourceControlExecutionTargetPolicy.DemandCurrent(context, access, profile, accepted);
		return accepted;
	}

	private SourceControlExecutionTarget DecodeExecutionTarget(SourceControlContext context, SourceControlOperationRecord row)
	{
		if (row.ExecutionContractVersion != 1 || string.IsNullOrEmpty(row.ProtectedExecutionTarget)
			|| row.ProtectedExecutionTarget.Length > 64 * 1024)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}

		try
		{
			return JsonSerializer.Deserialize<SourceControlExecutionTarget>(
				protection.CreateProtector("VertexBPMN.SourceControl.ExecutionTarget.v1", context.TenantId,
					row.RepositoryId.ToString("N"), context.ActorId, row.Id.ToString("N"))
					.Unprotect(Convert.FromBase64String(row.ProtectedExecutionTarget)))
				?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		catch (Exception error) when (error is JsonException or FormatException or System.Security.Cryptography.CryptographicException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}

	private async Task<RepositoryConnectionProfile> ReadAssignedProfileAsync(SourceControlContext context,
		RepositoryBinding binding, CancellationToken cancellationToken)
	{
		if (binding.ConnectionProfileId is not { } profileId)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}

		var row = await db.SourceControlConnectionProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == context.TenantId
			&& x.Id == profileId && !x.IsRevoked, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		return DecodeProfile(row);
	}

	private static SourceControlCapability RequiredCapability(SourceControlOperationKind kind) => kind switch
	{
		SourceControlOperationKind.Read => SourceControlCapability.Read,
		SourceControlOperationKind.Commit => SourceControlCapability.Commit,
		SourceControlOperationKind.Push => SourceControlCapability.Push,
		SourceControlOperationKind.PullRequest => SourceControlCapability.PullRequest,
		_ => throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable)
	};

	private static void DemandLegacyBinding(RepositoryBinding binding)
	{
		if (binding.ConnectionProfileId is not null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
	}

	private static void DemandLegacyExecutionContract(SourceControlOperationRecord row)
	{
		if (row.ExecutionContractVersion != 0 || row.ProtectedExecutionTarget is not null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
	}
}
