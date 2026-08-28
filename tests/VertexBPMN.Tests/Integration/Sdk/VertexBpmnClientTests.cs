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

        var capabilities = await client.GetEngineCapabilitiesAsync(CancellationToken.None);

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

        var capabilities = await client.GetEngineCapabilitiesAsync(CancellationToken.None);

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
        }, CancellationToken.None);
        deploy.EnsureSuccessStatusCode();

        var client = new VertexBpmnClient(httpClient);
        var created = await client.CreateWorkflowTriggerAsync("SDK trigger", key, null,CancellationToken.None);
        Assert.NotNull(created);

        var triggers = await client.ListWorkflowTriggersAsync(null, CancellationToken.None);
        Assert.Contains(triggers, trigger => trigger.Id == created!.Trigger.Id);

        var instance = await client.InvokeWorkflowTriggerAsync(created!.Trigger.Id, created.Secret,null, null, CancellationToken.None);
        Assert.NotNull(instance);
    }

    [Fact]
    public async Task ValidateBpmnAsync_AndStartTestRunAsync_AreAvailableThroughSdk()
    {
        using var factory = new CustomWebApplicationFactory();
        using var httpClient = factory.CreateClient();
        var client = new VertexBpmnClient(httpClient);
        var key = $"sdk-test-run-{Guid.NewGuid():N}";
        var bpmnXml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{key}'><startEvent id='start'/><endEvent id='end'/></process></definitions>";

        var validation = await client.ValidateBpmnAsync(bpmnXml, CancellationToken.None);
        var testRun = await client.StartTestRunAsync(bpmnXml, $"{key}.bpmn", new Dictionary<string, object?> { ["source"] = "sdk" }, null, CancellationToken.None);

        Assert.NotNull(validation);
        Assert.True(validation!.IsValid);
        Assert.Equal(key, testRun.Definition.Key);
        Assert.Equal(key, testRun.Instance.ProcessDefinitionKey);
    }

}
