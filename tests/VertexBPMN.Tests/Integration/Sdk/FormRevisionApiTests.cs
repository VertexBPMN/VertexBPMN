using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VertexBPMN.Sdk;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Sdk;

public sealed class FormRevisionApiTests
{
    [Fact]
    public async Task RevisionEndpoints_ReturnPersistedHistoryAndEnforceTenant()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var http = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var key = "revision-" + Guid.NewGuid().ToString("N");
        using var create = await http.PostAsJsonAsync("/api/forms", new { tenantId = "tenant-a", key, name = "Approval", schema = "{}" }, ct);
        create.EnsureSuccessStatusCode();
        using var update = await http.PutAsJsonAsync($"/api/forms/{key}", new { tenantId = "tenant-a", key, name = "Approval", schema = "{\"new\":true}" }, ct);
        update.EnsureSuccessStatusCode();
        using var get = await http.GetAsync($"/api/forms/{key}/revisions/1?tenantId=tenant-a", ct);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var revision = await get.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("{}", revision.GetProperty("schema").GetString());
        Assert.Equal(1, revision.GetProperty("version").GetInt32());
        using var list = await http.GetAsync($"/api/forms/{key}/revisions?tenantId=tenant-a", ct);
        list.EnsureSuccessStatusCode();
        Assert.Equal(2, (await list.Content.ReadFromJsonAsync<JsonElement>(ct)).GetArrayLength());
        using var foreign = await http.GetAsync($"/api/forms/{key}/revisions/1?tenantId=tenant-b", ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        var sdk = new VertexBpmnClient(http);
        var sdkRevision = await sdk.GetFormRevisionAsync(key, 1, "tenant-a", ct);
        Assert.Equal("{}", sdkRevision!.Schema);
        Assert.Equal(2, (await sdk.ListFormRevisionsAsync(key, "tenant-a", ct)).Count);
    }
}
