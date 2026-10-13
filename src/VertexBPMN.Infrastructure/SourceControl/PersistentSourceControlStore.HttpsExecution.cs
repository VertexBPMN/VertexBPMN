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
	/// <summary>
	/// Prepared read acceptance only. Version-two jobs remain excluded from the legacy claim path.
	/// </summary>
	internal async Task<Guid> EnqueueProfiledReadAsync(SourceControlContext context, SourceControlExecutionTarget target,
		IReadOnlyCollection<string> currentRoles, SourceControlIdempotencyKey key, ReadOnlyMemory<byte> canonicalRequest,
		GitHttpsPolicyRegistry policies, CancellationToken cancellationToken)
	{
		Relational();
		var request = canonicalRequest.ToArray();
		var roles = currentRoles.ToArray();
		if (request.Length is 0 or > 3 * 1024 * 1024)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
		}
		await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await DemandConnectionTargetAsync(context, target, roles, SourceControlOperationKind.Read, cancellationToken);
		var access = await FindAsync(context.TenantId, target.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		if (target.Profile.Transport != GitTransportKind.Https
			|| target.Profile.Authentication is not (GitAuthenticationKind.Anonymous or GitAuthenticationKind.HttpBasic or GitAuthenticationKind.HttpBearer))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		var execution = new AcceptedGitHttpsExecution(target, policies.Resolve(context, access.Binding, target.Profile));
		var hash = HttpsExecutionHash(execution, request);
		var existing = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(row => row.TenantId == context.TenantId
			&& row.RepositoryId == target.RepositoryId && row.Kind == (int)SourceControlOperationKind.Read
			&& row.IdempotencyKey == key.Value, cancellationToken);
		if (existing is not null)
		{
			if (existing.ActorId != context.ActorId)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
			}
			if (existing.ExecutionContractVersion != AcceptedGitHttpsExecution.ContractVersion || existing.RequestHash != hash)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.IdempotencyConflict);
			}
			await ReadAcceptedHttpsExecutionAsync(context, existing.Id, roles, policies, cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return existing.Id;
		}
		var operation = CreateHttpsReadOperation(context, target.RepositoryId, key, request, execution, hash);
		db.SourceControlOperations.Add(operation);
		await db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return operation.Id;
	}

	private SourceControlOperationRecord CreateHttpsReadOperation(SourceControlContext context, Guid repositoryId,
		SourceControlIdempotencyKey key, byte[] request, AcceptedGitHttpsExecution execution, string hash)
	{
		var operation = new SourceControlOperationRecord
		{
			Id = Guid.NewGuid(),
			TenantId = context.TenantId,
			ActorId = context.ActorId,
			RepositoryId = repositoryId,
			Kind = (int)SourceControlOperationKind.Read,
			IdempotencyKey = key.Value,
			RequestHash = hash,
			ProtectedRequest = Protect(context, repositoryId, request),
			ExecutionContractVersion = AcceptedGitHttpsExecution.ContractVersion,
			State = (int)SourceControlOperationState.Queued,
			UpdatedUtcTicks = DateTimeOffset.UtcNow.UtcTicks
		};
		operation.ProtectedExecutionTarget = new GitHttpsExecutionProtection(protection).Protect(operation, execution);
		return operation;
	}

	internal async Task<AcceptedGitHttpsExecution> ReadAcceptedHttpsExecutionAsync(SourceControlContext context, Guid operationId,
		IReadOnlyCollection<string> currentRoles, GitHttpsPolicyRegistry policies, CancellationToken cancellationToken)
		=> (await ReadAcceptedHttpsExecutionRecordAsync(context, operationId, currentRoles, policies, cancellationToken)).Execution;

	private async Task<(SourceControlOperationRecord Row, AcceptedGitHttpsExecution Execution)> ReadAcceptedHttpsExecutionRecordAsync(
		SourceControlContext context, Guid operationId, IReadOnlyCollection<string> currentRoles, GitHttpsPolicyRegistry policies,
		CancellationToken cancellationToken)
	{
		Relational();
		var roles = currentRoles.ToArray();
		var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.TenantId == context.TenantId
			&& candidate.ActorId == context.ActorId && candidate.Id == operationId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		var execution = new GitHttpsExecutionProtection(protection).Unprotect(row);
		if (row.Kind != (int)SourceControlOperationKind.Read || execution.Target.RepositoryId != row.RepositoryId)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		if (row.ProtectedRequest.Length > 6 * 1024 * 1024
			|| HttpsExecutionHash(execution, Unprotect(context, row.RepositoryId, row.ProtectedRequest)) != row.RequestHash)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		await DemandConnectionTargetAsync(context, execution.Target, roles, SourceControlOperationKind.Read, cancellationToken);
		var access = await FindAsync(context.TenantId, row.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		policies.DemandCurrent(context, access.Binding, execution.Target.Profile, execution.Policy);
		return (row, execution);
	}

	private static string HttpsExecutionHash(AcceptedGitHttpsExecution execution, byte[] request) =>
		Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
		{
			Execution = execution,
			Request = Convert.ToBase64String(request)
		})));
}
