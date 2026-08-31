using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace VertexBPMN.Tests.Architecture;

public sealed class AppHostTopologyTests
{
    [Fact]
    public void ProjectMode_ModelsInfrastructureReadinessAndServiceDependencies()
    {
        var builder = DistributedApplication.CreateBuilder();

        VertexBpmnAppHostTopology.ConfigureProjectMode(builder);

        var resources = builder.Resources.ToDictionary(resource => resource.Name);
        Assert.Contains("postgres", resources);
        Assert.Contains("BpmnDbContext", resources);
        Assert.Contains("TenantDbContext", resources);
        Assert.Contains("SimulationScenarioDbContext", resources);
        Assert.Contains("ProcessMiningEvents", resources);
        Assert.Contains("DecisionDbContext", resources);
        Assert.Contains("messaging", resources);
        Assert.Contains("api", resources);
        Assert.Contains("studio", resources);

        var postgres = resources["postgres"];
        var messaging = resources["messaging"];
        var api = resources["api"];
        var studio = resources["studio"];

        Assert.NotEmpty(api.Annotations.OfType<HealthCheckAnnotation>());
        Assert.NotEmpty(studio.Annotations.OfType<HealthCheckAnnotation>());
        AssertWaitsFor(api, postgres);
        AssertWaitsFor(api, messaging);
        AssertWaitsFor(studio, api);
    }

    [Fact]
    public void ContainerMode_ModelsDurableApiAndReadinessBeforeStudio()
    {
        var builder = DistributedApplication.CreateBuilder();

        VertexBpmnAppHostTopology.ConfigureContainerMode(builder);

        var resources = builder.Resources.ToDictionary(resource => resource.Name);
        var api = resources["api"];
        var studio = resources["studio"];

        Assert.IsAssignableFrom<ContainerResource>(api);
        Assert.Contains(
            api.Annotations.OfType<ContainerMountAnnotation>(),
            mount => mount.Target == "/var/lib/vertexbpmn" && !mount.IsReadOnly);
        Assert.NotEmpty(api.Annotations.OfType<HealthCheckAnnotation>());
        Assert.NotEmpty(studio.Annotations.OfType<HealthCheckAnnotation>());
        AssertWaitsFor(studio, api);
    }

    private static void AssertWaitsFor(IResource dependent, IResource dependency) =>
        Assert.Contains(
            dependent.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, dependency)
                    && wait.WaitType == WaitType.WaitUntilHealthy);
}
