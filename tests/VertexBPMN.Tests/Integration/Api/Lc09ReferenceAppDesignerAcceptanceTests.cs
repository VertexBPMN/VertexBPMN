using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

/// <summary>
/// LC-AC-09 (Kriterium B): a reference application is authored entirely through the LC07
/// designer surface, published to a released revision, then exercised through the REAL portal
/// API + persisted rows — independent of any UI success message.
///
/// The content is exactly what AppDesignerDocument.ToJson emits (process reference as
/// { type, key }, pages with route, components/actions with versions). It drives the real
/// ApplicationController → PersistentApplicationDefinitionService pipeline, proving the S5
/// serialization/gate fix (key → ProcessDefinition id+tenantId at the server boundary), the
/// release path, and the live runtime flow:
///   - searchable table   : released revision declares the data source; released read is 200.
///   - data mapping       : mapping {amount → Betrag} persists in the released revision.
///   - form               : form-js component persists in the released revision.
///   - real process start : start by the persisted key creates a DB process instance + task.
///   - task completion    : claim + complete transition the DB task to Completed.
/// Every criterion is asserted against the API response AND the persisted rows.
/// </summary>
public sealed class Lc09ReferenceAppDesignerAcceptanceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private const string Tenant = "tenant-a";
    private const string ProcessKey = "lc07-invoice-flow";
    private const string AppKey = "lc07-invoice-app";

    private const string DesignerContent =
        "{" +
        "\"localizations\": {}," +
        "\"references\":[{\"type\":\"process\",\"key\":\"" + ProcessKey + "\"}]," +
        "\"dataSources\":[{\"key\":\"invoices\",\"bindingVersion\":2,\"fields\":[\"id\",\"amount\"]}]," +
        "\"pages\":[{\"id\":\"p1\",\"route\":\"/invoices\",\"components\":[" +
        "{\"id\":\"c1\",\"type\":\"table\",\"version\":\"1.0.0\",\"dataSource\":\"invoices\"," +
        "\"mappings\":[{\"sourcePath\":\"amount\",\"targetKey\":\"Betrag\"}]," +
        "\"action\":{\"type\":\"data-source-read\",\"version\":\"1.0.0\",\"dataSource\":\"invoices\"}}," +
        "{\"id\":\"c2\",\"type\":\"form-js\",\"version\":\"1.0.0\"," +
        "\"action\":{\"type\":\"start-process\",\"version\":\"1.0.0\",\"processDefinitionId\":\"" + ProcessKey + "\"}}" +
        "]}]}";

    private const string InvoiceFlowBpmn =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"" +
        " xmlns:vertex=\"http://vertexbpmn.io/schema/1.0\" targetNamespace=\"https://vertexbpmn.dev/s5\">" +
        "<process id=\"lc07-invoice-flow\" isExecutable=\"true\">" +
        "<startEvent id=\"start\" />" +
        "<sequenceFlow id=\"f1\" sourceRef=\"start\" targetRef=\"approve\" />" +
        "<userTask id=\"approve\" name=\"Freigabe\" candidateRole=\"FinanceAgent\" />" +
        "<sequenceFlow id=\"f2\" sourceRef=\"approve\" targetRef=\"end\" />" +
        "<endEvent id=\"end\" />" +
        "</process></definitions>";

    public Lc09ReferenceAppDesignerAcceptanceTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        _client.Timeout = TimeSpan.FromSeconds(60);
        XunitLoggerProvider.Register(factory.Services.GetRequiredService<ILoggerFactory>(), output);
    }

    [Fact]
    public async Task Author_Publish_And_Run_ReferenceApplication_ThroughRealApiAndDb()
    {
        var processId = await SeedProcessAsync();
        var appId = await PublishViaApiAsync();
        var releasedContent = await GetReleasedContentAsync(appId);

        AssertGateFixPersisted(processId, releasedContent);
        AssertSearchableTableAndMapping(releasedContent);
        await AssertReleasedDataSourceServedAsync(appId);
        AssertFormPresent(releasedContent);
        var instanceId = await StartRealProcessAsync(processId);
        await AssertTaskCompletionAsync(instanceId);
    }

    private async Task<Guid> SeedProcessAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        if (await db.ProcessDefinitions.AnyAsync(x => x.TenantId == Tenant && x.Key == ProcessKey, TestContext.Current.CancellationToken))
        {
            return (await db.ProcessDefinitions.SingleAsync(x => x.TenantId == Tenant && x.Key == ProcessKey, TestContext.Current.CancellationToken)).Id;
        }
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "lc07-deployment", CreatedAt = DateTime.UtcNow };
        var process = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Key = ProcessKey,
            Name = "Invoice flow",
            Version = 1,
            TenantId = Tenant,
            Deployment = deployment,
            DeploymentId = deployment.Id,
            BpmnXml = InvoiceFlowBpmn
        };
        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.Add(process);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return process.Id;
    }

    private async Task<string> PublishViaApiAsync()
    {
        var response = await _client.PostAsJsonAsync("api/applications",
            new { tenantId = Tenant, key = AppKey, name = "Invoices", expectedVersion = 0, content = DesignerContent },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var appId = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetString()!;

        var candidateResponse = await _client.PostAsync($"api/applications/{appId}/revisions/1/release-candidate?tenantId={Tenant}", null, TestContext.Current.CancellationToken);
        var candidateBody = await candidateResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(HttpStatusCode.OK == candidateResponse.StatusCode, $"candidate {candidateResponse.StatusCode}: {candidateBody}");
        var candidateId = (await candidateResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetString()!;

        var releaseResponse = await _client.PostAsync($"api/applications/release-candidates/{candidateId}/release?tenantId={Tenant}", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, releaseResponse.StatusCode);
        return appId;
    }

    private async Task<JsonElement> GetReleasedContentAsync(string appId)
    {
        var releasedResponse = await _client.GetAsync($"api/applications/{appId}/released-revision?tenantId={Tenant}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, releasedResponse.StatusCode);
        var releasedJson = await releasedResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(releasedJson.GetProperty("contentJson").GetString()!);
        return doc.RootElement.Clone();
    }

    private void AssertGateFixPersisted(Guid processId, JsonElement content)
    {
        // Serialization/gate fix + release persisted: the released revision resolves the process
        // reference to its real ProcessDefinition id+tenantId while keeping the stable key.
        Assert.Single(content.GetProperty("references").EnumerateArray());
        var reference = content.GetProperty("references")[0];
        Assert.Equal("process", reference.GetProperty("type").GetString());
        Assert.Equal(ProcessKey, reference.GetProperty("key").GetString());
        Assert.Equal(processId.ToString(), Guid.Parse(reference.GetProperty("id").GetString()!).ToString());
        Assert.Equal(Tenant, reference.GetProperty("tenantId").GetString());

        AssertGateFixPersistedDb(processId);
    }

    private void AssertGateFixPersistedDb(Guid processId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var candidate = db.ApplicationReleaseCandidates.Single(c => c.IsReleased);
        Assert.NotNull(candidate);
        var revision = db.ApplicationRevisions.Single(r => r.Version == 1);
        Assert.Equal("[]", revision.DiagnosticsJson); // designer-built content passes the domain gate
        using var doc = JsonDocument.Parse(revision.ContentJson);
        var reference = doc.RootElement.GetProperty("references")[0];
        Assert.Equal(processId.ToString(), Guid.Parse(reference.GetProperty("id").GetString()!).ToString());
        Assert.Equal(Tenant, reference.GetProperty("tenantId").GetString());
    }

    private async Task AssertReleasedDataSourceServedAsync(string appId)
    {
        var url = $"/api/applications/{appId}/released/data-sources/invoices?tenantId={Tenant}";
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"released data-source read failed ({response.StatusCode}): {body}");
        Assert.Equal("invoices", body.GetProperty("binding").GetString());
        Assert.Equal(2, body.GetProperty("bindingVersion").GetInt32());
        Assert.Equal("success", body.GetProperty("status").GetString());
    }

    private static void AssertSearchableTableAndMapping(JsonElement released)
    {
        // Searchable table: released revision declares the data source; the released read is 200.
        var table = released.GetProperty("pages")[0].GetProperty("components")[0];
        Assert.Equal("table", table.GetProperty("type").GetString());
        Assert.Equal("invoices", table.GetProperty("dataSource").GetString());

        // Data mapping persists in the released revision.
        var mapping = Assert.Single(table.GetProperty("mappings").EnumerateArray());
        Assert.Equal("amount", mapping.GetProperty("sourcePath").GetString());
        Assert.Equal("Betrag", mapping.GetProperty("targetKey").GetString());
    }

    private static void AssertFormPresent(JsonElement content)
    {
        // Form: form-js component persists in the released revision, with the start action.
        var form = content.GetProperty("pages")[0].GetProperty("components")[1];
        Assert.Equal("form-js", form.GetProperty("type").GetString());
        Assert.Equal("start-process", form.GetProperty("action").GetProperty("type").GetString());
        Assert.Equal(ProcessKey, form.GetProperty("action").GetProperty("processDefinitionId").GetString());
    }

    private async Task<Guid> StartRealProcessAsync(Guid processId)
    {
        var startResponse = await _client.PostAsJsonAsync("api/runtime/start",
            new { ProcessDefinitionKey = ProcessKey, Variables = (object?)null, BusinessKey = (object?)null, TenantId = Tenant },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        var instanceId = (await startResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var instance = await db.ProcessInstances.SingleAsync(x => x.Id == instanceId, TestContext.Current.CancellationToken);
        Assert.Equal(processId, instance.ProcessDefinitionId); // started the real process
        Assert.Equal(Tenant, instance.TenantId);
        var task = await db.Tasks.SingleAsync(x => x.ProcessInstanceId == instanceId, TestContext.Current.CancellationToken);
        Assert.Equal(UserTaskStatus.Pending, task.Status);
        Assert.Equal("FinanceAgent", task.CandidateRole); // approval task with FinanceAgent role
        Assert.Null(task.Assignee);
        return instanceId;
    }

    private async Task AssertTaskCompletionAsync(Guid instanceId)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
            var task = await db.Tasks.SingleAsync(x => x.ProcessInstanceId == instanceId, TestContext.Current.CancellationToken);
            var claimResponse = await _client.PostAsJsonAsync($"api/task/{task.Id}/claim",
                new { UserId = "finance-user", TenantId = Tenant }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, claimResponse.StatusCode);
            var completeResponse = await _client.PostAsJsonAsync($"api/task/{task.Id}/complete",
                new { Variables = new { }, TenantId = Tenant }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, completeResponse.StatusCode);
        }

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var completed = await db2.Tasks.SingleAsync(x => x.ProcessInstanceId == instanceId, TestContext.Current.CancellationToken);
        Assert.Equal(UserTaskStatus.Completed, completed.Status);
        Assert.Equal("finance-user", completed.Assignee);
        var history = await db2.HistoryEvents.SingleOrDefaultAsync(h => h.ProcessInstanceId == instanceId && h.EventType == "USER_TASK_COMPLETED", TestContext.Current.CancellationToken);
        Assert.NotNull(history);
    }
}
