using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Portal.Bff;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Unit.Portal;

/// <summary>
/// LC06 portal BFF runtime slice: released-only serving, server-derived actor/tenant,
/// CSRF enforcement, narrow released query-binding gating and status projection.
/// </summary>
public sealed class BffRuntimeSliceTests : IClassFixture<PortalWebApplicationFactory>
{
    private readonly PortalWebApplicationFactory _factory;

    public BffRuntimeSliceTests(PortalWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetReleased_serves_only_the_released_revision_never_latest_or_draft()
    {
        // Released binding pins version 2 with two pages; a "latest/draft" (version 3) page
        // exists only outside the released revision and must never surface.
        _factory.Gateway.Released = new ReleasedApplication("app-1", 2, PortalWebApplicationFactory.ReleasedContent());
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/api/bff/applications/app-1/released", ct);
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        Assert.Equal("app-1", root.GetProperty("applicationId").GetString(), StringComparer.Ordinal);
        Assert.Equal(2, root.GetProperty("version").GetInt32());
        Assert.Equal(2, root.GetProperty("pageCount").GetInt32());
        var routes = root.GetProperty("pageRoutes").EnumerateArray().Select(r => r.GetString()).ToArray();
        Assert.Contains("/home", routes, StringComparer.Ordinal);
        Assert.Contains("/settings", routes, StringComparer.Ordinal);
        // The draft-only route never appears and raw content is never exposed.
        Assert.DoesNotContain("/draft-only", routes, StringComparer.Ordinal);
        Assert.False(root.TryGetProperty("contentJson", out _));
    }

    [Fact]
    public async Task Arbitrary_dataSource_binding_is_rejected_and_never_executed()
    {
        _factory.Gateway.ResetCalls();
        _factory.Gateway.Released = new ReleasedApplication("app-1", 2, PortalWebApplicationFactory.ReleasedContent());
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/api/bff/applications/app-1/data-sources/arbitraryRawQuery", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
        Assert.NotNull(body);
        Assert.Equal("bffUnsanctionedBinding", ProblemCode(body), StringComparer.Ordinal);
        Assert.Null(_factory.Gateway.LastExecutedBinding);

        // A declared-but-unreferenced binding (raw query/connector style) is also rejected.
        _factory.Gateway.Released = new ReleasedApplication("app-1", 2, """{"dataSources":[{"key":"rawConnector","bindingVersion":2}],"pages":[{"id":"p","route":"/p"}]}""");
        client = _factory.CreateClient();
        var second = await client.GetAsync("/api/bff/applications/app-1/data-sources/rawConnector", ct);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task Sanctioned_dataSource_binding_is_executed_and_projects_status()
    {
        _factory.Gateway.ResetCalls();
        _factory.Gateway.Released = new ReleasedApplication("app-1", 2, PortalWebApplicationFactory.ReleasedContent());
        _factory.Gateway.DataSourceResult = new DataSourceResult("invoiceList", "success", 3, DateTimeOffset.UtcNow);
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/api/bff/applications/app-1/data-sources/invoiceList", ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal("invoiceList", _factory.Gateway.LastExecutedBinding);
        var body = await response.Content.ReadFromJsonAsync<DataSourceResult>(cancellationToken: ct);
        Assert.Equal("invoiceList", body!.Binding);
        Assert.Equal("success", body.Status);
        Assert.Equal(3, body.RowCount);
    }

    [Fact]
    public async Task Claim_derives_actor_and_tenant_from_identity_and_ignores_body()
    {
        _factory.Gateway.ResetCalls();
        _factory.Gateway.Released = new ReleasedApplication("app-1", 2, PortalWebApplicationFactory.ReleasedContent());
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var taskId = Guid.NewGuid();
        await ObtainCsrfAsync(client, ct);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        // The caller attempts to smuggle an assignee/tenant; the BFF must ignore them and
        // derive everything from the authenticated identity (tenant claim "vertexbpmn").
        var response = await client.PostAsJsonAsync(
            $"/api/bff/applications/app-1/actions/claim/{taskId}",
            new { assignee = "attacker", tenant = "other-tenant" },
            ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("vertexbpmn", _factory.Gateway.LastClaimedTenant, StringComparer.Ordinal);
        Assert.Equal(taskId, _factory.Gateway.LastClaimedTask);
    }

    [Fact]
    public async Task Claim_without_csrf_token_is_rejected()
    {
        _factory.Gateway.ResetCalls();
        _factory.Gateway.Released = new ReleasedApplication("app-1", 2, PortalWebApplicationFactory.ReleasedContent());
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync(
            $"/api/bff/applications/app-1/actions/claim/{Guid.NewGuid()}",
            new { },
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(_factory.Gateway.LastClaimedTask);
    }

    [Fact]
    public async Task Complete_requires_idempotency_key()
    {
        _factory.Gateway.ResetCalls();
        _factory.Gateway.Released = new ReleasedApplication("app-1", 2, PortalWebApplicationFactory.ReleasedContent());
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        await ObtainCsrfAsync(client, ct);

        var response = await client.PostAsJsonAsync(
            $"/api/bff/applications/app-1/actions/complete/{Guid.NewGuid()}",
            new { variables = new Dictionary<string, object?>(StringComparer.Ordinal) { ["approved"] = true } },
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
        Assert.NotNull(body);
        Assert.Equal("bffMissingIdempotencyKey", ProblemCode(body), StringComparer.Ordinal);
        Assert.Null(_factory.Gateway.LastCompletedTask);
    }

    [Fact]
    public async Task TaskStatus_endpoint_returns_the_projected_status()
    {
        _factory.Gateway.Released = new ReleasedApplication("app-1", 2, PortalWebApplicationFactory.ReleasedContent());
        _factory.Gateway.State = new BffTaskState(Guid.Empty, "in_progress", "FinanceAgent", null, null);
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var taskId = Guid.NewGuid();

        var response = await client.GetAsync($"/api/bff/applications/app-1/tasks/{taskId}/status", ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TaskStatusProjection>(cancellationToken: ct);
        Assert.Equal("active", body!.Status, StringComparer.Ordinal);
        Assert.Equal("FinanceAgent", body.CandidateRole, StringComparer.Ordinal);
    }

    private static string? ProblemCode(ProblemDetails body)
        => body.Extensions.TryGetValue("code", out var code) && code is JsonElement element ? element.GetString() : null;

    private static async Task ObtainCsrfAsync(HttpClient client, CancellationToken ct)
    {
        var tokenResponse = await client.GetAsync("/api/bff/antiforgery/token", ct);
        tokenResponse.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Clear();
        var json = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }
}
