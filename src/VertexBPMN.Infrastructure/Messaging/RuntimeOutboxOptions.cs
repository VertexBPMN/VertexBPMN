namespace VertexBPMN.Infrastructure.Messaging;

public sealed class RuntimeOutboxOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Disabled";
    public string? ConnectionString { get; set; }
    public string Destination { get; set; } = "vertexbpmn-runtime";
    public int BatchSize { get; set; } = 50;
    public int PollIntervalMilliseconds { get; set; } = 1_000;
    public int LeaseSeconds { get; set; } = 30;
    public int RetryDelaySeconds { get; set; } = 5;
    public int MaxAttempts { get; set; } = 10;
}
