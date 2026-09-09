using System.Net.Http.Json;
using VertexBPMN.Sdk;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Sdk;

public sealed class VertexBpmnClientTests
{
    [Fact]
    public async Task Sdk_CannotOverrideTenantOrBypassMutationRole()
    {
        using var factory = new CustomWebApplicationFactory();
        using var adminHttp = factory.CreateClient();
        var admin = new VertexBpmnClient(adminHttp);
        var ct = TestContext.Current.CancellationToken;
        var key = "sdk-isolation-" + Guid.NewGuid().ToString("N");
        var xml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{key}'><startEvent id='s'/><sequenceFlow id='f' sourceRef='s' targetRef='e'/><endEvent id='e'/></process></definitions>";
        var deployed = await admin.DeployProcessAsync(xml, key, "tenant-a", ct);
        Assert.NotNull(deployed);
        using var readerHttp = factory.CreateClient();
        readerHttp.DefaultRequestHeaders.Add("X-Test-User", "reader");
        readerHttp.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-b");
        var reader = new VertexBpmnClient(readerHttp);
        Assert.Empty(await reader.ListProcessDefinitionsAsync(key, "tenant-a", ct));
        var foreign = await Assert.ThrowsAsync<HttpRequestException>(() => reader.GetProcessDefinitionAsync(Guid.Parse(deployed.Id), ct));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, foreign.StatusCode);
        var mutation = await Assert.ThrowsAsync<HttpRequestException>(() => reader.DeployProcessAsync(xml, key, "tenant-b", ct));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, mutation.StatusCode);
    }

    [Fact]
    public async Task GetEngineCapabilitiesAsync_SimpleApi_ReturnsSimpleCapabilities()
    {
        using var factory = new CustomWebApplicationFactory().WithEngineType("Simple");
        using var httpClient = factory.CreateClient();
        var client = new VertexBpmnClient(httpClient, new VertexBpmnClientOptions
        {
            ExpectedEngineType = VertexBpmnEngineType.Simple
        });

        var capabilities = await client.GetEngineCapabilitiesAsync(TestContext.Current.CancellationToken);

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

        var capabilities = await client.GetEngineCapabilitiesAsync(TestContext.Current.CancellationToken);

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
bpmnXml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{key}'><startEvent id='start'/><sequenceFlow id='start-end' sourceRef='start' targetRef='end'/><endEvent id='end'/></process></definitions>",
            name = $"{key}.bpmn",
            tenantId = (string?)null
        }, TestContext.Current.CancellationToken);
        deploy.EnsureSuccessStatusCode();

        var client = new VertexBpmnClient(httpClient);
        var created = await client.CreateWorkflowTriggerAsync("SDK trigger", key, null,TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        var triggers = await client.ListWorkflowTriggersAsync(null, TestContext.Current.CancellationToken);
        Assert.Contains(triggers, trigger => trigger.Id == created!.Trigger.Id);

        var instance = await client.InvokeWorkflowTriggerAsync(created!.Trigger.Id, created.Secret,null, null, TestContext.Current.CancellationToken);
        Assert.NotNull(instance);
    }

    [Fact]
    public async Task ValidateBpmnAsync_AndStartTestRunAsync_AreAvailableThroughSdk()
    {
        using var factory = new CustomWebApplicationFactory();
        using var httpClient = factory.CreateClient();
        var client = new VertexBpmnClient(httpClient);
        var key = $"sdk-test-run-{Guid.NewGuid():N}";
        var bpmnXml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{key}'><startEvent id='start'/><sequenceFlow id='start-end' sourceRef='start' targetRef='end'/><endEvent id='end'/></process></definitions>";

        var validation = await client.ValidateBpmnAsync(bpmnXml, TestContext.Current.CancellationToken);
        var testRun = await client.StartTestRunAsync(bpmnXml, $"{key}.bpmn", new Dictionary<string, object?> { ["source"] = "sdk" }, null, TestContext.Current.CancellationToken);

        Assert.NotNull(validation);
        Assert.True(validation!.IsValid);
        Assert.Equal(key, testRun.Definition.Key);
        Assert.Equal(key, testRun.Instance.ProcessDefinitionKey);
    }

}
