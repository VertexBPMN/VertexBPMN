using System.Net;
using System.Net.Http.Json;

namespace VertexBPMN.Tests.Integration.Applications;

public sealed class ApplicationEndpointActiveReleaseTests
{
    [Fact]
    public async Task Get_UsesActiveLc10RevisionNotLegacyPublishedRevision()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json);
        fixture.Http.DefaultRequestHeaders.Remove("X-Test-User");
        fixture.Http.DefaultRequestHeaders.Remove("X-Test-Tenant");
        using var updated = await fixture.Http.PutAsJsonAsync($"/api/applications/{fixture.Id}", new
        {
            tenantId = "tenant-a", key = fixture.Key, name = fixture.Key, expectedVersion = 1,
            content = EndpointRuntimeContent.Json.Replace("\"key\":\"search\"", "\"key\":\"active-search\"", StringComparison.Ordinal)
                .Replace("/endpoints/search/", "/endpoints/active-search/", StringComparison.Ordinal)
        }, ct);
        updated.EnsureSuccessStatusCode();
        await fixture.ActivateRevisionAsync(2);
        fixture.Http.DefaultRequestHeaders.Add("X-Test-User", "reader");
        fixture.Http.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-a");
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/active-search/Invoice", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
