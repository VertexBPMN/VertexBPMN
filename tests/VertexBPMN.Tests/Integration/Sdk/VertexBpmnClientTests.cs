using System.Net.Http.Json;
using VertexBPMN.Sdk;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Sdk;

public sealed class VertexBpmnClientTests
{
    [Fact]
    public async Task GetEngineCapabilitiesAsync_SimpleApi_ReturnsSimpleCapabilities()
    {
        using var factory = new CustomWebApplicationFactory().WithEngineType("Simple");
        using var httpClient = factory.CreateClient();
        var client = new VertexBpmnClient(httpClient, new VertexBpmnClientOptions
        {
            ExpectedEngineType = VertexBpmnEngineType.Simple
        });

        var capabilities = await client.GetEngineCapabilitiesAsync();

        Assert.Equal(VertexBpmnEngineType.Simple, capabilities.EngineType);
        Assert.True(capabilities.SupportsCmmn);
        Assert.False(capabilities.SupportsWorkers);
        Assert.False(capabilities.SupportsDurablePersistence);
    }

    [Fact]
    public async Task GetEngineCapabilitiesAsync_DistributedApi_ReturnsDistributedCapabilities()
    {
        using var factory = new CustomWebApplicationFactory().WithEngineType("Distributed");
        using var httpClient = factory.CreateClient();
        var client = new VertexBpmnClient(httpClient, new VertexBpmnClientOptions
        {
            ExpectedEngineType = VertexBpmnEngineType.Distributed
        });

        var capabilities = await client.GetEngineCapabilitiesAsync();

        Assert.Equal(VertexBpmnEngineType.Distributed, capabilities.EngineType);
        Assert.True(capabilities.SupportsCmmn);
        Assert.True(capabilities.SupportsWorkers);
        Assert.True(capabilities.SupportsDurablePersistence);
    }

    [Fact]
    public async Task WorkflowTriggerLifecycle_IsAvailableThroughSdk()
    {
        using var factory = new CustomWebApplicationFactory();
        using var httpClient = factory.CreateClient();
        var key = $"sdk-trigger-{Guid.NewGuid():N}";
        var deploy = await httpClient.PostAsJsonAsync("/api/repository", new
        {
            bpmnXml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{key}'><startEvent id='start'/><endEvent id='end'/></process></definitions>",
            name = $"{key}.bpmn",
            tenantId = (string?)null
        });
        deploy.EnsureSuccessStatusCode();

        var client = new VertexBpmnClient(httpClient);
        var created = await client.CreateWorkflowTriggerAsync("SDK trigger", key);
        Assert.NotNull(created);

        var triggers = await client.ListWorkflowTriggersAsync();
        Assert.Contains(triggers, trigger => trigger.Id == created!.Trigger.Id);

        var instance = await client.InvokeWorkflowTriggerAsync(created!.Trigger.Id, created.Secret);
        Assert.NotNull(instance);
    }

}
