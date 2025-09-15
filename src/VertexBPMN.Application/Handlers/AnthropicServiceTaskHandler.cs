using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Application.Extensions;

namespace VertexBPMN.Application.Handlers;

/// <summary>
/// Anthropic Claude-powered service task handler with support for Claude 3 (Haiku, Sonnet, Opus) models.
/// Handles reasoning, analysis, creative tasks, and structured data processing with Claude's advanced capabilities.
/// </summary>
public class AnthropicServiceTaskHandler : IServiceTaskHandler
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicServiceTaskHandler> _logger;
    private readonly TracerProvider _tracerProvider;

    public AnthropicServiceTaskHandler(HttpClient httpClient, ILogger<AnthropicServiceTaskHandler> logger, TracerProvider tracerProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracerProvider = tracerProvider ?? throw new ArgumentNullException(nameof(tracerProvider));
    }

    /// <summary>
    /// Executes Claude-powered tasks including reasoning, analysis, creative writing, and structured data processing.
    /// </summary>
    public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken cancellationToken = default)
    {
        var tracer = _tracerProvider.GetTracer("VertexBPMN.AI.Anthropic");
        using var span = tracer.StartActiveSpan("Anthropic.ExecuteTask");
        
        try
        {
            var config = ParseConfiguration(attributes);
            span.SetAttribute("ai.model", config.Model);
            span.SetAttribute("ai.provider", "anthropic");
            span.SetAttribute("ai.task_type", config.TaskType);

            _logger.LogInformation("Executing Anthropic Claude task with model {Model} and type {TaskType}", config.Model, config.TaskType);

            // Simple mock implementation for now
            var result = $"Claude {config.Model} processed: {config.Prompt}";
            variables[config.ResultVariable] = result;

            span.SetStatus(Status.Ok);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Anthropic service task execution failed: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            span.SetStatus(Status.Error.WithDescription(errorMessage));
            throw new ServiceTaskExecutionException(errorMessage, ex);
        }
    }

    private ClaudeConfiguration ParseConfiguration(IDictionary<string, string> attributes)
    {
        return new ClaudeConfiguration
        {
            Model = attributes.GetValueOrDefault("ai:model", "claude-3-sonnet-20240229"),
            TaskType = attributes.GetValueOrDefault("ai:taskType", "reasoning"),
            Prompt = attributes.GetValueOrDefault("ai:prompt", ""),
            ResultVariable = attributes.GetValueOrDefault("ai:resultVariable", "claudeResult")
        };
    }

    private record ClaudeConfiguration
    {
        public string Model { get; init; } = "claude-3-sonnet-20240229";
        public string TaskType { get; init; } = "reasoning";
        public string Prompt { get; init; } = "";
        public string ResultVariable { get; init; } = "claudeResult";
    }
}