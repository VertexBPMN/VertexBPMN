extern alias api;

using Grpc.Net.Client;
using Grpc.Core;
using VertexBPMN.Tests.Infrastructure;
using ApiExecuteCaseRequest = api::VertexBPMN.Api.Grpc.ExecuteCaseRequest;
using ApiGenerateAdHocSubprocessRequest = api::VertexBPMN.Api.Grpc.GenerateAdHocSubprocessRequest;
using ApiRegisterCmmnRequest = api::VertexBPMN.Api.Grpc.RegisterCmmnRequest;
using ApiTriggerEventRequest = api::VertexBPMN.Api.Grpc.TriggerEventRequest;
using ApiCaseFileUpdateRequest = api::VertexBPMN.Api.Grpc.CaseFileUpdateRequest;
using McpExecuteCaseRequest = api::VertexBPMN.Api.Grpc.Mcp.ExecuteCaseRequest;
using McpTriggerEventRequest = api::VertexBPMN.Api.Grpc.Mcp.TriggerEventRequest;
using McpCaseFileUpdateRequest = api::VertexBPMN.Api.Grpc.Mcp.CaseFileUpdateRequest;
using McpGenerateAdHocSubprocessRequest = api::VertexBPMN.Api.Grpc.Mcp.GenerateAdHocSubprocessRequest;
using McpHistoricalContextRequest = api::VertexBPMN.Api.Grpc.Mcp.HistoricalContextRequest;

namespace VertexBPMN.Tests.Integration.Api;

public sealed class GrpcContractTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly GrpcChannel _channel;

    public GrpcContractTests()
    {
        _factory = new CustomWebApplicationFactory();
        var httpClient = _factory.CreateClient();
        _channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = httpClient
        });
    }

    public void Dispose()
    {
        _channel.Dispose();
        _factory.Dispose();
    }

    [Fact]
    [Trait("Category", "Phase4Acceptance")]
    public async Task DefaultProfile_CmmnGrpcUsesQualifiedPersistentRuntime()
    {
        using var disabledFactory = new CustomWebApplicationFactory();
        using var httpClient = disabledFactory.CreateClient();
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = httpClient
        });
        var client = new api::VertexBPMN.Api.Grpc.VertexBPMNService.VertexBPMNServiceClient(channel);

        const string key = "default-qualified-cmmn";
        var registration = await client.RegisterCmmnModelAsync(new ApiRegisterCmmnRequest
        {
            CaseId = key,
            CmmnXml = EmptyCase(key)
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(key, registration.Message);
        var execution = await client.ExecuteCaseAsync(
            new ApiExecuteCaseRequest { CaseId = key },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(Guid.TryParse(execution.CaseInstanceId, out _));
        Assert.Contains("CASE_COMPLETED", execution.Trace);
    }

    [Fact]
    public async Task VertexBpmnService_ExposesAllDefinedActions()
    {
        var client = new api::VertexBPMN.Api.Grpc.VertexBPMNService.VertexBPMNServiceClient(_channel);
        const string caseId = "grpc-contract-case";

        var registration = await client.RegisterCmmnModelAsync(new ApiRegisterCmmnRequest
        {
            CaseId = caseId,
            CmmnXml = InteractiveCase(caseId)
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(caseId, registration.Message);

        var execution = await client.ExecuteCaseAsync(new ApiExecuteCaseRequest { CaseId = caseId }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(Guid.TryParse(execution.CaseInstanceId, out _));

        var eventResult = await client.TriggerUserEventAsync(new ApiTriggerEventRequest
        {
            CaseId = execution.CaseInstanceId,
            EventId = "user-event"
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("user-event", eventResult.Message);

        var updateResult = await client.UpdateCaseFileItemAsync(new ApiCaseFileUpdateRequest
        {
            CaseId = execution.CaseInstanceId,
            CaseFileItemId = "item",
            NewValue = "value"
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("item", updateResult.Message);

        var adhocResult = await client.GenerateAdHocSubprocessAsync(new ApiGenerateAdHocSubprocessRequest { CaseId = execution.CaseInstanceId, PlanItemId = "optional-review" }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("optional-review", adhocResult.Message);
    }

    [Fact]
    public async Task McpService_ExposesAllDefinedActions()
    {
        var registrationClient = new api::VertexBPMN.Api.Grpc.VertexBPMNService.VertexBPMNServiceClient(_channel);
        var client = new api::VertexBPMN.Api.Grpc.Mcp.VertexBPMNMCPService.VertexBPMNMCPServiceClient(_channel);
        const string caseId = "grpc-mcp-contract-case";

        await registrationClient.RegisterCmmnModelAsync(new ApiRegisterCmmnRequest
        {
            CaseId = caseId,
            CmmnXml = InteractiveCase(caseId)
        }, cancellationToken: TestContext.Current.CancellationToken);

        var execution = await client.ExecuteCaseAsync(new McpExecuteCaseRequest { CaseId = caseId }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(Guid.TryParse(execution.CaseInstanceId, out _));

        var eventResult = await client.TriggerUserEventAsync(new McpTriggerEventRequest
        {
            CaseId = execution.CaseInstanceId,
            EventId = "user-event"
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("user-event", eventResult.Message);

        var updateResult = await client.UpdateCaseFileItemAsync(new McpCaseFileUpdateRequest
        {
            CaseId = execution.CaseInstanceId,
            CaseFileItemId = "item",
            NewValue = "value"
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("item", updateResult.Message);

        var adhocResult = await client.GenerateAdHocSubprocessAsync(new McpGenerateAdHocSubprocessRequest { CaseId = execution.CaseInstanceId, PlanItemId = "optional-review" }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("optional-review", adhocResult.Message);

        var history = await client.GetHistoricalContextAsync(new McpHistoricalContextRequest { CaseId = execution.CaseInstanceId }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEmpty(history.HistoricalData);
    }

    private static string EmptyCase(string id) =>
        $"<definitions xmlns='https://www.omg.org/spec/CMMN/20151109/MODEL'><case id='{id}'><casePlanModel id='plan'/></case></definitions>";

    private static string InteractiveCase(string id) => $"""
        <definitions xmlns="https://www.omg.org/spec/CMMN/20151109/MODEL">
          <case id="{id}" name="Interactive case">
            <casePlanModel id="plan">
              <planItem id="hold" definitionRef="holdDefinition" />
              <planItem id="event-listener" definitionRef="eventDefinition" />
              <planningTable>
                <discretionaryItem id="optional-review" definitionRef="optionalDefinition" />
              </planningTable>
              <humanTask id="holdDefinition" />
              <userEventListener id="eventDefinition" name="user-event" />
              <manualTask id="optionalDefinition" />
              <caseFileItem id="item" name="Item" />
            </casePlanModel>
          </case>
        </definitions>
        """;
}
