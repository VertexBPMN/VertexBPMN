using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Applications;

/// <summary>
/// LC07 S1: drives the REAL ApplicationController + persistence so the Studio
/// application metadata + designer foundation can list/create/save/get a draft.
/// </summary>
public sealed class ApplicationListApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string TenantA = "tenant-a";

    private readonly HttpClient _client;

    public ApplicationListApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        _client.Timeout = TimeSpan.FromSeconds(30);
    }

    [Fact]
    public async Task List_Create_Get_Save_Roundtrip_ThroughRealControllerAndPersistence()
    {
        var key = $"lc07s1r_{Guid.NewGuid():N}";
        const string content = "{\"localizations\":{},\"pages\":[]}";

        // (1) Create a draft through the real controller + persistence.
        using var createdResponse = await _client.PostAsJsonAsync("/api/applications", new
        {
            tenantId = TenantA,
            key,
            name = "LC07 designer draft",
            expectedVersion = 0,
            content
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<ApplicationDefinitionMetadata>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal(key, created.Key);
        Assert.Equal(1, created.Version);
        Assert.Equal(TenantA, created.TenantId);

        // (2) LIST must include the new draft.
        using var listResponse = await _client.GetAsync($"/api/applications?tenantId={TenantA}", TestContext.Current.CancellationToken);
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<List<ApplicationDefinitionSummary>>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(list);
        Assert.Contains(list, summary => summary.Id == created.Id && summary.Key == key && summary.Name == "LC07 designer draft" && summary.Version == 1);

        // (3) SAVE a new content revision (expectedVersion 1 -> 2).
        const string edited = "{\"localizations\":{},\"pages\":[{\"id\":\"home\",\"route\":\"/home\",\"components\":[{\"type\":\"text\",\"version\":\"1.0.0\"}]}]}";
        var saveResponse = await _client.PutAsJsonAsync($"/api/applications/{created.Id}", new
        {
            tenantId = TenantA,
            key,
            name = "LC07 designer draft",
            expectedVersion = 1,
            content = edited
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, saveResponse.StatusCode);

        // (4) GET (its latest/draft content) reflects the saved revision.
        var getResponse = await _client.GetAsync($"/api/applications/{created.Id}?tenantId={TenantA}", TestContext.Current.CancellationToken);
        getResponse.EnsureSuccessStatusCode();
        var draft = await getResponse.Content.ReadFromJsonAsync<ApplicationDefinitionMetadata>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(draft);
        Assert.Equal(2, draft.Version);
        Assert.Equal(edited, draft.Content);
    }
}
