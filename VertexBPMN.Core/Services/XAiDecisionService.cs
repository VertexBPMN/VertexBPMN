using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VertexBPMN.Core.Cmmn;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Exceptions;

namespace VertexBPMN.Core.Services;

public class XAiDecisionService : IAiDecisionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<XAiDecisionService> _logger;
    private readonly string _apiEndpoint = "https://api.x.ai/grok";

    public XAiDecisionService(HttpClient httpClient, ILogger<XAiDecisionService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
}