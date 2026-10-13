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

    internal GitCredentialChannel(Uri remote, IGitCredentialLease lease, CancellationToken cancellationToken = default)
    {
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _stop.CancelAfter(TimeSpan.FromMinutes(5));
        _pipe = new(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _serve = ServeAsync(remote, lease);
    }

    private async Task ServeAsync(Uri remote, IGitCredentialLease lease)
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await _pipe.WaitForConnectionAsync(_stop.Token);
                using var reader = new StreamReader(_pipe, new UTF8Encoding(false), leaveOpen: true);
				await using var writer = new StreamWriter(_pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                var protocol = await BoundedProtocol.ReadLineAsync(reader, 16, _stop.Token);
                var host = await BoundedProtocol.ReadLineAsync(reader, 256, _stop.Token);
                var path = await BoundedProtocol.ReadLineAsync(reader, 4096, _stop.Token);
				var bearerAllowed = lease is not GitHttpBearerCredentialLease
					|| await BoundedProtocol.ReadLineAsync(reader, 64, _stop.Token) == "capability=authtype";
                var repositoryPath = remote.AbsolutePath.TrimStart('/').TrimEnd('/');
                // Git canonicalizes smart-HTTP remotes with one terminal slash.
                // Accept that spelling only, never subpaths or a different repository.
                if (bearerAllowed && protocol == "https" && host == remote.Authority
                    && (path == repositoryPath || path == repositoryPath + "/"))
                {
                    await lease.WriteCredentialAsync(remote, writer, _stop.Token);
                }
                _pipe.Disconnect();
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // Expected shutdown path: the channel is being disposed.
        }
        catch { /* Invalid/expired channels are closed, never logged. */ }
		finally
		{
			// A rejected/expired lease must close the connected client immediately,
			// rather than leave the helper waiting for its own timeout.
			if (_pipe.IsConnected)
			{
				_pipe.Disconnect();
			}
		}
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _serve.ConfigureAwait(false);
        await _pipe.DisposeAsync();
        _stop.Dispose();
    }
}
