using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.ML;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// ML Predictive Analytics Controller
/// Olympic-level feature: Innovation Differentiators - Machine Learning
/// </summary>
[ApiController]
[Route("api/ml")]
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
    public async Task<ActionResult<ProcessCompletionPrediction>> PredictCompletion(Guid processInstanceId)
    {
        try
        {
            var prediction = await _analyticsService.PredictProcessCompletionAsync(processInstanceId);
            return Ok(prediction);
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
    public async Task<ActionResult<ProcessDurationPrediction>> PredictDuration([FromBody] DurationPredictionRequest request)
    {
        try
        {
            var prediction = await _analyticsService.PredictProcessDurationAsync(request.ProcessDefinitionKey, request.Variables);
            return Ok(prediction);
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
    public async Task<ActionResult<ProcessBottleneckPrediction>> PredictBottlenecks(string processDefinitionKey)
    {
        try
        {
            var prediction = await _analyticsService.PredictBottlenecksAsync(processDefinitionKey);
            return Ok(prediction);
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
    public async Task<ActionResult<ProcessOptimizationSuggestion>> GetOptimizationSuggestions(string processDefinitionKey)
    {
        try
        {
            var suggestions = await _analyticsService.GetOptimizationSuggestionsAsync(processDefinitionKey);
            return Ok(suggestions);
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
    public async Task<ActionResult> TrainModels()
    {
        try
        {
            await _analyticsService.TrainModelsAsync();
            return Ok(new { message = "ML models training started successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error training ML models");
            return StatusCode(500, new { error = "Failed to start ML model training" });
        }
    }
}

public class DurationPredictionRequest
{
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
}
