using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Application.Lc06;
using VertexBPMN.Portal.Bff;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Unit.Portal;

/// <summary>
/// LC06 reference application (LC-AC-08): the released revision served by the Portal BFF.
/// Proves the reference app's released ContentJson is served by GET /released, its
/// dataSources/actions survive the BFF narrow binding gate (sanctioned reads resolve,
/// arbitrary/unreferenced ones are rejected), and the start-claim-complete task flow resolves.
/// </summary>
public sealed class ReferenceAppBffTests : IClassFixture<PortalWebApplicationFactory>
{
    private const string AppId = "lc06-reference-app";
    private static readonly string[] ReferenceRoutes = { "/home", "/tasks", "/settings" };
    private readonly PortalWebApplicationFactory _factory;

    public ReferenceAppBffTests(PortalWebApplicationFactory factory) => _factory = factory;

    private static void UseReleased(PortalWebApplicationFactory factory)
        => factory.Gateway.Released = new ReleasedApplication(AppId, 1, Lc06ReferenceApp.ContentJson);

    [Fact]
    public async Task GetReleased_serves_the_reference_app_and_its_page_routes()
    {
        UseReleased(_factory);
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync($"/api/bff/applications/{AppId}/released", ct);

        response.EnsureSuccessStatusCode();
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement;
        Assert.Equal(AppId, root.GetProperty("applicationId").GetString(), StringComparer.Ordinal);
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal(3, root.GetProperty("pageCount").GetInt32());
        var routes = root.GetProperty("pageRoutes").EnumerateArray().Select(r => r.GetString()).ToArray();
        Assert.Equal(ReferenceRoutes, routes, StringComparer.Ordinal);
        Assert.False(root.TryGetProperty("contentJson", out _));
    }

    [Fact]
    public async Task Sanctioned_data_source_binding_resolves_and_executes()
    {
        UseReleased(_factory);
        _factory.Gateway.ResetCalls();
        _factory.Gateway.DataSourceResult = new DataSourceResult(Lc06ReferenceApp.SanctionedBinding, "success", 3, DateTimeOffset.UtcNow);
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync($"/api/bff/applications/{AppId}/data-sources/{Lc06ReferenceApp.SanctionedBinding}", ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal(Lc06ReferenceApp.SanctionedBinding, _factory.Gateway.LastExecutedBinding, StringComparer.Ordinal);
        var body = await response.Content.ReadFromJsonAsync<DataSourceResult>(cancellationToken: ct);
        Assert.Equal(Lc06ReferenceApp.SanctionedBinding, body!.Binding, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Declared_but_unreferenced_binding_is_rejected_and_never_executed()
    {
        UseReleased(_factory);
        _factory.Gateway.ResetCalls();
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync($"/api/bff/applications/{AppId}/data-sources/{Lc06ReferenceApp.UnreferencedBinding}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
        Assert.NotNull(body);
        Assert.Equal("bffUnsanctionedBinding", ProblemCode(body), StringComparer.Ordinal);
        Assert.Null(_factory.Gateway.LastExecutedBinding);
    }

    [Fact]
    public async Task Arbitrary_undeclared_binding_is_rejected_and_never_executed()
    {
        UseReleased(_factory);
        _factory.Gateway.ResetCalls();
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync($"/api/bff/applications/{AppId}/data-sources/{Lc06ReferenceApp.ArbitraryBinding}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
        Assert.NotNull(body);
        Assert.Equal("bffUnsanctionedBinding", ProblemCode(body), StringComparer.Ordinal);
        Assert.Null(_factory.Gateway.LastExecutedBinding);
    }

    [Fact]
    public async Task Start_process_is_sanctioned_by_the_reference_app()
    {
        UseReleased(_factory);
        _factory.Gateway.ResetCalls();
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        await ObtainCsrfAsync(client, ct);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync($"/api/bff/applications/{AppId}/actions/start", new { }, ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BffActionResult>(cancellationToken: ct);
        Assert.Equal(ReleasedActions.StartProcess, body!.Action, StringComparer.Ordinal);
        Assert.Equal(ReleasedActions.StartProcess, _factory.Gateway.LastExecutedBinding, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Claim_and_complete_tasks_resolve_against_the_reference_app()
    {
        UseReleased(_factory);
        _factory.Gateway.ResetCalls();
        _factory.Gateway.State = new BffTaskState(Guid.Empty, "in_progress", "FinanceAgent", null, null);
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        await ObtainCsrfAsync(client, ct);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var taskId = Guid.NewGuid();

        var claim = await client.PostAsJsonAsync($"/api/bff/applications/{AppId}/actions/claim/{taskId}", new { assignee = "ignored" }, ct);
        Assert.Equal(HttpStatusCode.NoContent, claim.StatusCode);
        Assert.Equal(taskId, _factory.Gateway.LastClaimedTask);

        var complete = await client.PostAsJsonAsync($"/api/bff/applications/{AppId}/actions/complete/{taskId}",
            new { variables = new Dictionary<string, object?>(StringComparer.Ordinal) { ["approved"] = true } }, ct);
        Assert.Equal(HttpStatusCode.NoContent, complete.StatusCode);
        Assert.Equal(taskId, _factory.Gateway.LastCompletedTask);

        var status = await client.GetAsync($"/api/bff/applications/{AppId}/tasks/{taskId}/status", ct);
        status.EnsureSuccessStatusCode();
        var projection = await status.Content.ReadFromJsonAsync<TaskStatusProjection>(cancellationToken: ct);
        Assert.Equal("active", projection!.Status, StringComparer.Ordinal);
        Assert.Equal("FinanceAgent", projection.CandidateRole, StringComparer.Ordinal);
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
