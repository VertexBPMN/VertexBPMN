using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Entities.Modeling;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

public class XAiDecisionService : IAiDecisionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<XAiDecisionService> _logger;
    private readonly Tracer _tracer;
    private readonly string _apiEndpoint = "https://api.x.ai/grok";
    private readonly string _aiApiEndpoint = "https://api.x.ai/ml-predict";
    private readonly string _mcpServerEndpoint = "http://mcp-server:8080/api/mcp"; // Konfigurierbar
    public XAiDecisionService(HttpClient httpClient, ILogger<XAiDecisionService> logger,  TracerProvider tracerProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracer = tracerProvider.GetTracer("VertexBPMN");
    }

    public async Task<PlanItem> GenerateAdHocSubprocessAsync(string caseId, Dictionary<string, object> caseFile, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                caseId,
                caseFile,
                prompt = "Generate an ad-hoc subprocess for a CMMN case based on the provided case file. Return a PlanItem with Type='adHocSubprocess' and appropriate attributes."
            };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_apiEndpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var aiResponse = JsonSerializer.Deserialize<PlanItem>(responseContent) ?? throw new DistributedTokenException("Invalid AI response");

            if (aiResponse.Type != "adHocSubprocess")
                throw new DistributedTokenException("AI-generated PlanItem must be of type adHocSubprocess");

            _logger.LogInformation("Generated ad-hoc subprocess for case {CaseId}", caseId);
            return aiResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate ad-hoc subprocess for case {CaseId}", caseId);
            throw new DistributedTokenException($"Failed to generate ad-hoc subprocess for case {caseId}", ex);
        }
    }

    public async Task<List<PlanItem>> PredictOptimalPlanItemsAsync(string caseId, Dictionary<string, object> caseFile, List<HistoricalCaseData> historicalData, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("PredictOptimalPlanItems");
        span.SetAttribute("caseId", caseId);
        try
        {
            // Externe Kontextdaten via MCP abrufen
            var externalContext = await FetchExternalContextAsync(caseId, "external_workflow_data", cancellationToken);
            // Beispiel: Steuerung eines externen MCP-Servers, um eine Aktion auszuführen
            if (externalContext.ContainsKey("requiresApproval"))
            {
                await ExecuteMcpActionAsync(caseId, _mcpServerEndpoint, "trigger_approval", new Dictionary<string, object>
                {
                    { "caseId", caseId },
                    { "documentId", externalContext["documentId"] }
                }, cancellationToken);
            }

            var request = new
            {
                caseId,
                caseFile,
                historicalData,
                externalContext,
                prompt = "Predict optimal PlanItems for a CMMN case based on case file, historical data, and external context."
            };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_apiEndpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var planItems = JsonSerializer.Deserialize<List<PlanItem>>(responseContent) ?? throw new DistributedTokenException("Invalid AI response");

            _logger.LogInformation("Predicted {Count} optimal PlanItems for case {CaseId}", planItems.Count, caseId);
            span.SetStatus(Status.Ok);
            return planItems;
        }
        catch (Exception ex)
        {
            span.SetStatus(Status.Error.WithDescription(ex.Message));
            _logger.LogError(ex, "Failed to predict optimal PlanItems for case {CaseId}", caseId);
            throw new DistributedTokenException($"Failed to predict optimal PlanItems for case {caseId}", ex);
        }
    }


    public async Task<Dictionary<string, object>> FetchExternalContextAsync(string caseId, string resourceId, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("FetchExternalContext");
        span.SetAttribute("caseId", caseId);
        span.SetAttribute("resourceId", resourceId);

        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                method = "get_resource",
                @params = new { caseId, resourceId },
                id = Guid.NewGuid().ToString()
            };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_mcpServerEndpoint, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonResponse = JsonSerializer.Deserialize<JsonRpcResponse>(responseContent)
                               ?? throw new DistributedTokenException("Invalid MCP response");

            if (jsonResponse.Error != null)
                throw new DistributedTokenException($"MCP error: {jsonResponse.Error.Message}");

            var context = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse.Result?.ToString() ?? "{}")
                          ?? throw new DistributedTokenException("Invalid MCP context data");

            _logger.LogInformation("Fetched external context for case {CaseId}, resource {ResourceId}", caseId, resourceId);
            span.SetStatus(Status.Ok);
            return context;
        }
        catch (Exception ex)
        {
            span.SetStatus(Status.Error.WithDescription(ex.Message));
            _logger.LogError(ex, "Failed to fetch external context for case {CaseId}, resource {ResourceId}", caseId, resourceId);
            throw new DistributedTokenException($"Failed to fetch external context for case {caseId}, resource {resourceId}", ex);
        }
    }


    public async Task ExecuteMcpActionAsync(string caseId, string mcpServerUrl, string method, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("ExecuteMcpAction");
        span.SetAttribute("caseId", caseId);
        span.SetAttribute("mcpServerUrl", mcpServerUrl);
        span.SetAttribute("method", method);

        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                method,
                @params = parameters,
                id = Guid.NewGuid().ToString()
            };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(mcpServerUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonResponse = JsonSerializer.Deserialize<JsonRpcResponse>(responseContent)
                ?? throw new DistributedTokenException("Invalid MCP response");

            if (jsonResponse.Error != null)
                throw new DistributedTokenException($"MCP error: {jsonResponse.Error.Message}");

            _logger.LogInformation("Executed MCP action {Method} for case {CaseId} on server {McpServerUrl}", method, caseId, mcpServerUrl);
            span.SetStatus(Status.Ok);
        }
        catch (Exception ex)
        {
            span.SetStatus(Status.Error.WithDescription(ex.Message));
            _logger.LogError(ex, "Failed to execute MCP action {Method} for case {CaseId} on server {McpServerUrl}", method, caseId, mcpServerUrl);
            throw new DistributedTokenException($"Failed to execute MCP action {method} for case {caseId}", ex);
        }
    }

}

