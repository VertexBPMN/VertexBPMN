using System.IO.Pipes;
using System.Text;
using VertexBPMN.SourceControl.Security;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>One local-key prompt, one response, restricted to the service account and lease lifetime.</summary>
internal sealed class GitSshPassphraseChannel : IAsyncDisposable
{
	private readonly NamedPipeServerStream pipe;
	private readonly CancellationTokenSource stop;
	private readonly Task serve;
	internal string Name { get; } = "vertex-source-control-ssh-" + Guid.NewGuid().ToString("N");
	internal GitSshPassphraseChannel(string keyFile, GitSshCredentialLease lease, CancellationToken cancellationToken)
	{
		stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		stop.CancelAfter(lease.ExpiresAt - DateTimeOffset.UtcNow - TimeSpan.FromSeconds(30));
		pipe = new(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
		serve = ServeAsync(keyFile, lease);
	}

	private async Task ServeAsync(string keyFile, GitSshCredentialLease lease)
	{
		try
		{
			await pipe.WaitForConnectionAsync(stop.Token);
			using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
			await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
			var prompt = await BoundedProtocol.ReadLineAsync(reader, 512, stop.Token);
			var path = keyFile.Replace('\\', '/');
			// OpenSSH bounds the local identity filename in this prompt to 100 bytes.
			var bytes = Encoding.UTF8.GetBytes(path);
			var displayed = Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 100)));
			if (prompt == "Enter passphrase for key '" + displayed + "': ")
			{
				await lease.WritePassphraseAsync(writer, stop.Token);
			}
		}
		catch (OperationCanceledException) when (stop.IsCancellationRequested)
		{
			// Lease expiry or operation shutdown closes this one-use channel.
		}
		catch { /* A failed channel never emits secret-bearing diagnostics. */ }
		finally
		{
			if (pipe.IsConnected)
			{
				pipe.Disconnect();
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		await stop.CancelAsync();
		await serve.ConfigureAwait(false);
		await pipe.DisposeAsync();
		stop.Dispose();
	}
}
