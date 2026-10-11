
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace VertexBPMN.Tests;

internal sealed class XunitLogger(ITestOutputHelper output, string categoryName) : ILogger
{
	private readonly ITestOutputHelper _output = output;
	private readonly string _categoryName = categoryName;

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId,
		TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (exception == null)
		{
			return;
		}

		try
		{
			_output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
			_output.WriteLine($"Exception: {exception.Message}");
		}
		catch (InvalidOperationException)
		{
			// A shared test host can log after the owning test has completed.
			// Logging must never turn that expected host lifecycle into a test failure.
		}
	}
}
