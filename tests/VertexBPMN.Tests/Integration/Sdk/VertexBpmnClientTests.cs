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
}