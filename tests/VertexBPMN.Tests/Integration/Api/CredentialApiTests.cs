using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

[Collection("IntegratedApi")]
public sealed class CredentialApiTests
{
    private const string InitialSecret = "plain-secret-value-123";
    private const string RotatedSecret = "rotated-secret-value-456";
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CredentialApiTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output)
    {
        _factory = factory.WithSharedFixture(dbFixture);
        _client = _factory.CreateClient(output);
    }

    [Fact]
    public async Task CredentialLifecycle_ReturnsMetadataOnly_AndWritesRedactedAudit()
    {
        var tenantId = $"tenant-{Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync("/api/credentials", new
        {
            tenantId,
            name = "Payments API",
            type = "api-key",
            description = "Payment gateway",
            secrets = new Dictionary<string, string> { ["token"] = InitialSecret }
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createJson = await create.Content.ReadAsStringAsync();
        Assert.DoesNotContain(InitialSecret, createJson, StringComparison.Ordinal);
        Assert.DoesNotContain("protectedValues", createJson, StringComparison.OrdinalIgnoreCase);
        var created = JsonSerializer.Deserialize<CredentialDto>(createJson, JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(new[] { "token" }, created!.SecretKeys);

        var list = await _client.GetAsync($"/api/credentials?tenantId={tenantId}");
        list.EnsureSuccessStatusCode();
        var listJson = await list.Content.ReadAsStringAsync();
        Assert.Contains(created.Id, listJson, StringComparison.Ordinal);
        Assert.DoesNotContain(InitialSecret, listJson, StringComparison.Ordinal);
        Assert.DoesNotContain("protectedValues", listJson, StringComparison.OrdinalIgnoreCase);

        var get = await _client.GetAsync($"/api/credentials/{created.Id}?tenantId={tenantId}");
        get.EnsureSuccessStatusCode();
        var getJson = await get.Content.ReadAsStringAsync();
        Assert.DoesNotContain(InitialSecret, getJson, StringComparison.Ordinal);

        var update = await _client.PutAsJsonAsync($"/api/credentials/{created.Id}", new
        {
            tenantId,
            name = "Payments API updated",
            type = "api-key",
            description = "Updated metadata"
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var rotate = await _client.PutAsJsonAsync($"/api/credentials/{created.Id}/secret", new
        {
            tenantId,
            key = "token",
            value = RotatedSecret
        });
        Assert.Equal(HttpStatusCode.NoContent, rotate.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcessMiningEventDbContext>();
            var auditJson = JsonSerializer.Serialize(db.AuditLogs.Where(log => log.TenantId == tenantId).ToList());
            Assert.Contains("credential.created", auditJson, StringComparison.Ordinal);
            Assert.Contains("credential.metadata_updated", auditJson, StringComparison.Ordinal);
            Assert.Contains("credential.secret_rotated", auditJson, StringComparison.Ordinal);
            Assert.DoesNotContain(InitialSecret, auditJson, StringComparison.Ordinal);
            Assert.DoesNotContain(RotatedSecret, auditJson, StringComparison.Ordinal);
        }

        var delete = await _client.DeleteAsync($"/api/credentials/{created.Id}?tenantId={tenantId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var deleted = await _client.GetAsync($"/api/credentials/{created.Id}?tenantId={tenantId}");
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Fact]
    public async Task Credentials_AreTenantIsolated_AndMutationsRequireAdmin()
    {
        var tenantA = $"tenant-a-{Guid.NewGuid():N}";
        var tenantB = $"tenant-b-{Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync("/api/credentials", new
        {
            tenantId = tenantA,
            name = "Tenant A Secret",
            type = "api-key",
            description = (string?)null,
            secrets = new Dictionary<string, string> { ["token"] = InitialSecret }
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<CredentialDto>(JsonOptions);
        Assert.NotNull(created);

        var otherTenant = await _client.GetAsync($"/api/credentials/{created!.Id}?tenantId={tenantB}");
        Assert.Equal(HttpStatusCode.NotFound, otherTenant.StatusCode);

        using var readOnlyRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/credentials?tenantId={tenantA}");
        readOnlyRequest.Headers.Add("X-Test-User", "reader");
        readOnlyRequest.Headers.Add("X-Test-Tenant", tenantA);
        var readOnlyList = await _client.SendAsync(readOnlyRequest);
        Assert.Equal(HttpStatusCode.OK, readOnlyList.StatusCode);

        using var crossTenantRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/credentials?tenantId={tenantB}");
        crossTenantRequest.Headers.Add("X-Test-User", "reader");
        crossTenantRequest.Headers.Add("X-Test-Tenant", tenantA);
        var crossTenantList = await _client.SendAsync(crossTenantRequest);
        Assert.Equal(HttpStatusCode.Forbidden, crossTenantList.StatusCode);

        using var readOnlyCreate = new HttpRequestMessage(HttpMethod.Post, "/api/credentials")
        {
            Content = JsonContent.Create(new
            {
                tenantId = tenantA,
                name = "Denied",
                type = "api-key",
                secrets = new Dictionary<string, string> { ["token"] = "denied-secret" }
            })
        };
        readOnlyCreate.Headers.Add("X-Test-User", "reader");
        readOnlyCreate.Headers.Add("X-Test-Tenant", tenantA);
        var denied = await _client.SendAsync(readOnlyCreate);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record CredentialDto(
        string Id,
        string TenantId,
        string Name,
        string Type,
        string? Description,
        IReadOnlyList<string> SecretKeys,
        DateTime CreatedAt,
        DateTime LastModified,
        DateTime? LastUsedAt);
}
