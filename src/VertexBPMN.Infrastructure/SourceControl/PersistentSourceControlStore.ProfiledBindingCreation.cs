using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	public async Task<RepositoryAccessSnapshot> CreateProfiledBindingAsync(SourceControlContext context,
		IReadOnlyCollection<string> authenticatedRoles, RepositoryBinding binding, Guid profileId, long profileRevision,
		CancellationToken cancellationToken)
	{
		DemandProfileAdministration(authenticatedRoles);
		var owned = binding with { ModelRoots = binding.ModelRoots.ToArray(), ConnectionProfileId = profileId };
		if (binding.TenantId != context.TenantId || binding.Id == Guid.Empty || owned.ModelRoots.Count == 0
			|| (binding.ConnectionProfileId is { } supplied && supplied != profileId))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		SourceControlInputPolicy.ValidateBranch(owned.DefaultBranch);
		SourceControlInputPolicy.ValidateBranch(owned.ReleaseBranch);
		foreach (var root in owned.ModelRoots)
		{
			SourceControlInputPolicy.ValidateRelativePath(root);
		}
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		var profile = await ReadAssignedProfileAsync(context, owned, cancellationToken);
		if (profile.Revision != profileRevision)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
		RepositoryConnectionProfilePolicy.DemandRepository(context, owned, profile);
		var json = JsonSerializer.Serialize(owned);
		var grants = new[] { new RepositoryGrant(context.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage) };
		db.SourceControlBindings.Add(new() { Id = owned.Id, TenantId = context.TenantId, Revision = 1,
			BindingJson = json, GrantsJson = JsonSerializer.Serialize(grants) });
		await db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return new(JsonSerializer.Deserialize<RepositoryBinding>(json)!, 1, grants);
	}
}
