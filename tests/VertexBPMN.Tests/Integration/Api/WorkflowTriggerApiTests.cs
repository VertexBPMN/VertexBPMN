using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        }, cancellationToken: TestContext.Current.CancellationToken);
        deployed.EnsureSuccessStatusCode();

        var create = await _client.PostAsJsonAsync("/api/triggers", new
        {
            name = "External order trigger",
            processDefinitionKey = key,
            tenantId = (string?)null
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<TriggerCreated>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created!.Secret));
        Assert.Contains($"/api/triggers/{created.Trigger.Id}/invoke", created.InvokePath);

        var listedResponse = await _client.GetAsync("/api/triggers", TestContext.Current.CancellationToken);
        listedResponse.EnsureSuccessStatusCode();
        var listedJson = await listedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("SecretHash", listedJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(created.Trigger.Id.ToString(), listedJson, StringComparison.OrdinalIgnoreCase);

        using var wrongRequest = new HttpRequestMessage(HttpMethod.Post, created.InvokePath)
        {
            Content = JsonContent.Create(new { variables = new { source = "test" } })
        };
        wrongRequest.Headers.Add("X-VertexBPMN-Trigger-Secret", "wrong-secret");
        var wrong = await _client.SendAsync(wrongRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        using var invokeRequest = new HttpRequestMessage(HttpMethod.Post, created.InvokePath)
        {
            Content = JsonContent.Create(new { variables = new { source = "test" }, businessKey = "ORDER-42" })
        };
        invokeRequest.Headers.Add("X-VertexBPMN-Trigger-Secret", created.Secret);
        var invoked = await _client.SendAsync(invokeRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, invoked.StatusCode);
        var instance = await invoked.Content.ReadFromJsonAsync<ProcessInstance>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(instance);
        Assert.Equal(key, instance!.ProcessId);

        var disable = await _client.PutAsJsonAsync($"/api/triggers/{created.Trigger.Id}", new { name = (string?)null, enabled = false }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
        using var disabledRequest = new HttpRequestMessage(HttpMethod.Post, created.InvokePath);
        disabledRequest.Headers.Add("X-VertexBPMN-Trigger-Secret", created.Secret);
        var disabled = await _client.SendAsync(disabledRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
    }

    [Fact]
    public async Task BpmnWebhook_IsSynchronized_AndStartsProcessWithValidHmac()
    {
        var tenantId = $"webhook-{Guid.NewGuid():N}";
        var processKey = $"webhook-process-{Guid.NewGuid():N}";
        var endpoint = $"/orders/{Guid.NewGuid():N}";
        const string signingSecret = "integration-webhook-secret";
        var credential = await _client.PostAsJsonAsync("/api/credentials", new
        {
            tenantId,
            name = "Webhook HMAC",
            type = "hmac",
            description = "test credential",
            secrets = new Dictionary<string, string> { ["secret"] = signingSecret }
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, credential.StatusCode);
        var credentialJson = await credential.Content.ReadFromJsonAsync<CredentialCreated>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(credentialJson);

        var xml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:vertex='https://vertexbpmn.io/schema/bpmn/1.0'><process id='{processKey}'><startEvent id='webhookStart'><extensionElements><vertex:webhook path='{endpoint}' method='POST' authMode='hmac-sha256' credentialRef='{credentialJson!.Id}' correlationKey='orderId' payloadSchema='{{&quot;type&quot;:&quot;object&quot;,&quot;required&quot;:[&quot;orderId&quot;],&quot;properties&quot;:{{&quot;amount&quot;:{{&quot;type&quot;:&quot;integer&quot;}}}}}}'/><vertex:trigger type='webhook' name='Order ingress' processDefinitionKey='{processKey}'/></extensionElements></startEvent><endEvent id='end'/><sequenceFlow id='f' sourceRef='webhookStart' targetRef='end'/></process></definitions>";
        var deploy = await _client.PostAsJsonAsync("/api/repository", new { bpmnXml = xml, name = "webhook.bpmn", tenantId }, cancellationToken: TestContext.Current.CancellationToken);
        deploy.EnsureSuccessStatusCode();

        var triggers = await _client.GetFromJsonAsync<List<TriggerInfo>>($"/api/triggers?tenantId={tenantId}", cancellationToken: TestContext.Current.CancellationToken);
        var registered = Assert.Single(triggers!);
        Assert.Equal(endpoint, registered.Path);
        Assert.Equal("POST", registered.Method);
        Assert.Equal("hmac-sha256", registered.AuthenticationMode);
        Assert.Equal(credentialJson.Id, registered.CredentialId);

        var body = "{\"orderId\":\"ORDER-42\",\"amount\":42}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingSecret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks{endpoint}") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-VertexBPMN-Signature", $"sha256={signature}");
        var invoked = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, invoked.StatusCode);

        using var invalidRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks{endpoint}") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        invalidRequest.Headers.Add("X-VertexBPMN-Signature", "sha256=00");
        var invalid = await _client.SendAsync(invalidRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);

        const string malformedBody = "{\"amount\":\"not-an-integer\"}";
        var malformedSignature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingSecret), Encoding.UTF8.GetBytes(malformedBody))).ToLowerInvariant();
        using var malformedRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks{endpoint}") { Content = new StringContent(malformedBody, Encoding.UTF8, "application/json") };
        malformedRequest.Headers.Add("X-VertexBPMN-Signature", $"sha256={malformedSignature}");
        var malformed = await _client.SendAsync(malformedRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task BpmnTriggerSecretWebhook_ExposesOneTimeSecretInDeployHeader_AndCanBeInvoked()
    {
        var tenantId = $"ts-webhook-{Guid.NewGuid():N}";
        var processKey = $"ts-webhook-process-{Guid.NewGuid():N}";
        var path = $"/ts-orders/{Guid.NewGuid():N}";

        var xml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:vertex='https://vertexbpmn.io/schema/bpmn/1.0'><process id='{processKey}'><startEvent id='webhookStart'><extensionElements><vertex:webhook path='{path}' method='POST' authMode='trigger-secret'/><vertex:trigger type='webhook' name='TS order ingress' processDefinitionKey='{processKey}'/></extensionElements></startEvent><endEvent id='end'/><sequenceFlow id='f' sourceRef='webhookStart' targetRef='end'/></process></definitions>";
        using var deployResponse = await _client.PostAsJsonAsync("/api/repository", new { bpmnXml = xml, name = "ts-webhook.bpmn", tenantId }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, deployResponse.StatusCode);

        Assert.True(deployResponse.Headers.TryGetValues("X-VertexBPMN-Created-Webhooks", out var values), "Deploy response should carry the one-time webhook secret header.");
        var header = Assert.Single(values);
        var created = JsonSerializer.Deserialize<List<CreatedWebhook>>(header) ?? [];
        var hook = Assert.Single(created);
        Assert.Equal(path, hook.Path);
        Assert.Equal("POST", hook.Method);
        Assert.False(string.IsNullOrWhiteSpace(hook.Secret));
        Assert.Equal($"/api/webhooks{path}", hook.InvokePath);

        using var wrongRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks{path}") { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        wrongRequest.Headers.Add("X-VertexBPMN-Trigger-Secret", "wrong-secret");
        var wrong = await _client.SendAsync(wrongRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        using var okRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks{path}") { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        okRequest.Headers.Add("X-VertexBPMN-Trigger-Secret", hook.Secret);
        var ok = await _client.SendAsync(okRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    [Fact]
    public async Task MessageCorrelation_UpdatesAnActiveProcessInstance()
    {
        var key = $"message-process-{Guid.NewGuid():N}";
        var deployed = await _client.PostAsJsonAsync("/api/repository", new
        {
            bpmnXml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><message id='order-received-message' name='order-received'/><process id='{key}'><startEvent id='start'/><intermediateCatchEvent id='wait-for-order'><messageEventDefinition messageRef='order-received-message'/></intermediateCatchEvent><endEvent id='end'/><sequenceFlow id='to-wait' sourceRef='start' targetRef='wait-for-order'/><sequenceFlow id='to-end' sourceRef='wait-for-order' targetRef='end'/></process></definitions>",
            name = $"{key}.bpmn",
            tenantId = (string?)null
        }, cancellationToken: TestContext.Current.CancellationToken);
        deployed.EnsureSuccessStatusCode();

        var start = await _client.PostAsJsonAsync("/api/runtime/start", new
        {
            processDefinitionKey = key,
            variables = new Dictionary<string, object> { ["origin"] = "test" },
            businessKey = "ORDER-99",
            tenantId = (string?)null
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var instance = await start.Content.ReadFromJsonAsync<ProcessInstance>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(instance);

        var correlate = await _client.PostAsJsonAsync("/api/vertex/message", new
        {
            messageName = "order-received",
            processInstanceId = instance!.Id,
            variables = new Dictionary<string, object> { ["status"] = "received" }
        }, cancellationToken: TestContext.Current.CancellationToken);
        correlate.EnsureSuccessStatusCode();
        var result = await correlate.Content.ReadFromJsonAsync<MessageCorrelationResult>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("correlated", result!.ResultType);
        Assert.Equal(instance.Id.ToString(), result.ProcessInstanceId);

        var persisted = await _client.GetFromJsonAsync<ProcessInstanceDetails>($"/api/runtime/{instance.Id}", cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal("test", persisted!.Variables["origin"].ToString());
        Assert.Equal("received", persisted.Variables["status"].ToString());
    }

    private sealed record TriggerCreated(TriggerInfo Trigger, string Secret, string InvokePath);
    private sealed record CreatedWebhook(string? Path, string? Method, string Secret, string InvokePath);
    private sealed record TriggerInfo(Guid Id, string Name, string ProcessDefinitionKey, string? Path, string? Method, string AuthenticationMode, string? CredentialId);
    private sealed record ProcessInstance(Guid Id, string ProcessId);
    private sealed record ProcessInstanceDetails(Guid Id, Dictionary<string, object> Variables);
    private sealed record MessageCorrelationResult(string ResultType, string ProcessInstanceId);
    private sealed record CredentialCreated(string Id);
}
