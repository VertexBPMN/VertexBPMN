namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class GitSshAuthenticationFileRecovery
{
	internal async Task<int> PruneAsync(DateTimeOffset now, CancellationToken cancellationToken)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(TimeSpan.FromSeconds(10));
		var removed = 0;
		try
		{
			foreach (var directory in Directory.EnumerateDirectories(Path.GetTempPath(), GitSshAuthenticationPath.Prefix + "*", SearchOption.TopDirectoryOnly))
			{
				deadline.Token.ThrowIfCancellationRequested();
				if (await PruneDirectoryAsync(directory, now, deadline.Token) && ++removed == 32)
				{
					break;
				}
			}
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			// Time-bounded maintenance resumes on the next tick; invalid proofs never authorize deletion.
		}
		return removed;
	}
}
