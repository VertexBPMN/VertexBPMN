using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record ClaimedCommitOutcome(SourceControlOperationState State, CommitReceipt? Receipt,
    SourceControlErrorCode? Error);

/// <summary>One already claimed operation. Each heartbeat/finalization owns a separate scope.</summary>
internal sealed class SourceControlClaimedCommitRunner(IServiceScopeFactory scopes)
{
    internal Task<ClaimedCommitOutcome> RunAsync(SourceControlContext context, Guid id, string worker, long fence,
        Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveCurrentRoles, CancellationToken cancellationToken)
        => RunCoreAsync(context, id, worker, fence, resolveCurrentRoles, null, TimeSpan.FromSeconds(30), cancellationToken);

    internal Task<ClaimedCommitOutcome> RunPreparedAsync(SourceControlContext context, Guid id, string worker, long fence,
        Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveCurrentRoles, GitWorkspace workspace,
        TimeSpan interval, CancellationToken cancellationToken)
        => RunCoreAsync(context, id, worker, fence, resolveCurrentRoles, workspace, interval, cancellationToken);

    private async Task<ClaimedCommitOutcome> RunCoreAsync(SourceControlContext context, Guid id, string worker, long fence,
        Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveCurrentRoles, GitWorkspace? prepared,
        TimeSpan interval, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        // Ownership before failure handling: wrong actor must not mutate another actor's job.
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
            && x.TenantId == context.TenantId && x.ActorId == context.ActorId && x.LeaseOwner == worker
            && x.Fence == fence && x.LeaseUntilUtcTicks > now && x.Kind == (int)SourceControlOperationKind.Commit
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling),
            cancellationToken) ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        CommitReceipt? completedReceipt = null;
        async Task<bool> RenewAsync(CancellationToken token)
        {
            await using var heartbeat = scopes.CreateAsyncScope();
            return await heartbeat.ServiceProvider.GetRequiredService<PersistentSourceControlStore>()
                .RenewLeaseAsync(context.TenantId, id, worker, fence, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), token);
        }
        try
        {
            // A claim may have little time remaining. Renew before any execution, not
            // only after the first timer tick; an expired/stolen claim never starts work.
            if (!await RenewAsync(cancellationToken))
                throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
            var executor = scope.ServiceProvider.GetRequiredService<SourceControlCommitExecutor>();
            var receipt = await SourceControlLeaseRunner.RunAsync(
                async token => completedReceipt = prepared is null
                    ? await executor.ExecuteAsync(context, id, worker, fence, resolveCurrentRoles, token)
                    : await executor.ExecutePreparedAsync(context, id, worker, fence, resolveCurrentRoles, prepared, token),
                RenewAsync, interval, cancellationToken);
            return new(SourceControlOperationState.CommittedLocal, receipt, null);
        }
        catch (Exception exception)
        {
            // Fresh scope after a failed transaction; never leak provider diagnostics.
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await using var finish = scopes.CreateAsyncScope();
                var store = finish.ServiceProvider.GetRequiredService<PersistentSourceControlStore>();
                // Completion clears the lease. A concurrent heartbeat can therefore see false
                // AFTER the durable commit finished; only an exact persisted receipt proves success.
                if (completedReceipt is not null && await store.ConfirmCompletedCommitAsync(context, id, fence, completedReceipt, deadline.Token))
                    return new(SourceControlOperationState.CommittedLocal, completedReceipt, null);
                var intent = await store.ReadSavedResultAsync(context.TenantId, id, worker, fence, DateTimeOffset.UtcNow, deadline.Token);
                var code = exception is SourceControlSecurityException safe ? safe.Code
                    : exception is OperationCanceledException ? SourceControlErrorCode.Cancelled : SourceControlErrorCode.ProviderUnavailable;
                var state = code == SourceControlErrorCode.RevisionConflict ? SourceControlOperationState.Conflict
                    : intent is not null || claim.State == (int)SourceControlOperationState.Reconciling || code == SourceControlErrorCode.ResultUnknown
                        ? SourceControlOperationState.ResultUnknown
                        : exception is OperationCanceledException ? SourceControlOperationState.Cancelled : SourceControlOperationState.Failed;
                if (!await store.FinishAsync(context.TenantId, id, worker, fence, state, DateTimeOffset.UtcNow, deadline.Token, code))
                    return new(SourceControlOperationState.ResultUnknown, null, SourceControlErrorCode.ResultUnknown);
                return new(state, null, code);
            }
            catch
            {
                // Lease lost, storage unavailable, or uncertain DB finish: maintenance reconciles.
                return new(SourceControlOperationState.ResultUnknown, null, SourceControlErrorCode.ResultUnknown);
            }
        }
    }
}
