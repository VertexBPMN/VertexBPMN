using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

public sealed class InspectorApiTests : IClassFixture<InspectorApiFactory>
{
    private readonly InspectorApiFactory _factory;

    public InspectorApiTests(InspectorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousRequest_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/inspector/process-instance/{Guid.NewGuid()}/state");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownInstance_ReturnsNotFoundInsteadOfDemoState()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "inspector-reader");
        client.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-a");

        var response = await client.GetAsync($"/api/inspector/process-instance/{Guid.NewGuid()}/state");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InstanceState_IsTenantScopedAndReturnsPersistedState()
    {
        var instanceId = await SeedInstanceAsync("tenant-a");

        using var otherTenantClient = _factory.CreateClient();
        otherTenantClient.DefaultRequestHeaders.Add("X-Test-User", "inspector-reader");
        otherTenantClient.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-b");

        var forbidden = await otherTenantClient.GetAsync($"/api/inspector/process-instance/{instanceId}/state");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var owningTenantClient = _factory.CreateClient();
        owningTenantClient.DefaultRequestHeaders.Add("X-Test-User", "inspector-reader");
        owningTenantClient.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-a");

        var response = await owningTenantClient.GetAsync($"/api/inspector/process-instance/{instanceId}/state");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("tenant-a", body, StringComparison.Ordinal);
        Assert.Contains("persisted-state", body, StringComparison.Ordinal);
    }

    private async Task<Guid> SeedInstanceAsync(string tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var definition = await db.ProcessDefinitions.FirstAsync();
        var instance = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            ProcessDefinitionId = definition.Id,
            TenantId = tenantId,
            State = "persisted-state",
            ProcessId = definition.Key,
            StartedAt = DateTime.UtcNow
        };

        await scope.ServiceProvider
            .GetRequiredService<IProcessInstanceRepository>()
            .AddAsync(instance);

        return instance.Id;
    }
}

public sealed class InspectorApiFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            //services.AddAuthentication("Test")
            //    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        });
    }
}