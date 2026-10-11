using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class SourceControlClaimedPullRequestRunner(IServiceScopeFactory scopes)
{
	internal Task<ClaimedPullRequestOutcome> RunAsync(SourceControlContext context, Guid id, string worker, long fence,
		Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveRoles, CancellationToken cancellationToken) =>
		RunCoreAsync(context, id, worker, fence, resolveRoles, null, cancellationToken);

	internal Task<ClaimedPullRequestOutcome> RunPreparedAsync(SourceControlContext context, Guid id, string worker, long fence,
		Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveRoles,
		Func<SourceControlPullRequestExecutor, CancellationToken, Task<PullRequestReceipt>> execute, CancellationToken cancellationToken) =>
		RunCoreAsync(context, id, worker, fence, resolveRoles, execute, cancellationToken);

	[SuppressMessage("Roslynator", "RCS1075", Justification = "Broadly catching finalization failure is intentional: a lost lease must not propagate provider diagnostics.")]
	private async Task<ClaimedPullRequestOutcome> RunCoreAsync(SourceControlContext context, Guid id, string worker, long fence,
		Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveRoles,
		Func<SourceControlPullRequestExecutor, CancellationToken, Task<PullRequestReceipt>>? prepared, CancellationToken cancellationToken)
	{
		await using var scope = scopes.CreateAsyncScope();
		var store = scope.ServiceProvider.GetRequiredService<PersistentSourceControlStore>();
		var startingState = SourceControlOperationState.Running;
		try
		{
			var work = await store.ReadPullRequestWorkAsync(context, id, worker, fence,
				await resolveRoles(cancellationToken), cancellationToken);
			startingState = work.State;
			// Remote calls are bounded by the claim deadline. Expiry transfers only to reconciliation.
			var executor = scope.ServiceProvider.GetRequiredService<SourceControlPullRequestExecutor>();
			var receipt = prepared is null
				? await executor.ExecuteAsync(context, id, worker, fence, resolveRoles, cancellationToken)
				: await prepared(executor, cancellationToken);
			return new(SourceControlOperationState.Succeeded, receipt, null);
		}
		catch (Exception error)
		{
			using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			try
			{
				await using var finish = scopes.CreateAsyncScope();
				var current = finish.ServiceProvider.GetRequiredService<PersistentSourceControlStore>();
				var intent = await current.ReadSavedResultAsync(context.TenantId, id, worker, fence, DateTimeOffset.UtcNow, deadline.Token);
				var code = ClassifyCode(error);
				var state = ClassifyState(error, code, intent is not null, startingState);
				if (await current.FinishAsync(context.TenantId, id, worker, fence, state, DateTimeOffset.UtcNow, deadline.Token, code))
				{
					return new(state, null, code);
				}
			}
			catch (Exception)
			{
				// No authority to finish a lost claim. Persistent expiry recovery owns it now.
			}
			return new(SourceControlOperationState.ResultUnknown, null, SourceControlErrorCode.ResultUnknown);
		}
	}

	private static SourceControlErrorCode ClassifyCode(Exception error)
	{
		if (error is SourceControlSecurityException safe)
		{
			return safe.Code;
		}
		if (error is OperationCanceledException)
		{
			return SourceControlErrorCode.Cancelled;
		}
		return SourceControlErrorCode.ProviderUnavailable;
	}

	private static SourceControlOperationState ClassifyState(Exception error, SourceControlErrorCode code,
		bool hasIntent, SourceControlOperationState startingState)
	{
		if (hasIntent || startingState == SourceControlOperationState.Reconciling || code == SourceControlErrorCode.ResultUnknown)
		{
			return SourceControlOperationState.ResultUnknown;
		}
		if (code == SourceControlErrorCode.RevisionConflict)
		{
			return SourceControlOperationState.Conflict;
		}
		if (error is OperationCanceledException)
		{
			return SourceControlOperationState.Cancelled;
		}
		return SourceControlOperationState.Failed;
	}
}
