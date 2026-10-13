using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Private read execution; heartbeat, finalization and cleanup each own a fresh scope.</summary>
internal sealed class SourceControlClaimedSshReadRunner(IServiceScopeFactory scopes, IOptions<SourceControlOptions> options)
{
	internal Task<ClaimedReadOutcome<T>> RunAsync<T>(SourceControlContext context, Guid id, string worker, long fence,
		Func<GitWorkspace, AcceptedGitSshReadWork, CancellationToken, Task<T>> read, CancellationToken cancellationToken) =>
		RunControlledAsync(context, id, worker, fence,
			(executor, token) => executor.ExecuteAsync(context, id, worker, fence, read, token), TimeSpan.FromSeconds(30), cancellationToken);

	internal Task<ClaimedReadOutcome<T>> RunWorkerAsync<T>(SourceControlContext context, Guid id, string worker, long fence,
		Func<GitWorkspace, AcceptedGitSshReadWork, CancellationToken, Task<T>> read, CancellationToken cancellationToken) =>
		RunCoreAsync(context, id, worker, fence, (executor, token) => executor.ExecuteAsync(context, id, worker, fence, read, token),
			TimeSpan.FromSeconds(30), ReadCancellationSource.WorkerShutdown, cancellationToken);

	internal Task<ClaimedReadOutcome<T>> RunControlledAsync<T>(SourceControlContext context, Guid id, string worker, long fence,
		Func<SourceControlSshReadExecutor, CancellationToken, Task<T>> execute, TimeSpan interval, CancellationToken cancellationToken) =>
		RunCoreAsync(context, id, worker, fence, execute, interval, ReadCancellationSource.Caller, cancellationToken);

	internal Task<ClaimedReadOutcome<T>> RunControlledWorkerAsync<T>(SourceControlContext context, Guid id, string worker, long fence,
		Func<SourceControlSshReadExecutor, CancellationToken, Task<T>> execute, TimeSpan interval, CancellationToken cancellationToken) =>
		RunCoreAsync(context, id, worker, fence, execute, interval, ReadCancellationSource.WorkerShutdown, cancellationToken);

