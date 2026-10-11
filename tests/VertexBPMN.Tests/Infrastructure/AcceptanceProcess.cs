using System.Diagnostics;

namespace VertexBPMN.Tests.Infrastructure;

internal static class AcceptanceProcess
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "The callback takes ownership of a successfully started Process. Startup failure kills and disposes it here; acceptance callers stop and dispose it in finally.")]
	public static void Start(ProcessStartInfo startInfo, Action<Process> takeOwnership)
	{
		var process = new Process { StartInfo = startInfo };
		process.OutputDataReceived += (_, args) =>
		{
			if (args.Data is not null)
			{
				Console.WriteLine(args.Data);
			}
		};
		process.ErrorDataReceived += (_, args) =>
		{
			if (args.Data is not null)
			{
				Console.WriteLine("[stderr] " + args.Data);
			}
		};
		var started = false;
		try
		{
			started = process.Start();
			if (!started)
			{
				throw new InvalidOperationException("API-Subprozess konnte nicht gestartet werden.");
			}

			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			takeOwnership(process);
		}
		catch
		{
			StopAndDispose(process, started);
			throw;
		}
	}

	public static void StopAndDispose(Process? process, bool started = true)
	{
		if (process is null)
		{
			return;
		}

		try
		{
			if (started && !process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
		}
		finally
		{
			process.Dispose();
		}
	}
}
