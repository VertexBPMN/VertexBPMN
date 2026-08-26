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
        _factory = new CustomWebApplicationFactory().WithCmmnExecutionEnabled();
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
    public async Task DefaultProfile_CmmnGrpcActionsFailClosed()
    {
        using var disabledFactory = new CustomWebApplicationFactory();
        using var httpClient = disabledFactory.CreateClient();
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = httpClient
        });
        var client = new api::VertexBPMN.Api.Grpc.VertexBPMNService.VertexBPMNServiceClient(channel);

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.RegisterCmmnModelAsync(new ApiRegisterCmmnRequest
            {
                CaseId = "disabled-cmmn",
                CmmnXml = "<definitions />"
            }));

        Assert.Equal(StatusCode.Unimplemented, exception.StatusCode);
    }

    [Fact]
    public async Task VertexBpmnService_ExposesAllDefinedActions()
    {
        var client = new api::VertexBPMN.Api.Grpc.VertexBPMNService.VertexBPMNServiceClient(_channel);
        const string caseId = "grpc-contract-case";

        var registration = await client.RegisterCmmnModelAsync(new ApiRegisterCmmnRequest
        {
            CaseId = caseId,
            CmmnXml = "<definitions xmlns='http://www.omg.org/spec/CMMN/20151109/MODEL'><case id='grpc-contract-case' name='gRPC contract case'><casePlanModel id='case-plan' /></case></definitions>"
        });

        Assert.Contains(caseId, registration.Message);

        var execution = await client.ExecuteCaseAsync(new ApiExecuteCaseRequest { CaseId = caseId });
        Assert.NotNull(execution);

        var eventResult = await client.TriggerUserEventAsync(new ApiTriggerEventRequest
        {
            CaseId = caseId,
            EventId = "user-event"
        });
        Assert.Contains(caseId, eventResult.Message);

        var updateResult = await client.UpdateCaseFileItemAsync(new ApiCaseFileUpdateRequest
        {
            CaseId = caseId,
            CaseFileItemId = "item",
            NewValue = "value"
        });
        Assert.Contains(caseId, updateResult.Message);

        var adhocResult = await client.GenerateAdHocSubprocessAsync(
            new ApiGenerateAdHocSubprocessRequest { CaseId = caseId });
        Assert.Contains(caseId, adhocResult.Message);
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
            CmmnXml = "<definitions xmlns='http://www.omg.org/spec/CMMN/20151109/MODEL'><case id='grpc-mcp-contract-case' name='gRPC MCP contract case'><casePlanModel id='case-plan' /></case></definitions>"
        });

        var execution = await client.ExecuteCaseAsync(new McpExecuteCaseRequest { CaseId = caseId });
        Assert.NotNull(execution);

        var eventResult = await client.TriggerUserEventAsync(new McpTriggerEventRequest
        {
            CaseId = caseId,
            EventId = "user-event"
        });
        Assert.Contains(caseId, eventResult.Message);

        var updateResult = await client.UpdateCaseFileItemAsync(new McpCaseFileUpdateRequest
        {
            CaseId = caseId,
            CaseFileItemId = "item",
            NewValue = "value"
        });
        Assert.Contains(caseId, updateResult.Message);

        var adhocResult = await client.GenerateAdHocSubprocessAsync(
            new McpGenerateAdHocSubprocessRequest { CaseId = caseId });
        Assert.Contains(caseId, adhocResult.Message);

        var history = await client.GetHistoricalContextAsync(
            new McpHistoricalContextRequest { CaseId = caseId });
        Assert.NotNull(history);
    }
}
