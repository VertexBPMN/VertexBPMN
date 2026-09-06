using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

/// <summary>
/// Covers the polling-trigger API surface end to end against the real BpmnDbContext / repository /
/// PollingTriggerService / Poller / ConnectorDestinationPolicy.
/// NOTE: the connector SSRF guard (ConnectorDestinationPolicy) hard-rejects loopback destinations, so a local
/// HTTP server cannot be used to drive a real "new data" start through the http connector in CI. The
/// new-data → instance-start semantics are therefore covered at the unit level (PollingSchedulerServiceTests),
/// and this test proves the full request → repository → poll → persistence path (a failed poll must still
/// persist ConsecutiveFailures/NextDueAt backoff).
/// </summary>
[Collection("IntegratedApi")]
public sealed class PollingTriggerApiTests
{
    private readonly HttpClient _client;

    public PollingTriggerApiTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output)
    {
        _client = factory.WithSharedFixture(dbFixture).CreateClient(output);
    }

    [Fact]
    public async Task CrudLifecycle_AndPollNow_PersistsThroughRepository()
    {
        var key = $"polled-process-{Guid.NewGuid():N}";
        var deployed = await _client.PostAsJsonAsync("/api/repository", new
        {
            bpmnXml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{key}'><startEvent id='start'/><endEvent id='end'/></process></definitions>",
            name = $"{key}.bpmn",
            tenantId = (string?)null
        }, cancellationToken: TestContext.Current.CancellationToken);
        deployed.EnsureSuccessStatusCode();

        // unreachable-anyway endpoint: the SSRF guard rejects all loopback, so the poll deterministically
        // fails at the connector stage and must persist an incremented failure count + backoff.
        var attributes = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["vertex:connector.method"] = "GET",
            ["vertex:connector.endpoint"] = "http://127.0.0.1:1/items",
            ["vertex:polling.cursorField"] = "httpStatus"
        });

        // --- create ---
        var create = await _client.PostAsJsonAsync("/api/polling-triggers", new
        {
            name = "Poll demo",
            processDefinitionKey = key,
            connectorType = "http",
            connectorAttributesJson = attributes,
            intervalSeconds = 60
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PollingTriggerCreated>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        var triggerId = created!.Trigger.Id;
        Assert.Equal("default", created.Trigger.TenantId);
        Assert.True(created.Trigger.Enabled);

        // --- get + list ---
        var get = await _client.GetAsync($"/api/polling-triggers/{triggerId}", TestContext.Current.CancellationToken);
        get.EnsureSuccessStatusCode();
        var list = await _client.GetFromJsonAsync<List<PollingTriggerInfo>>("/api/polling-triggers?tenantId=default", TestContext.Current.CancellationToken);
        Assert.Contains(list!, t => t.Id == triggerId);

        // --- update (interval; enable/disable is a separate path) ---
        var update = await _client.PutAsJsonAsync($"/api/polling-triggers/{triggerId}", new { intervalSeconds = 120, enabled = false }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        var disabled = await _client.GetFromJsonAsync<PollingTriggerInfo>($"/api/polling-triggers/{triggerId}?tenantId=default", TestContext.Current.CancellationToken);
        Assert.NotNull(disabled);
        Assert.Equal(120, disabled!.IntervalSeconds);
        Assert.False(disabled.Enabled);

        // --- poll-now executes the real poller; SSRF-guarded loopback fails -> failure count persists ---
        var pollNow = await _client.PostAsync($"/api/polling-triggers/{triggerId}/poll-now", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, pollNow.StatusCode);
        var polled = await pollNow.Content.ReadFromJsonAsync<PollingTriggerInfo>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(polled);
        Assert.NotNull(polled.LastPolledAt);
        Assert.Equal(1, polled.ConsecutiveFailures);
        Assert.True(polled.NextDueAt > DateTime.UtcNow.AddSeconds(50)); // exponential backoff >= interval

        // --- delete ---
        var del = await _client.DeleteAsync($"/api/polling-triggers/{triggerId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        var afterDelete = await _client.GetAsync($"/api/polling-triggers/{triggerId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }
}
