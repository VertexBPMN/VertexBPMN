namespace VertexBPMN.Infrastructure.Messaging;

public enum RuntimeOutboxEntityType
{
    Topic,
    Queue
}

public enum RuntimeOutboxAuthenticationMode
{
    ManagedIdentity,
    ConnectionString
}

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

    // Azure Service Bus specific options (required when Provider = "AzureServiceBus").
    /// <summary>e.g. "yournamespace.servicebus.windows.net" (used with Managed Identity).</summary>
    public string? FullyQualifiedNamespace { get; set; }
    /// <summary>Topic or Queue name to publish to.</summary>
    public string? EntityName { get; set; }
    public RuntimeOutboxEntityType EntityType { get; set; } = RuntimeOutboxEntityType.Topic;
    public RuntimeOutboxAuthenticationMode AuthenticationMode { get; set; } = RuntimeOutboxAuthenticationMode.ManagedIdentity;
    /// <summary>Optional User-Assigned Managed Identity client id used by the Azure transport.</summary>
    public string? ManagedIdentityClientId { get; set; }
    /// <summary>Seconds allowed for a single publish round-trip before it is treated as a transport timeout.</summary>
    public int OperationTimeoutSeconds { get; set; } = 60;
}
