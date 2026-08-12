using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpBpmnEngineServiceTests
{
    [Fact]
    public async Task DeployXmlAsync_PostsRepositoryContract()
    {
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Key = "InvoiceProcess",
            Name = "invoice.bpmn"
        };
        var (service, requests) = CreateService(JsonResponse(definition, HttpStatusCode.Created));

        var result = await service.DeployXmlAsync("<definitions />", "invoice.bpmn", "tenant-a");

        Assert.Equal(definition.Id, result.Id);
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://api.test/api/repository", request.RequestUri!.ToString());
        var body = await request.Content!.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("<definitions />", body.GetProperty("bpmnXml").GetString());
        Assert.Equal("invoice.bpmn", body.GetProperty("name").GetString());
        Assert.Equal("tenant-a", body.GetProperty("tenantId").GetString());
    }

    [Fact]
    public async Task StartProcessAsync_PostsRuntimeStartContract()
    {
        var instance = new ProcessInstance { Id = Guid.NewGuid(), ProcessId = "InvoiceProcess" };
        var (service, requests) = CreateService(JsonResponse(instance));
        var variables = new Dictionary<string, object?> { ["approved"] = true };

        var result = await service.StartProcessAsync("InvoiceProcess", variables, "business-42", "tenant-a");

        Assert.Equal(instance.Id, result.Id);
        var request = Assert.Single(requests);
        Assert.Equal("http://api.test/api/runtime/start", request.RequestUri!.ToString());
        var body = await request.Content!.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InvoiceProcess", body.GetProperty("processDefinitionKey").GetString());
        Assert.Equal("business-42", body.GetProperty("businessKey").GetString());
        Assert.True(body.GetProperty("variables").GetProperty("approved").GetBoolean());
    }

    [Fact]
    public async Task CompleteTaskAsync_PostsTaskVariables()
    {
        var taskId = Guid.NewGuid();
        var (service, requests) = CreateService(new HttpResponseMessage(HttpStatusCode.NoContent));

        await service.CompleteTaskAsync(taskId, new Dictionary<string, object?> { ["formData"] = "{}" });

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"http://api.test/api/task/{taskId}/complete", request.RequestUri!.ToString());
        var body = await request.Content!.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("{}", body.GetProperty("variables").GetProperty("formData").GetString());
    }

    [Fact]
    public async Task TenantAwareDefinitionOperations_ForwardSelectedTenant()
    {
        var definitionId = Guid.NewGuid();
        var definition = new ProcessDefinition { Id = definitionId, BpmnXml = "<definitions />" };
        var (service, requests) = CreateService(JsonResponse(definition));

        Assert.Equal("<definitions />", await service.GetProcessDefinitionXmlAsync(definitionId.ToString(), "tenant-a"));
        await service.DeleteProcessDefinitionAsync(definitionId, "tenant-a");

        Assert.Equal(2, requests.Count);
        Assert.Equal($"http://api.test/api/repository/{definitionId}?tenantId=tenant-a", requests[0].RequestUri!.ToString());
        Assert.Equal($"http://api.test/api/repository/{definitionId}?tenantId=tenant-a", requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task CompleteTaskAsync_ForwardsSelectedTenant()
    {
        var taskId = Guid.NewGuid();
        var (service, requests) = CreateService(new HttpResponseMessage(HttpStatusCode.NoContent));

        await service.CompleteTaskAsync(taskId, null, "tenant-a");

        var request = Assert.Single(requests);
        var body = await request.Content!.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("tenant-a", body.GetProperty("tenantId").GetString());
    }

    [Fact]
    public async Task GetEngineConfigurationAsync_UsesCapabilitiesEndpoint()
    {
        var capabilities = new
        {
            engineType = "Distributed",
            supportsCmmn = true,
            supportsWorkers = true,
            supportsDurablePersistence = true
        };
        var (service, requests) = CreateService(JsonResponse(capabilities));

        var result = await service.GetEngineConfigurationAsync();

        Assert.Contains("Distributed", result.StatusMessage);
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://api.test/api/engine/capabilities", request.RequestUri!.ToString());
    }

    private static (HttpBpmnEngineService Service, List<HttpRequestMessage> Requests) CreateService(HttpResponseMessage response)
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingHandler(requests, response);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        return (new HttpBpmnEngineService(factory.Object, NullLogger<HttpBpmnEngineService>.Instance), requests);
    }

    private static HttpResponseMessage JsonResponse<T>(T value, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = JsonContent.Create(value)
        };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<HttpRequestMessage> _requests;
        private readonly HttpResponseMessage _response;

        public RecordingHandler(List<HttpRequestMessage> requests, HttpResponseMessage response)
        {
            _requests = requests;
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requests.Add(request);
            return Task.FromResult(_response);
        }
    }
}
