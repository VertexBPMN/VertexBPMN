using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure;
using VertexBPMN.Infrastructure.Messaging;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class Phase2ProductionConfigurationTests
{
    [Fact]
    public void Production_rejects_missing_durable_database_configuration()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddAllEngineDbContexts(configuration));

        Assert.Contains("durable connection string", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_local_dependency_registry_fallback()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Production",
            ["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "vertexbpmn-test-keys")
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddBpmnPersistenceServices(configuration));

        Assert.Contains("DependencyRegistry", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_registrations_are_persistent_implementations()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OperationalMode"] = "Development"
        });
        var services = new ServiceCollection();

        services.AddBpmnPersistenceServices(configuration);
        services.AddEngineServices(configuration);

        Assert.Equal(typeof(PersistentMessageDispatcher),
            services.Last(descriptor => descriptor.ServiceType == typeof(IMessageDispatcher)).ImplementationType);
        Assert.Equal(typeof(PersistentWorkerNodeManager),
            services.Last(descriptor => descriptor.ServiceType == typeof(IWorkerNodeManager)).ImplementationType);
    }

    [Fact]
    public async Task Connector_policy_blocks_private_and_non_allowlisted_destinations()
    {
        var privatePolicy = new ConnectorDestinationPolicy(BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectorRuntime:AllowedHttpHosts:0"] = "127.0.0.1"
        }));
        var privateContext = Context(new Uri("http://127.0.0.1/internal"));
        var privateException = await Assert.ThrowsAsync<ServiceTaskExecutionException>(() =>
            privatePolicy.ValidateAsync(privateContext, TestContext.Current.CancellationToken));
        Assert.Contains("private", privateException.Message, StringComparison.OrdinalIgnoreCase);

        var emptyPolicy = new ConnectorDestinationPolicy(BuildConfiguration([]));
        var allowlistException = await Assert.ThrowsAsync<ServiceTaskExecutionException>(() =>
            emptyPolicy.ValidateAsync(Context(new Uri("https://example.com/hook")), TestContext.Current.CancellationToken));
        Assert.Contains("not allowlisted", allowlistException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Connector_policy_parses_sql_server_tcp_host_before_network_validation()
    {
        var policy = new ConnectorDestinationPolicy(BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectorRuntime:AllowedDatabaseProviders:0"] = "sqlserver",
            ["ConnectorRuntime:AllowedDatabaseHosts:0"] = "127.0.0.1"
        }));
        var context = new ConnectorExecutionContext(
            "tenant-a",
            "sqlserver",
            Guid.NewGuid().ToString("N"),
            null,
            new Dictionary<string, string> { ["vertex:connector.provider"] = "sqlserver" },
            new Dictionary<string, object>(),
            new ConnectorRetryPolicy(),
            CredentialSecret: "Server=tcp:127.0.0.1,1433;Database=workflow;User Id=test;Password=test");

        var exception = await Assert.ThrowsAsync<ServiceTaskExecutionException>(() =>
            policy.ValidateAsync(context, TestContext.Current.CancellationToken));

        Assert.Contains("private", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ConnectorExecutionContext Context(Uri endpoint) => new(
        "tenant-a",
        "http",
        Guid.NewGuid().ToString("N"),
        endpoint,
        new Dictionary<string, string>(),
        new Dictionary<string, object>(),
        new ConnectorRetryPolicy());

    private static IConfiguration BuildConfiguration(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
