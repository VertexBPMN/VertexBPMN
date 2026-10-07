using System.IO.Pipes;
using System.Text;
using VertexBPMN.SourceControl.Security;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Same-service-account IPC; privileged processes still have access to process memory.</summary>
internal sealed class GitCredentialChannel : IAsyncDisposable
{
    private readonly NamedPipeServerStream _pipe;
    private readonly CancellationTokenSource _stop;
    private readonly Task _serve;
    internal string Name { get; } = $"vertex-source-control-{Guid.NewGuid():N}";

    internal GitCredentialChannel(Uri remote, GitHubTokenLease lease, CancellationToken cancellationToken = default)
    {
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _stop.CancelAfter(TimeSpan.FromMinutes(5));
        _pipe = new(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _serve = ServeAsync(remote, lease);
    }

    private async Task ServeAsync(Uri remote, GitHubTokenLease lease)
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await _pipe.WaitForConnectionAsync(_stop.Token);
                using var reader = new StreamReader(_pipe, new UTF8Encoding(false), leaveOpen: true);
                using var writer = new StreamWriter(_pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                var protocol = await BoundedProtocol.ReadLineAsync(reader, 16, _stop.Token);
                var host = await BoundedProtocol.ReadLineAsync(reader, 256, _stop.Token);
                var path = await BoundedProtocol.ReadLineAsync(reader, 4096, _stop.Token);
                var repositoryPath = remote.AbsolutePath.TrimStart('/').TrimEnd('/');
                // Git canonicalizes smart-HTTP remotes with one terminal slash.
                // Accept that spelling only, never subpaths or a different repository.
                if (protocol == "https" && host == remote.Authority
                    && (path == repositoryPath || path == repositoryPath + "/"))
                {
                    await writer.WriteLineAsync("x-access-token".AsMemory(), _stop.Token);
                    await writer.WriteLineAsync(lease.Authorization().Parameter!.AsMemory(), _stop.Token);
                }
                _pipe.Disconnect();
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch { /* Invalid/expired channels are closed, never logged. */ }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        await _serve.ConfigureAwait(false);
        _pipe.Dispose();
        _stop.Dispose();
    }
}
