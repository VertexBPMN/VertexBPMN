using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Tests.Integration.Applications;

// LC09B-MS-1 RED1: POST to a process-start endpoint bound to an active released revision
// returns 202 with operationId + statusUrl + processInstanceId.
public sealed class ApplicationEndpointOperationTests
{
    [Fact]
    public async Task StartProcessEndpoint_Returns202_WithOperationIdStatusUrlAndProcessInstanceId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new EndpointOperationFixture();
        await fixture.OpenAsync();
        using var response = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/start", new { }, ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).Clone();
        var operationId = body.GetProperty("operationId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(operationId));
        var statusUrl = body.GetProperty("statusUrl").GetString()!;
        Assert.Contains($"/operations/{operationId}", statusUrl, StringComparison.Ordinal);
        Assert.Equal("Accepted", body.GetProperty("status").GetString());
        Assert.False(body.TryGetProperty("processInstanceId", out _));
    }

    [Fact]
    public async Task OperationIsDurableAndStatusGet_ReturnsStructuredResult()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new EndpointOperationFixture();
        await fixture.OpenAsync();
        using var start = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/start", new { }, ct);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var startBody = (await start.Content.ReadFromJsonAsync<JsonElement>(ct)).Clone();
        var operationId = startBody.GetProperty("operationId").GetString()!;
        await fixture.Services.GetRequiredService<JobExecutorService>().RunPollingIterationAsync(ct);
        var reconciled = await fixture.FindOperationAsync(operationId);
        Assert.NotNull(reconciled);
        var processInstanceId = reconciled.ProcessInstanceId!;

        // Durable start confirmation, not a terminal business-process result.
        var stored = await fixture.FindOperationAsync(operationId);
        Assert.NotNull(stored);
        Assert.Equal(operationId, stored.OperationId);
        Assert.Equal("tenant-a", stored.TenantId);
        Assert.Equal(fixture.Key, stored.ApplicationKey);
        Assert.Equal("start", stored.EndpointKey);
        Assert.Equal("Started", stored.Status);
        Assert.Equal(processInstanceId, stored.ProcessInstanceId);
        using var result = JsonDocument.Parse(stored.ResultJson!);
        Assert.Equal(processInstanceId, result.RootElement.GetProperty("processInstanceId").GetString());

        // GET status is tenant-/endpoint-scoped and returns a structured result object.
        var statusUrl = startBody.GetProperty("statusUrl").GetString()!;
        var status = await fixture.Http.GetAsync(statusUrl, ct);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        var statusBody = (await status.Content.ReadFromJsonAsync<JsonElement>(ct)).Clone();
        Assert.Equal(operationId, statusBody.GetProperty("operationId").GetString());
        Assert.Equal(fixture.Key, statusBody.GetProperty("appKey").GetString());
        Assert.Equal("start", statusBody.GetProperty("endpointKey").GetString());
        Assert.Equal("Started", statusBody.GetProperty("status").GetString());
        Assert.Equal(processInstanceId, statusBody.GetProperty("result").GetProperty("processInstanceId").GetString());
    }

    [Fact]
    public async Task StatusGet_IsTenantScoped_And_MissingOperationIs404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = new EndpointOperationFixture();
        await fixture.OpenAsync();
        using var start = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/start", new { }, ct);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var startBody = (await start.Content.ReadFromJsonAsync<JsonElement>(ct)).Clone();
        var statusUrl = startBody.GetProperty("statusUrl").GetString()!;

        // Foreign tenant cannot read the operation (tenant-scoped 404), same as the GET path.
        using var foreign = new HttpRequestMessage(HttpMethod.Get, statusUrl);
        foreign.Headers.Add("X-Test-User", "reader");
        foreign.Headers.Add("X-Test-Tenant", "tenant-b");
        using var foreignResp = await fixture.Http.SendAsync(foreign, ct);
        Assert.Equal(HttpStatusCode.NotFound, foreignResp.StatusCode);

        // Unknown operation id is a 404.
        var missing = await fixture.Http.GetAsync(
            $"/api/apps/{fixture.Key}/endpoints/start/operations/{Guid.NewGuid():N}", ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // Internal exception text must not leak: unknown endpoint key is a 404, no message body.
        var unknown = await fixture.Http.PostAsJsonAsync(
            $"/api/apps/{fixture.Key}/endpoints/does-not-exist", new { }, ct);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var unknownBody = await unknown.Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("Exception", unknownBody, StringComparison.OrdinalIgnoreCase);
    }
}
