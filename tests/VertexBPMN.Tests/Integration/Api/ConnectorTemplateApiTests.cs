using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

[Collection("IntegratedApi")]
public sealed class ConnectorTemplateApiTests
{
    private readonly HttpClient _client;

    public ConnectorTemplateApiTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output)
        => _client = factory.WithSharedFixture(dbFixture).CreateClient(output);

    [Fact]
    public async Task TemplateLifecycle_IsTenantScoped_AndProtectsMutations()
    {
        var tenantId = $"template-{Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync("/api/connector-templates", Template(tenantId, "HTTP request"), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ConnectorTemplateMetadata>(JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal("http", created!.Runtime);
        Assert.Equal("url", Assert.Single(created.Properties).Key);
        Assert.Equal("GET", Assert.Single(created.Properties).DefaultValue);

        var listed = await _client.GetFromJsonAsync<List<ConnectorTemplateMetadata>>($"/api/connector-templates?tenantId={tenantId}", JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(listed!, item => item.Id == created.Id);

        var update = await _client.PutAsJsonAsync($"/api/connector-templates/{created.Id}", Template(tenantId, "HTTP request v2"), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var otherTenant = await _client.GetAsync($"/api/connector-templates/{created.Id}?tenantId=other-{tenantId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, otherTenant.StatusCode);

        using var denied = new HttpRequestMessage(HttpMethod.Post, "/api/connector-templates") { Content = JsonContent.Create(Template(tenantId, "Forbidden")) };
        denied.Headers.Add("X-Test-User", "reader");
        denied.Headers.Add("X-Test-Tenant", tenantId);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(denied, TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/connector-templates/{created.Id}?tenantId={tenantId}", TestContext.Current.CancellationToken)).StatusCode);
    }

    private static object Template(string tenantId, string name) => new
    {
        tenantId,
        name,
        category = "Communication",
        appliesTo = new[] { "bpmn:ServiceTask" },
        runtime = "http",
        icon = "http",
        properties = new[] { new { key = "url", type = "expression", required = true, @default = "GET", options = (string[]?)null } }
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
