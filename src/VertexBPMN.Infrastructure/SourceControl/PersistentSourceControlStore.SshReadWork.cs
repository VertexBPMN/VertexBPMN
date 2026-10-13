using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	internal async Task<AcceptedGitSshReadWork> ReadAcceptedSshReadAsync(SourceControlContext context, Guid operationId,
		IReadOnlyCollection<string> currentRoles, GitSshPolicyRegistry policies, CancellationToken cancellationToken)
		=> (await ReadAcceptedSshReadRecordAsync(context, operationId, currentRoles, policies, cancellationToken)).Work;

	private async Task<(VertexBPMN.Domain.Entities.SourceControlOperationRecord Row, AcceptedGitSshReadWork Work)> ReadAcceptedSshReadRecordAsync(
		SourceControlContext context, Guid operationId, IReadOnlyCollection<string> currentRoles, GitSshPolicyRegistry policies,
		CancellationToken cancellationToken)
	{
		Relational();
		var roles = currentRoles.ToArray();
		var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.TenantId == context.TenantId
			&& candidate.ActorId == context.ActorId && candidate.Id == operationId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		var execution = new GitSshExecutionProtection(protection).Unprotect(row);
		if (row.Kind != (int)SourceControlOperationKind.Read || execution.Target.RepositoryId != row.RepositoryId
			|| row.ProtectedRequest.Length > 6 * 1024 * 1024)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		var bytes = Unprotect(context, row.RepositoryId, row.ProtectedRequest);
		if (SshExecutionHash(execution, bytes) != row.RequestHash)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		await DemandConnectionTargetAsync(context, execution.Target, roles, SourceControlOperationKind.Read, cancellationToken);
		var binding = (await FindAsync(context.TenantId, row.RepositoryId, cancellationToken))?.Binding
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		policies.DemandCurrent(context, binding, execution.Target.Profile, execution.Policy);
		return (row, new(execution, DecodeSshReadRequest(bytes, binding)));
	}

	private AcceptedGitRevisionRead DecodeSshReadRequest(byte[] bytes, RepositoryBinding binding)
	{
		try
		{
			var request = JsonSerializer.Deserialize<AcceptedGitRevisionRead>(bytes);
			if (request is null || request.SchemaVersion != 1 || (request.Query is null && request.Revision is null))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			request.Query?.Validate(binding, request.Revision, options.Value.Limits);
			return request;
		}
		catch (Exception error) when (error is JsonException or ArgumentException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}
}
