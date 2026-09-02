using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using VertexBPMN.Domain.Entities.ML;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// ML Predictive Analytics Controller
/// Olympic-level feature: Innovation Differentiators - Machine Learning
/// </summary>
[ApiController]
[Route("api/ml")]
[Authorize]
public class MLAnalyticsController : ControllerBase
{
    private readonly IPredictiveAnalyticsService _analyticsService;
    private readonly ILogger<MLAnalyticsController> _logger;

    public MLAnalyticsController(
        IPredictiveAnalyticsService analyticsService,
        ILogger<MLAnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    /// <summary>
    /// Predict process completion probability and timeline
    /// </summary>
    [HttpGet("predict/completion/{processInstanceId}")]
    public async Task<ActionResult<ProcessCompletionPrediction>> PredictCompletion(Guid processInstanceId, [FromQuery] string? tenantId = null)
    {
        try
        {
            var prediction = await _analyticsService.PredictProcessCompletionAsync(processInstanceId, tenantId);
            return Ok(prediction);
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(501, new ProblemDetails { Title = "Predictive analytics is unavailable", Detail = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error predicting completion for process {ProcessInstanceId}", processInstanceId);
            return StatusCode(500, new { error = "Failed to predict process completion" });
        }
    }

    /// <summary>
    /// Predict process duration based on definition and variables
    /// </summary>
    [HttpPost("predict/duration")]
    public async Task<ActionResult<ProcessDurationPrediction>> PredictDuration([FromBody] DurationPredictionRequest request, [FromQuery] string? tenantId = null)
    {
        try
        {
            var prediction = await _analyticsService.PredictProcessDurationAsync(request.ProcessDefinitionKey, request.Variables, tenantId ?? request.TenantId);
            return Ok(prediction);
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(501, new ProblemDetails { Title = "Predictive analytics is unavailable", Detail = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error predicting duration for process {ProcessKey}", request.ProcessDefinitionKey);
            return StatusCode(500, new { error = "Failed to predict process duration" });
        }
    }

    /// <summary>
    /// Predict potential bottlenecks in process execution
    /// </summary>
    [HttpGet("predict/bottlenecks/{processDefinitionKey}")]
    public async Task<ActionResult<ProcessBottleneckPrediction>> PredictBottlenecks(string processDefinitionKey, [FromQuery] string? tenantId = null)
    {
        try
        {
            var prediction = await _analyticsService.PredictBottlenecksAsync(processDefinitionKey, tenantId);
            return Ok(prediction);
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(501, new ProblemDetails { Title = "Predictive analytics is unavailable", Detail = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error predicting bottlenecks for process {ProcessKey}", processDefinitionKey);
            return StatusCode(500, new { error = "Failed to predict process bottlenecks" });
        }
    }

    /// <summary>
    /// Get AI-powered optimization suggestions
    /// </summary>
    [HttpGet("optimize/{processDefinitionKey}")]
    public async Task<ActionResult<ProcessOptimizationSuggestion>> GetOptimizationSuggestions(string processDefinitionKey, [FromQuery] string? tenantId = null)
    {
        try
        {
            var suggestions = await _analyticsService.GetOptimizationSuggestionsAsync(processDefinitionKey, tenantId);
            return Ok(suggestions);
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(501, new ProblemDetails { Title = "Predictive analytics is unavailable", Detail = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting optimization suggestions for process {ProcessKey}", processDefinitionKey);
            return StatusCode(500, new { error = "Failed to get optimization suggestions" });
        }
    }

    /// <summary>
    /// Train ML models with latest process data
    /// </summary>
    [HttpPost("train")]
    public async Task<ActionResult> TrainModels([FromQuery] string? tenantId = null)
    {
        try
        {
            await _analyticsService.TrainModelsAsync(tenantId);
            return Ok(new { message = "ML models training started successfully" });
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(501, new ProblemDetails { Title = "Predictive analytics is unavailable", Detail = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error training ML models");
            return StatusCode(500, new { error = "Failed to start ML model training" });
        }
    }
    /// <summary>
    /// Export the persisted, tenant-scoped training rows used by predictive analytics.
    /// </summary>
    [HttpGet("export/training-data")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportTrainingData([FromQuery] string? processDefinitionKey = null, [FromQuery] string? tenantId = null)
    {
        try
        {
            var csv = await _analyticsService.ExportTrainingDataAsync(processDefinitionKey, tenantId);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "vertexbpmn-training-data.csv");
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(501, new ProblemDetails { Title = "Training data export is unavailable", Detail = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}

public class DurationPredictionRequest
{
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? TenantId { get; set; }
}
