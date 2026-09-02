using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Integration.Studio;

public sealed class HttpMigrationServiceTests
{
    [Fact]
    public async Task PreviewAsync_PostsPreviewRequest()
    {
        var requests = new List<HttpRequestMessage>();
        var client = CreateClient(requests, new { source = "source", target = "target" });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpMigrationService(factory.Object);

        await service.PreviewAsync("source", "target", TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://api.test/api/process-migration/plan/preview", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_PostsPlanToExecuteEndpoint()
    {
        var requests = new List<HttpRequestMessage>();
        var client = CreateClient(requests, new { success = true });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpMigrationService(factory.Object);

        using var document = JsonDocument.Parse("{\"sourceProcessDefinitionId\":\"source\",\"targetProcessDefinitionId\":\"target\"}");
        await service.ExecuteAsync(document.RootElement, TestContext.Current.CancellationToken);

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://api.test/api/process-migration/plan/execute", request.RequestUri!.ToString());
    }

    [Theory]
    [InlineData("GetStatusAsync", "/api/migration/status/migration-1", "GET")]
    [InlineData("CreateSnapshotAsync", "/api/migration/snapshot/process-1", "POST")]
    [InlineData("RestoreFromSnapshotAsync", "/api/migration/restore/process-1/snapshot-1", "POST")]
    [InlineData("RollbackAsync", "/api/migration/rollback/migration-1", "POST")]
    public async Task LiveMigrationOperations_UseExpectedRoutes(string operation, string expectedPath, string expectedMethod)
    {
        var requests = new List<HttpRequestMessage>();
        var client = CreateClient(requests, new { success = true });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(client);
        var service = new HttpMigrationService(factory.Object);

        switch (operation)
        {
            case "GetStatusAsync":
                await service.GetStatusAsync("migration-1", TestContext.Current.CancellationToken);
                break;
            case "CreateSnapshotAsync":
                await service.CreateSnapshotAsync("process-1", TestContext.Current.CancellationToken);
                break;
            case "RestoreFromSnapshotAsync":
                await service.RestoreFromSnapshotAsync("process-1", "snapshot-1", TestContext.Current.CancellationToken);
                break;
            case "RollbackAsync":
                await service.RollbackAsync("migration-1", TestContext.Current.CancellationToken);
                break;
        }

        var request = Assert.Single(requests);
        Assert.Equal(new HttpMethod(expectedMethod), request.Method);
        Assert.Equal($"http://api.test{expectedPath}", request.RequestUri!.ToString());
    }

    private static HttpClient CreateClient(List<HttpRequestMessage> requests, object body)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body)
        };
        return new HttpClient(new RecordingHandler(requests, response)) { BaseAddress = new Uri("http://api.test/") };
    }

    private sealed class RecordingHandler(List<HttpRequestMessage> requests, HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(request);
            return Task.FromResult(response);
        }
    }
}