	private async Task<ClaimedReadOutcome<T>> RunCoreAsync<T>(SourceControlContext context, Guid id, string worker, long fence,
		Func<SourceControlSshReadExecutor, CancellationToken, Task<T>> execute, TimeSpan interval,
		ReadCancellationSource cancellationSource, CancellationToken cancellationToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		try
		{
			await DemandOwnershipAsync(scope.ServiceProvider, context, id, worker, fence, cancellationToken);
		}
		catch (Exception error) when (error is not SourceControlSecurityException and not OperationCanceledException)
		{
			// Ownership was not established: do not change the job or expose backend diagnostics.
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		var executor = scope.ServiceProvider.GetRequiredService<SourceControlSshReadExecutor>();
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.ReadTimeout);
		ClaimedReadOutcome<T> outcome;
		var cleanupPending = false;
		try
		{
			async Task<bool> RenewAsync(CancellationToken token)
			{
				await using var heartbeat = scopes.CreateAsyncScope();
				await DemandCurrentAsync(heartbeat.ServiceProvider, context, id, worker, fence, token);
				return await heartbeat.ServiceProvider.GetRequiredService<PersistentSourceControlStore>()
					.RenewLeaseAsync(context.TenantId, id, worker, fence, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), token);
			}
			if (!await RenewAsync(deadline.Token))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
			}
			var result = await SourceControlLeaseRunner.RunAsync(token => execute(executor, token), RenewAsync, interval, deadline.Token);
			await using var finish = scopes.CreateAsyncScope();
			var roles = await finish.ServiceProvider.GetRequiredService<ISourceControlActorResolver>().ResolveAsync(context, deadline.Token);
			if (!await finish.ServiceProvider.GetRequiredService<PersistentSourceControlStore>().FinishSshReadAsync(context, id,
				worker, fence, roles, finish.ServiceProvider.GetRequiredService<GitSshPolicyRegistry>(), result, deadline.Token))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
			}
			outcome = new(SourceControlOperationState.Succeeded, result, null);
		}
		catch (Exception error)
		{
			outcome = await FinishFailureAsync<T>(context, id, worker, fence, error, cancellationSource, cancellationToken);
		}
		finally
		{
			if (executor.HasCreatedWorkspace)
			{
				cleanupPending = !await CleanupAsync(context, id, fence);
			}
		}
		return outcome with { CleanupPending = cleanupPending };
	}

	private static async Task DemandOwnershipAsync(IServiceProvider services, SourceControlContext context, Guid id,
		string worker, long fence, CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow.UtcTicks;
		if (!await services.GetRequiredService<BpmnDbContext>().SourceControlOperations.AsNoTracking().AnyAsync(row =>
			row.Id == id && row.TenantId == context.TenantId && row.ActorId == context.ActorId
			&& row.Kind == (int)SourceControlOperationKind.Read && row.ExecutionContractVersion == AcceptedGitSshExecution.ContractVersion
			&& row.State == (int)SourceControlOperationState.Running && row.LeaseOwner == worker && row.Fence == fence
			&& row.LeaseUntilUtcTicks > now, cancellationToken))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
	}

	private static async Task DemandCurrentAsync(IServiceProvider services, SourceControlContext context, Guid id,
		string worker, long fence, CancellationToken cancellationToken) =>
		_ = await services.GetRequiredService<PersistentSourceControlStore>().ReadClaimedSshReadAsync(context, id, worker, fence,
			await services.GetRequiredService<ISourceControlActorResolver>().ResolveAsync(context, cancellationToken),
			services.GetRequiredService<GitSshPolicyRegistry>(), cancellationToken);

	private async Task<ClaimedReadOutcome<T>> FinishFailureAsync<T>(SourceControlContext context, Guid id, string worker, long fence,
		Exception error, ReadCancellationSource cancellationSource, CancellationToken callerToken)
	{
		var code = error switch
		{
			SourceControlSecurityException safe => safe.Code,
			OperationCanceledException when callerToken.IsCancellationRequested && cancellationSource == ReadCancellationSource.WorkerShutdown
				=> SourceControlErrorCode.ResultUnknown,
			OperationCanceledException => callerToken.IsCancellationRequested ? SourceControlErrorCode.Cancelled : SourceControlErrorCode.TimedOut,
			_ => SourceControlErrorCode.ProviderUnavailable
		};
		var state = code switch
		{
			SourceControlErrorCode.Cancelled => SourceControlOperationState.Cancelled,
			SourceControlErrorCode.RevisionConflict => SourceControlOperationState.Conflict,
			SourceControlErrorCode.ResultUnknown => SourceControlOperationState.ResultUnknown,
			_ => SourceControlOperationState.Failed
		};
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		try
		{
			await using var finish = scopes.CreateAsyncScope();
			if (await finish.ServiceProvider.GetRequiredService<PersistentSourceControlStore>().FinishAsync(context.TenantId, id,
				worker, fence, state, DateTimeOffset.UtcNow, deadline.Token, code))
			{
				return new(state, default, code);
			}
		}
		catch (Exception)
		{
			// An uncertain finish cannot authorize returning a result or overwriting another claim.
			return new(SourceControlOperationState.ResultUnknown, default, SourceControlErrorCode.ResultUnknown);
		}
		return new(SourceControlOperationState.ResultUnknown, default, SourceControlErrorCode.ResultUnknown);
	}

	private async Task<bool> CleanupAsync(SourceControlContext context, Guid id, long fence)
	{
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		try
		{
			await using var cleanup = scopes.CreateAsyncScope();
			await cleanup.ServiceProvider.GetRequiredService<SourceControlWorkspace>().CleanupAsync(context, id, fence, deadline.Token);
			return true;
		}
		catch (Exception)
		{
			// Preserve the established outcome; maintenance retries only verified owned directories.
			return false;
		}
	}
}
