using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace VertexBPMN.Tests.Integration.Applications;

public sealed class EndpointAcceptedTests
{
    [Fact]
    public async Task Post_PersistsAcceptedSnapshotBeforeEngineStart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new EndpointOperationFixture();
        await fixture.OpenAsync();
        await using var before = fixture.OpenDb();
        var release = await before.ApplicationReleases.AsNoTracking().SingleAsync(x =>
            x.ApplicationDefinitionId == fixture.Id && x.TenantId == "tenant-a", ct);
        using var response = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/start", new { }, ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var operationId = body.GetProperty("operationId").GetString()!;
        var stored = await fixture.FindOperationAsync(operationId);
        Assert.NotNull(stored);
        Assert.Equal("Accepted", stored.Status);
        Assert.Equal("Accepted", body.GetProperty("status").GetString());
        Assert.Null(stored.ProcessInstanceId);
        Assert.Null(stored.ResultJson);
        Assert.Equal("tenant-a", stored.TenantId);
        Assert.Equal(fixture.Key, stored.ApplicationKey);
        Assert.Equal("start", stored.EndpointKey);
        var snapshot = JsonSerializer.SerializeToElement(stored);
        Assert.Equal(fixture.Id, snapshot.GetProperty("ApplicationDefinitionId").GetString());
        Assert.Equal(release.ApplicationRevisionId, snapshot.GetProperty("ApplicationRevisionId").GetString());
        Assert.Equal(release.Id, snapshot.GetProperty("ApplicationReleaseId").GetString());
        Assert.Equal(fixture.ProcessId.ToString(), snapshot.GetProperty("BoundProcessDefinitionId").GetString());
        await using var fresh = fixture.OpenDb();
        Assert.False(await fresh.ProcessInstances.AnyAsync(x => x.ProcessDefinitionId == fixture.ProcessId, ct));
        using var status = await fixture.Http.GetAsync(body.GetProperty("statusUrl").GetString(), ct);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        var statusBody = await status.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("Accepted", statusBody.GetProperty("status").GetString());
    }
}
