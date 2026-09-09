using System.Net;
using System.Net.Http;
using System.Text;
using VertexBPMN.Tests.Infrastructure;
using Xunit;

namespace VertexBPMN.Tests.Integration.Api;

/// <summary>
/// Phase-3 S-Gaps S2/S3/S5 hardening verification:
/// System-/Infra-Mutationen und globale Tenant-Listen sind per [Authorize(Policy = "AdminOnly")]
/// geschützt. ReadOnly-Benutzer (X-Test-User-Header) müssen 403 erhalten, Admins (Standard-Principal) OK.
/// </summary>
[Collection("IntegratedApi")]
public class PrivilegeGateSecurityTests
{
    private readonly HttpClient _client;

    public PrivilegeGateSecurityTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output)
    {
        _client = factory.WithSharedFixture(dbFixture).CreateClient(output);
    }

    private HttpRequestMessage WithReadOnly(HttpRequestMessage request)
    {
        request.Headers.Add("X-Test-User", "readonly-user");
        request.Headers.Add("X-Test-Tenant", "vertexbpmn");
        return request;
    }

    [Theory]
    [InlineData("POST", "/api/health/gc", "")]
    [InlineData("POST", "/api/health/rate-limits/test-identifier/reset", "?policy=default")]
    [InlineData("GET", "/api/identity/list-tenants", "")]
    [InlineData("DELETE", "/api/load-balancer/workers/worker-1", "")]
    [InlineData("POST", "/api/load-balancer/rebalance", "")]
    [InlineData("PUT", "/api/load-balancer/config", "")]
    public async Task AdminOnlyEndpoints_ReadOnlyUser_IsForbidden(string method, string path, string query)
    {
        var request = WithReadOnly(new HttpRequestMessage(new HttpMethod(method), path + query));
        if (string.Equals(method, "PUT", StringComparison.Ordinal))
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HealthGc_AdminUser_IsAllowed()
    {
        var response = await _client.PostAsync("/api/health/gc", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task IdentityListTenants_AdminUser_IsAllowed()
    {
        var response = await _client.GetAsync("/api/identity/list-tenants", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
