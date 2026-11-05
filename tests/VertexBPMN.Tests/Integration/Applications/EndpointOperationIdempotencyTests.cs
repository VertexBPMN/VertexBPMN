using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application;

namespace VertexBPMN.Tests.Integration.Applications;

// LC09B-MS-2b Behavior 1 (idempotency/duplicate-start): POST with the same client
// idempotency key returns the same operationId and creates no second Accepted handle;
// after reconciliation exactly one process instance starts.
public sealed class EndpointOperationIdempotencyTests
{
    [Fact]
    public async Task SameIdempotencyKey_ReturnsSameOperationId_AndStartsSingleProcessInstance()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new EndpointOperationFixture();
        await fixture.OpenAsync();

        var key = "client-key-" + Guid.NewGuid().ToString("N");
        var payload = new { idempotencyKey = key };

        using var first = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/start", payload, ct);
        using var second = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/start", payload, ct);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        var firstBody = (await first.Content.ReadFromJsonAsync<JsonElement>(ct)).Clone();
        var secondBody = (await second.Content.ReadFromJsonAsync<JsonElement>(ct)).Clone();
        var operationId = firstBody.GetProperty("operationId").GetString()!;

        // Same frozen snapshot identity for the same key (same target/app/release binding).
        Assert.Equal(operationId, secondBody.GetProperty("operationId").GetString());
        Assert.Equal("Accepted", firstBody.GetProperty("status").GetString());
        Assert.Equal("Accepted", secondBody.GetProperty("status").GetString());

        // Exactly one Accepted handle exists for the repeated key (no duplicate row).
        await using var db = fixture.OpenDb();
        var handleCount = await db.ApplicationEndpointOperations.CountAsync(
            x => x.OperationId == operationId && x.Status == "Accepted", ct);
        Assert.Equal(1, handleCount);

        // After reconciliation exactly one process instance starts for the bound definition.
        await fixture.Services.GetRequiredService<JobExecutorService>().RunPollingIterationAsync(ct);
        var instanceCount = await db.ProcessInstances.CountAsync(
            x => x.ProcessDefinitionId == fixture.ProcessId && x.TenantId == "tenant-a", ct);
        Assert.Equal(1, instanceCount);
    }

    // Regression guard for the header transport: a body-key POST and an Idempotency-Key
    // header POST with the identical key resolve to the same Accepted handle.
    [Fact]
    public async Task HeaderAndBodyKey_WithSameValue_ResolveToSameOperation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new EndpointOperationFixture();
        await fixture.OpenAsync();

        var key = "client-key-" + Guid.NewGuid().ToString("N");
        using var bodyPost = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/start", new { idempotencyKey = key }, ct);
        using var headerRequest = new HttpRequestMessage(HttpMethod.Post,
            $"/api/apps/{fixture.Key}/endpoints/start");
        headerRequest.Headers.Add("Idempotency-Key", key);
        headerRequest.Content = JsonContent.Create(new { });
        using var headerPost = await fixture.Http.SendAsync(headerRequest, ct);
        Assert.Equal(HttpStatusCode.Accepted, bodyPost.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, headerPost.StatusCode);

        var bodyOp = (await bodyPost.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("operationId").GetString();
        var headerOp = (await headerPost.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("operationId").GetString();
        Assert.Equal(bodyOp, headerOp);

        await using var db = fixture.OpenDb();
        var handleCount = await db.ApplicationEndpointOperations.CountAsync(
            x => x.IdempotencyKey == key, ct);
        Assert.Equal(1, handleCount);
    }
}
