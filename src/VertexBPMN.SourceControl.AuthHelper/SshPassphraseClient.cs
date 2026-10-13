using System.IO.Pipes;
using System.Text;
using VertexBPMN.SourceControl.Security;

namespace VertexBPMN.SourceControl.Security;

internal static class SshPassphraseClient
{
	internal static async Task<int> RunAsync(string channel, string[] arguments)
	{
		if (!channel.StartsWith("vertex-source-control-ssh-", StringComparison.Ordinal) || channel.Length > 100
			|| channel.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-')
			|| arguments.Length != 1 || arguments[0].Length > 512 || arguments[0].Any(char.IsControl))
		{
			return 2;
		}
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		try
		{
			await using var pipe = new NamedPipeClientStream(".", channel, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
			await pipe.ConnectAsync(timeout.Token);
			await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
			using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
			await writer.WriteLineAsync(arguments[0].AsMemory(), timeout.Token);
			var passphrase = await BoundedProtocol.ReadLineAsync(reader, 4096, timeout.Token);
			if (string.IsNullOrEmpty(passphrase) || passphrase.Any(char.IsControl))
			{
				return 2;
			}
			await Console.Out.WriteLineAsync(passphrase.AsMemory(), timeout.Token);
			return 0;
		}
		catch
		{
			return 2;
		}
	}
}
