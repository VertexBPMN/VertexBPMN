namespace VertexBPMN.Tests;

using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

public static class TestUtils
{
    public static async Task<HttpResponseMessage> WaitForEndpointAsync(HttpClient client, string relativeUrl, int maxRetries = 10, int delayMs = 500)
    {
        AsyncRetryPolicy<HttpResponseMessage> policy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(maxRetries, _ => TimeSpan.FromMilliseconds(delayMs));

        return await policy.ExecuteAsync(() => client.GetAsync(relativeUrl));
    }
}

public class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName) => new XunitLogger(_output, categoryName);

    public void Dispose() { }
}

public class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) => null!;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId,
        TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (exception != null)
        {
            _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
            _output.WriteLine($"Exception: {exception.Message}");
        }
    }
}
