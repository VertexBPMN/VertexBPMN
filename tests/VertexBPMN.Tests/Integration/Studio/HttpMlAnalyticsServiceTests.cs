using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpMlAnalyticsServiceTests
{
    [Fact]
    public async Task PredictDurationAsync_PostsDurationContract()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { estimatedDurationMinutes = 12.5 })
        };
        var client = new HttpClient(new RecordingHandler(requests, response))
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpMlAnalyticsService(factory.Object);

        var result = await service.PredictDurationAsync("invoice", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(12.5, result.GetProperty("estimatedDurationMinutes").GetDouble());
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://api.test/api/ml/predict/duration", request.RequestUri!.ToString());
        var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("invoice", body.GetProperty("processDefinitionKey").GetString());
    }

    [Fact]
    public async Task PredictBottlenecksAsync_EscapesProcessDefinitionKey()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { overallBottleneckRisk = 0.4 })
        };
        var client = new HttpClient(new RecordingHandler(requests, response))
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpMlAnalyticsService(factory.Object);

        await service.PredictBottlenecksAsync("invoice/v2", cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal("http://api.test/api/ml/predict/bottlenecks/invoice%2Fv2", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task TrainModelsAsync_PostsToTrainEndpoint()
    {
        var requests = new List<HttpRequestMessage>();
        var client = new HttpClient(new RecordingHandler(
            requests,
            new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpMlAnalyticsService(factory.Object);

        await service.TrainModelsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://api.test/api/ml/train", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task ExportTrainingDataAsync_GetsCsvFromExportEndpoint()
    {
        var requests = new List<HttpRequestMessage>();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("tenantId,processDefinitionKey\n")
        };
        var client = new HttpClient(new RecordingHandler(requests, response))
        {
            BaseAddress = new Uri("http://api.test/")
        };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpMlAnalyticsService(factory.Object);

        var csv = await service.ExportTrainingDataAsync("invoice/v2", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("tenantId,processDefinitionKey\n", System.Text.Encoding.UTF8.GetString(csv));
        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "http://api.test/api/ml/export/training-data?processDefinitionKey=invoice%2Fv2",
            request.RequestUri!.ToString());
    }

    private sealed class RecordingHandler(
        List<HttpRequestMessage> requests,
        HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            requests.Add(request);
            return Task.FromResult(response);
        }
    }
}
