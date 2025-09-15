using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Application.Extensions;

namespace VertexBPMN.Application.Handlers;

/// <summary>
/// Google Gemini-powered service task handler with support for Gemini Pro and Gemini Pro Vision models.
/// Handles multimodal AI tasks including text generation, image analysis, code generation, and structured reasoning.
/// </summary>
public class GeminiServiceTaskHandler : IServiceTaskHandler
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiServiceTaskHandler> _logger;
    private readonly TracerProvider _tracerProvider;

    public GeminiServiceTaskHandler(HttpClient httpClient, ILogger<GeminiServiceTaskHandler> logger, TracerProvider tracerProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracerProvider = tracerProvider ?? throw new ArgumentNullException(nameof(tracerProvider));
    }

    /// <summary>
    /// Executes Gemini-powered tasks including text generation, multimodal analysis, coding, and structured reasoning.
    /// </summary>
    public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken cancellationToken = default)
    {
        var tracer = _tracerProvider.GetTracer("VertexBPMN.AI.Gemini");
        using var span = tracer.StartActiveSpan("Gemini.ExecuteTask");
        
        try
        {
            var config = ParseConfiguration(attributes);
            span.SetAttribute("ai.model", config.Model);
            span.SetAttribute("ai.provider", "google");
            span.SetAttribute("ai.task_type", config.TaskType);

            _logger.LogInformation("Executing Gemini task with model {Model} and type {TaskType}", config.Model, config.TaskType);

            // Simple mock implementation for now
            var result = $"Gemini {config.Model} processed: {config.Prompt}";
            variables[config.ResultVariable] = result;

            span.SetStatus(Status.Ok);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Gemini service task execution failed: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            span.SetStatus(Status.Error.WithDescription(errorMessage));
            throw new ServiceTaskExecutionException(errorMessage, ex);
        }
    }

    private GeminiConfiguration ParseConfiguration(IDictionary<string, string> attributes)
    {
        return new GeminiConfiguration
        {
            Model = attributes.GetValueOrDefault("ai:model", "gemini-pro"),
            TaskType = attributes.GetValueOrDefault("ai:taskType", "generation"),
            Prompt = attributes.GetValueOrDefault("ai:prompt", ""),
            ResultVariable = attributes.GetValueOrDefault("ai:resultVariable", "geminiResult")
        };
    }

    private record GeminiConfiguration
    {
        public string Model { get; init; } = "gemini-pro";
        public string TaskType { get; init; } = "generation";
        public string Prompt { get; init; } = "";
        public string ResultVariable { get; init; } = "geminiResult";
    }
}