using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

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

    [Fact]
    public async Task ExternalServicesMode_ModelsExternalConnectionsWithoutContainerResources()
    {
        var builder = DistributedApplication.CreateBuilder();

        VertexBpmnAppHostTopology.ConfigureExternalServicesMode(builder);

        var resources = builder.Resources.ToDictionary(resource => resource.Name);
        Assert.Contains("BpmnDbContext", resources);
        Assert.Contains("TenantDbContext", resources);
        Assert.Contains("SimulationScenarioDbContext", resources);
        Assert.Contains("ProcessMiningEvents", resources);
        Assert.Contains("DecisionDbContext", resources);
        Assert.Contains("messaging", resources);
        Assert.Contains("api", resources);
        Assert.Contains("studio", resources);
        Assert.DoesNotContain(resources.Values, resource => resource is ContainerResource);

        var api = resources["api"];
        var studio = resources["studio"];
        Assert.NotEmpty(api.Annotations.OfType<HealthCheckAnnotation>());
        Assert.NotEmpty(studio.Annotations.OfType<HealthCheckAnnotation>());
        AssertWaitsFor(studio, api);

        var apiEnvironment = await ResolveEnvironmentAsync(api);
        var studioEnvironment = await ResolveEnvironmentAsync(studio);
        Assert.Equal("Development", apiEnvironment["ASPNETCORE_ENVIRONMENT"]);
        Assert.Equal("Development", apiEnvironment["DOTNET_ENVIRONMENT"]);
        Assert.Equal("Development", apiEnvironment["OperationalMode"]);
        Assert.Equal("Development", studioEnvironment["ASPNETCORE_ENVIRONMENT"]);
        Assert.Equal("true", studioEnvironment["StudioAuthentication__LocalDevelopmentEnabled"]);
        Assert.DoesNotContain("StudioAuthentication__Authority", studioEnvironment.Keys);
        Assert.DoesNotContain("oidcStudioClientSecret", resources.Keys);
    }

    [Fact]
    public async Task ExternalServicesOidcTestMode_ModelsProviderNeutralOidcAndSecretReference()
    {
        const string authority = "http://localhost:58080/realms/vertexbpmn";
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VertexBPMN:AuthenticationMode"] = "OidcTest",
            ["VertexBPMN:Oidc:Authority"] = authority,
            ["Parameters:oidcStudioClientSecret"] = "test-only-secret"
        });

        VertexBpmnAppHostTopology.ConfigureExternalServicesMode(builder);

        var resources = builder.Resources.ToDictionary(resource => resource.Name);
        var apiEnvironment = await ResolveEnvironmentAsync(resources["api"]);
        var studioEnvironment = await ResolveEnvironmentAsync(resources["studio"]);
        var secret = Assert.IsType<ParameterResource>(resources["oidcStudioClientSecret"]);

        Assert.True(secret.Secret);
        Assert.Equal("OidcTest", apiEnvironment["ASPNETCORE_ENVIRONMENT"]);
        Assert.Equal("OidcTest", apiEnvironment["DOTNET_ENVIRONMENT"]);
        Assert.Equal("OidcTest", apiEnvironment["OperationalMode"]);
        Assert.Equal(authority, apiEnvironment["Jwt__Authority"]);
        Assert.Equal(authority, apiEnvironment["Jwt__Issuer"]);
        Assert.Equal("vertexbpmn-api", apiEnvironment["Jwt__Audience"]);
        Assert.Equal("false", apiEnvironment["Jwt__RequireHttpsMetadata"]);
        Assert.Equal("false", apiEnvironment["Jwt__UseDevelopmentApiKey"]);

        Assert.Equal("OidcTest", studioEnvironment["ASPNETCORE_ENVIRONMENT"]);
        Assert.Equal("OidcTest", studioEnvironment["DOTNET_ENVIRONMENT"]);
        Assert.Equal(authority, studioEnvironment["StudioAuthentication__Authority"]);
        Assert.Equal("vertexbpmn-studio", studioEnvironment["StudioAuthentication__ClientId"]);
        Assert.Equal("vertexbpmn-claims", studioEnvironment["StudioAuthentication__ClaimsScope"]);
        Assert.Equal("true", studioEnvironment["StudioAuthentication__RequireClientSecret"]);
        Assert.Equal("false", studioEnvironment["StudioAuthentication__RequireHttpsMetadata"]);
        Assert.Equal("false", studioEnvironment["StudioAuthentication__LocalDevelopmentEnabled"]);
        Assert.Equal("false", studioEnvironment["StudioAuthentication__UiTestEnabled"]);

        var secretEnvironmentValue = studioEnvironment["StudioAuthentication__ClientSecret"];
        Assert.IsAssignableFrom<IValueProvider>(secretEnvironmentValue);
        Assert.DoesNotContain("test-only-secret", secretEnvironmentValue.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://identity.example.com/realms/vertexbpmn")]
    [InlineData("http://identity.example.com/realms/vertexbpmn")]
    [InlineData("not-a-uri")]
    public void ExternalServicesOidcTestMode_RejectsNonLocalAuthority(string authority)
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VertexBPMN:AuthenticationMode"] = "OidcTest",
            ["VertexBPMN:Oidc:Authority"] = authority,
            ["Parameters:oidcStudioClientSecret"] = "test-only-secret"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => VertexBpmnAppHostTopology.ConfigureExternalServicesMode(builder));

        Assert.Contains("HTTP loopback URI", exception.Message, StringComparison.Ordinal);
    }

    private static void AssertWaitsFor(IResource dependent, IResource dependency) =>
        Assert.Contains(
            dependent.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, dependency)
                    && wait.WaitType == WaitType.WaitUntilHealthy);

    private static async Task<Dictionary<string, object>> ResolveEnvironmentAsync(IResource resource)
    {
        var environment = new Dictionary<string, object>();
        var executionContext = new DistributedApplicationExecutionContext(
            DistributedApplicationOperation.Run);
        var callbackContext = new EnvironmentCallbackContext(
            executionContext,
            resource,
            environment,
            CancellationToken.None);

        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(callbackContext);
        }

        return environment;
    }
}
