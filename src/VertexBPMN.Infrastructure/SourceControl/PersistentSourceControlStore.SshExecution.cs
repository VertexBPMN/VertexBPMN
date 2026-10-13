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
	internal async Task<Guid> EnqueueSshReadAsync(SourceControlContext context, SourceControlExecutionTarget target,
		IReadOnlyCollection<string> currentRoles, SourceControlIdempotencyKey key, AcceptedGitRevisionRead request,
		GitSshPolicyRegistry policies, CancellationToken cancellationToken)
	{
		Relational();
		var roles = currentRoles.ToArray();
		var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
		if (bytes.Length is 0 or > 3 * 1024 * 1024)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
		}
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await DemandConnectionTargetAsync(context, target, roles, SourceControlOperationKind.Read, cancellationToken);
		var binding = (await FindAsync(context.TenantId, target.RepositoryId, cancellationToken))?.Binding
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		_ = DecodeSshReadRequest(bytes, binding);
		var execution = new AcceptedGitSshExecution(target, policies.Resolve(context, binding, target.Profile));
		var hash = SshExecutionHash(execution, bytes);
		var existing = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(row => row.TenantId == context.TenantId
			&& row.RepositoryId == target.RepositoryId && row.Kind == (int)SourceControlOperationKind.Read
			&& row.IdempotencyKey == key.Value, cancellationToken);
		if (existing is not null)
		{
			if (existing.ActorId != context.ActorId)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
			}
			if (existing.ExecutionContractVersion != AcceptedGitSshExecution.ContractVersion || existing.RequestHash != hash)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.IdempotencyConflict);
			}
			_ = await ReadAcceptedSshReadAsync(context, existing.Id, roles, policies, cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return existing.Id;
		}
		var operation = CreateSshReadOperation(context, target.RepositoryId, key, bytes, execution, hash);
		db.SourceControlOperations.Add(operation);
		await db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return operation.Id;
	}

	private SourceControlOperationRecord CreateSshReadOperation(SourceControlContext context, Guid repositoryId,
		SourceControlIdempotencyKey key, byte[] request, AcceptedGitSshExecution execution, string hash)
	{
		var row = new SourceControlOperationRecord
		{
			Id = Guid.NewGuid(),
			TenantId = context.TenantId,
			ActorId = context.ActorId,
			RepositoryId = repositoryId,
			Kind = (int)SourceControlOperationKind.Read,
			IdempotencyKey = key.Value,
			RequestHash = hash,
			ProtectedRequest = Protect(context, repositoryId, request),
			ExecutionContractVersion = AcceptedGitSshExecution.ContractVersion,
			State = (int)SourceControlOperationState.Queued,
			UpdatedUtcTicks = DateTimeOffset.UtcNow.UtcTicks
		};
		row.ProtectedExecutionTarget = new GitSshExecutionProtection(protection).Protect(row, execution);
		return row;
	}

	private static string SshExecutionHash(AcceptedGitSshExecution execution, byte[] request) =>
		Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
		{ Execution = execution, Request = Convert.ToBase64String(request) })));
}
