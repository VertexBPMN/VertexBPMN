using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Observability;

internal sealed class TestLogger<T> : ILogger<T>
{
	public List<LogEntry> LogEntries { get; } = [];

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		LogEntries.Add(new LogEntry(logLevel, formatter(state, exception), state?.ToString()));
	}
}
