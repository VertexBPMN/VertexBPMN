using VertexBPMN.Domain.Interfaces;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>Internal resolution; no HTTP DTO or diagnostic string ever carries secret values.</summary>
public sealed class SourceControlCredentialResolver(ISourceControlAccessStore store, ICredentialService credentials)
{
	public async Task<RepositoryAccessSnapshot> AuthorizeAsync(SourceControlContext context, Guid repositoryId,
		IReadOnlyCollection<string> authenticatedRoles, RepositoryPermission permission, CancellationToken cancellationToken)
	{
		var access = await store.FindAsync(context.TenantId, repositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, authenticatedRoles, access.Grants, permission);
		return access;
	}

	public async Task<string> ResolveAsync(SourceControlContext context, Guid repositoryId, long expectedRevision,
		IReadOnlyCollection<string> authenticatedRoles, RepositoryPermission permission, string credentialType,
		string secretKey, CancellationToken cancellationToken)
	{
		var access = await AuthorizeAsync(context, repositoryId, authenticatedRoles, permission, cancellationToken);
		if (access.Revision != expectedRevision)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}

		if (access.Binding.CredentialReference is not { Length: > 0 } reference)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}

		try
		{
			var metadata = await credentials.GetAsync(context.TenantId, reference, cancellationToken);
			if (metadata is null || metadata.TenantId != context.TenantId || metadata.Type != credentialType)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
			}

			return await credentials.ResolveSecretAsync(context.TenantId, reference, secretKey, cancellationToken)
				?? throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		catch (OperationCanceledException) { throw; }
		catch (SourceControlSecurityException) { throw; }
		catch { throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
	}
}
