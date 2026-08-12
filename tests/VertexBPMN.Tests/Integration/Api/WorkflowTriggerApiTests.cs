using System.Net;
using System.Net.Http.Json;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

[Collection("IntegratedApi")]
public sealed class WorkflowTriggerApiTests
{
    private readonly HttpClient _client;

    public WorkflowTriggerApiTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output)
    {
        _client = factory.WithSharedFixture(dbFixture).CreateClient(output);
    }

    [Fact]
    public async Task Trigger_CanBeRegistered_Invoked_Disabled_AndListedWithoutSecret()
    {
        var key = $"trigger-process-{Guid.NewGuid():N}";
        var deployed = await _client.PostAsJsonAsync("/api/repository", new
        {
            bpmnXml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='{key}'><startEvent id='start'/><endEvent id='end'/></process></definitions>",
            name = $"{key}.bpmn",
            tenantId = (string?)null
        });
        deployed.EnsureSuccessStatusCode();

        var create = await _client.PostAsJsonAsync("/api/triggers", new
        {
            name = "External order trigger",
            processDefinitionKey = key,
            tenantId = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<TriggerCreated>();
        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created!.Secret));
        Assert.Contains($"/api/triggers/{created.Trigger.Id}/invoke", created.InvokePath);

        var listedResponse = await _client.GetAsync("/api/triggers");
        listedResponse.EnsureSuccessStatusCode();
        var listedJson = await listedResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SecretHash", listedJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(created.Trigger.Id.ToString(), listedJson, StringComparison.OrdinalIgnoreCase);

        using var wrongRequest = new HttpRequestMessage(HttpMethod.Post, created.InvokePath)
        {
            Content = JsonContent.Create(new { variables = new { source = "test" } })
        };
        wrongRequest.Headers.Add("X-VertexBPMN-Trigger-Secret", "wrong-secret");
        var wrong = await _client.SendAsync(wrongRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        using var invokeRequest = new HttpRequestMessage(HttpMethod.Post, created.InvokePath)
        {
            Content = JsonContent.Create(new { variables = new { source = "test" }, businessKey = "ORDER-42" })
        };
        invokeRequest.Headers.Add("X-VertexBPMN-Trigger-Secret", created.Secret);
        var invoked = await _client.SendAsync(invokeRequest);
        Assert.Equal(HttpStatusCode.Created, invoked.StatusCode);
        var instance = await invoked.Content.ReadFromJsonAsync<ProcessInstance>();
        Assert.NotNull(instance);
        Assert.Equal(key, instance!.ProcessId);

        var disable = await _client.PutAsJsonAsync($"/api/triggers/{created.Trigger.Id}", new { name = (string?)null, enabled = false });
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
        using var disabledRequest = new HttpRequestMessage(HttpMethod.Post, created.InvokePath);
        disabledRequest.Headers.Add("X-VertexBPMN-Trigger-Secret", created.Secret);
        var disabled = await _client.SendAsync(disabledRequest);
        Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
    }

    private sealed record TriggerCreated(TriggerInfo Trigger, string Secret, string InvokePath);
    private sealed record TriggerInfo(Guid Id, string Name, string ProcessDefinitionKey);
    private sealed record ProcessInstance(Guid Id, string ProcessId);
}
