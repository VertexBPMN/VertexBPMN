using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>
/// Bounds one claimed execution by its lease. The heartbeat callback must use a separate
/// scoped store/DbContext: EF contexts must not be shared with the executing job.
/// Does not claim work, resolve identities, or classify uncertain write outcomes.
/// </summary>
internal static class SourceControlLeaseRunner
{
    internal static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> execute,
        Func<CancellationToken, Task<bool>> renew, TimeSpan interval, CancellationToken cancellationToken)
    {
        if (interval <= TimeSpan.Zero || interval > TimeSpan.FromMinutes(1))
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var stopHeartbeat = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var leaseLost = false;
        async Task HeartbeatAsync()
        {
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(stopHeartbeat.Token))
                {
                    if (await renew(stopHeartbeat.Token)) continue;
                    leaseLost = true;
                    execution.Cancel();
                    return;
                }
            }
            catch (OperationCanceledException) when (stopHeartbeat.IsCancellationRequested) { }
            catch
            {
                // Provider failures may contain secrets. Fail closed without exposing them.
                leaseLost = true;
                execution.Cancel();
            }
        }
        var heartbeat = HeartbeatAsync();
        try
        {
            var result = await execute(execution.Token);
            await stopHeartbeat.CancelAsync();
            await heartbeat;
            cancellationToken.ThrowIfCancellationRequested();
            if (leaseLost) throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
            return result;
        }
        catch (OperationCanceledException) when (leaseLost && !cancellationToken.IsCancellationRequested)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
        }
        finally
        {
            await stopHeartbeat.CancelAsync();
            await heartbeat;
        }
    }
}
