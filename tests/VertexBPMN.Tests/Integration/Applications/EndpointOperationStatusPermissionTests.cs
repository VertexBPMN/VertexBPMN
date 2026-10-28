using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Tests.Integration.Applications;

// LC09B-MS-2b Behavior 2 (status-GET permission): GET operations/{id} re-checks the
// endpoint.Permission policy of the CURRENT endpoint (like POST); 403 on insufficient
// role, 404 only for an unknown/undefined endpoint or release, never permission denial.
public sealed class EndpointOperationStatusPermissionTests
{
    [Fact]
    public async Task StatusGet_RechecksCurrentEndpointPermission_Returns403_OnInsufficientRole()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new EndpointOperationFixture();
        await fixture.OpenAsync();

        using var start = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/start", new { }, ct);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var startBody = (await start.Content.ReadFromJsonAsync<JsonElement>(ct)).Clone();
        var statusUrl = startBody.GetProperty("statusUrl").GetString()!;

        // Current endpoint now requires AdminOnly; the reader (ReadOnly role) must be denied.
        await fixture.SwitchStartEndpointPermissionAsync("AdminOnly", includeEndpoint: true, ct);

        using var denied = await fixture.Http.GetAsync(statusUrl, ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task StatusGet_UndefinedCurrentEndpoint_Returns404_NotForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new EndpointOperationFixture();
        await fixture.OpenAsync();

        using var start = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/start", new { }, ct);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var startBody = (await start.Content.ReadFromJsonAsync<JsonElement>(ct)).Clone();
        var statusUrl = startBody.GetProperty("statusUrl").GetString()!;

        // The current release no longer defines the "start" endpoint -> undefined endpoint,
        // so the status read is a 404 (unknown), not a permission denial.
        await fixture.SwitchStartEndpointPermissionAsync("ReadOnly", includeEndpoint: false, ct);

        using var missing = await fixture.Http.GetAsync(statusUrl, ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
