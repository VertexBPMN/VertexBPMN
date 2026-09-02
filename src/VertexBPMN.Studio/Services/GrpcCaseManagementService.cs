using Grpc.Net.Client;
using VertexBPMN.Api.Grpc;
using Mcp = VertexBPMN.Api.Grpc.Mcp;

namespace VertexBPMN.Studio.Services;

public sealed class GrpcCaseManagementService : ICaseManagementService, IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly VertexBPMNService.VertexBPMNServiceClient _client;
    private readonly Mcp.VertexBPMNMCPService.VertexBPMNMCPServiceClient _mcpClient;

    public GrpcCaseManagementService(IConfiguration configuration)
    {
        var apiBaseUrl = configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(apiBaseUrl) || !Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri))
            throw new InvalidOperationException("ApiBaseUrl must be an absolute URI.");

        _channel = GrpcChannel.ForAddress(apiUri);
        _client = new VertexBPMNService.VertexBPMNServiceClient(_channel);
        _mcpClient = new Mcp.VertexBPMNMCPService.VertexBPMNMCPServiceClient(_channel);
    }

    public async Task RegisterModelAsync(string caseId, string cmmnXml, CancellationToken cancellationToken = default)
    {
        await _client.RegisterCmmnModelAsync(
            new RegisterCmmnRequest { CaseId = caseId, CmmnXml = cmmnXml },
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ExecuteCaseAsync(string caseId, CancellationToken cancellationToken = default)
    {
        var response = await _client.ExecuteCaseAsync(
            new ExecuteCaseRequest { CaseId = caseId },
            cancellationToken: cancellationToken);
        return response.Trace;
    }

    public async Task TriggerUserEventAsync(
        string caseId,
        string eventId,
        IReadOnlyDictionary<string, string> eventData,
        CancellationToken cancellationToken = default)
    {
        var request = new TriggerEventRequest { CaseId = caseId, EventId = eventId };
        foreach (var pair in eventData)
            request.EventData[pair.Key] = pair.Value;
        await _client.TriggerUserEventAsync(request, cancellationToken: cancellationToken);
    }

    public async Task UpdateCaseFileItemAsync(
        string caseId,
        string itemId,
        string value,
        CancellationToken cancellationToken = default)
    {
        await _client.UpdateCaseFileItemAsync(
            new CaseFileUpdateRequest
            {
                CaseId = caseId,
                CaseFileItemId = itemId,
                NewValue = value
            },
            cancellationToken: cancellationToken);
    }

    public async Task GenerateAdHocSubprocessAsync(string caseId, CancellationToken cancellationToken = default)
    {
        await _client.GenerateAdHocSubprocessAsync(
            new GenerateAdHocSubprocessRequest { CaseId = caseId },
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<HistoricalCaseSnapshot>> GetHistoricalContextAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        var response = await _mcpClient.GetHistoricalContextAsync(
            new Mcp.HistoricalContextRequest { CaseId = caseId },
            cancellationToken: cancellationToken);

        return response.HistoricalData.Select(entry => new HistoricalCaseSnapshot(
            entry.CaseId,
            entry.CaseFile.ToDictionary(pair => pair.Key, pair => pair.Value),
            entry.CompletedPlanItems.ToList(),
            DateTime.Parse(entry.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind))).ToList();
    }

    public void Dispose() => _channel.Dispose();
}
