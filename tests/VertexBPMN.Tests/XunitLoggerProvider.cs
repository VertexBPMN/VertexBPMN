
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace VertexBPMN.Tests;

internal sealed class XunitLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
	private readonly ITestOutputHelper _output = output;

	public ILogger CreateLogger(string categoryName) => new XunitLogger(_output, categoryName);

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "ILoggerFactory owns registered providers and disposes them with the factory. Registration failure disposes the provider here.")]
	public static void Register(ILoggerFactory factory, ITestOutputHelper output)
	{
		var provider = new XunitLoggerProvider(output);
		try
		{
			factory.AddProvider(provider);
		}
		catch
		{
			provider.Dispose();
			throw;
		}
	}

	public void Dispose() { }
}
