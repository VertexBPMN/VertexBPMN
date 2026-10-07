using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class LeaseRunnerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_or_failed_heartbeat_cancels_execution_and_reports_unknown(bool providerFailure)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var cancelled = false;
        var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => SourceControlLeaseRunner.RunAsync(
            async token =>
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                finally { cancelled = token.IsCancellationRequested; }
                return 1;
            }, _ => providerFailure ? throw new InvalidOperationException("private provider details") : Task.FromResult(false),
            TimeSpan.FromMilliseconds(20), deadline.Token));
        Assert.True(cancelled);
        Assert.Equal(SourceControlErrorCode.ResultUnknown, error.Code);
        Assert.DoesNotContain("private provider details", error.ToString());
    }

    [Fact]
    public async Task Successful_renewal_allows_completion_and_stops_heartbeat()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var renewed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken heartbeatToken = default;
        var result = await SourceControlLeaseRunner.RunAsync(async token =>
        {
            await renewed.Task.WaitAsync(token);
            return 42;
        }, token =>
        {
            heartbeatToken = token;
            renewed.TrySetResult();
            return Task.FromResult(true);
        }, TimeSpan.FromMilliseconds(20), deadline.Token);
        Assert.Equal(42, result);
        Assert.True(heartbeatToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Caller_cancellation_remains_cancellation_not_unknown()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SourceControlLeaseRunner.RunAsync(
            token => Task.FromCanceled<int>(token), _ => Task.FromResult(true), TimeSpan.FromMilliseconds(20), cancelled.Token));
    }
}
