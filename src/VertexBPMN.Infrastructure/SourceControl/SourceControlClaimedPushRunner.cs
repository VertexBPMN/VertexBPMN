using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record ClaimedPushOutcome(SourceControlOperationState State, PushReceipt? Receipt,
	SourceControlErrorCode? Error);

/// <summary>Execute one claimed push with independently scoped lease renewal and failure finalization.</summary>
internal sealed class SourceControlClaimedPushRunner(IServiceScopeFactory scopes)
{
	internal Task<ClaimedPushOutcome> RunAsync(SourceControlContext context, Guid id, string worker, long fence,
		Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveCurrentRoles, CancellationToken cancellationToken) =>
		RunCoreAsync(context, id, worker, fence, resolveCurrentRoles, null, TimeSpan.FromSeconds(30), cancellationToken);

	internal Task<ClaimedPushOutcome> RunPreparedAsync(SourceControlContext context, Guid id, string worker, long fence,
		Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveCurrentRoles,
		Func<SourceControlPushExecutor, CancellationToken, Task<PushReceipt>> execute,
		TimeSpan interval, CancellationToken cancellationToken) =>
		RunCoreAsync(context, id, worker, fence, resolveCurrentRoles, execute, interval, cancellationToken);

	private async Task<ClaimedPushOutcome> RunCoreAsync(SourceControlContext context, Guid id, string worker, long fence,
		Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveCurrentRoles,
		Func<SourceControlPushExecutor, CancellationToken, Task<PushReceipt>>? prepared,
		TimeSpan interval, CancellationToken cancellationToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
		var now = DateTimeOffset.UtcNow.UtcTicks;
		var claim = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
			&& x.TenantId == context.TenantId && x.ActorId == context.ActorId && x.LeaseOwner == worker
			&& x.Fence == fence && x.LeaseUntilUtcTicks > now && x.Kind == (int)SourceControlOperationKind.Push
			&& (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling),
			cancellationToken) ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		PushReceipt? completed = null;
		async Task<bool> RenewAsync(CancellationToken token)
		{
			await using var heartbeat = scopes.CreateAsyncScope();
			return await heartbeat.ServiceProvider.GetRequiredService<PersistentSourceControlStore>()
				.RenewLeaseAsync(context.TenantId, id, worker, fence, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), token);
		}
		try
		{
			if (!await RenewAsync(cancellationToken))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
			}
			var executor = scope.ServiceProvider.GetRequiredService<SourceControlPushExecutor>();
			var receipt = await SourceControlLeaseRunner.RunAsync(async token => completed = prepared is null
				? await executor.ExecuteAsync(context, id, worker, fence, resolveCurrentRoles, token)
				: await prepared(executor, token), RenewAsync, interval, cancellationToken);
			return new(SourceControlOperationState.Pushed, receipt, null);
		}
		catch (Exception exception)
		{
			using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			try
			{
				await using var finish = scopes.CreateAsyncScope();
				var store = finish.ServiceProvider.GetRequiredService<PersistentSourceControlStore>();
				if (completed is not null && await store.ConfirmCompletedPushAsync(context, id, fence, completed, deadline.Token))
				{
					return new(SourceControlOperationState.Pushed, completed, null);
				}
				var intent = await store.ReadSavedResultAsync(context.TenantId, id, worker, fence, DateTimeOffset.UtcNow, deadline.Token);
				var code = exception is RejectedRemotePushException ? SourceControlErrorCode.RevisionConflict
					: exception is SourceControlSecurityException safe ? safe.Code
					: exception is OperationCanceledException ? SourceControlErrorCode.Cancelled : SourceControlErrorCode.ProviderUnavailable;
				var state = exception is RejectedRemotePushException ? SourceControlOperationState.Conflict
					: intent is not null || claim.State == (int)SourceControlOperationState.Reconciling || code == SourceControlErrorCode.ResultUnknown
						? SourceControlOperationState.ResultUnknown
						: code == SourceControlErrorCode.RevisionConflict ? SourceControlOperationState.Conflict
							: exception is OperationCanceledException ? SourceControlOperationState.Cancelled : SourceControlOperationState.Failed;
				if (!await store.FinishAsync(context.TenantId, id, worker, fence, state, DateTimeOffset.UtcNow, deadline.Token, code))
				{
					return new(SourceControlOperationState.ResultUnknown, null, SourceControlErrorCode.ResultUnknown);
				}
				return new(state, null, code);
			}
			catch
			{
				return new(SourceControlOperationState.ResultUnknown, null, SourceControlErrorCode.ResultUnknown);
			}
		}
	}
}
