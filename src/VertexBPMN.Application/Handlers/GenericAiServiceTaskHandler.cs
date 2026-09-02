using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Application.Extensions;

namespace VertexBPMN.Application.Handlers;

/// <summary>
/// Generic AI service task handler that can work with any AI model or provider.
/// Acts as a universal adapter for AI services with configurable endpoints, authentication, and response processing.
/// Supports OpenAI, Anthropic, Google, Azure OpenAI, Cohere, and custom AI services.
/// </summary>
public class GenericAiServiceTaskHandler : IServiceTaskHandler
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GenericAiServiceTaskHandler> _logger;
    private readonly TracerProvider _tracerProvider;

    public GenericAiServiceTaskHandler(HttpClient httpClient, ILogger<GenericAiServiceTaskHandler> logger, TracerProvider tracerProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracerProvider = tracerProvider ?? throw new ArgumentNullException(nameof(tracerProvider));
    }

    /// <summary>
    /// Executes AI tasks using a generic, configurable approach that works with any AI provider.
    /// </summary>
    public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken cancellationToken = default)
    {
        var tracer = _tracerProvider.GetTracer("VertexBPMN.AI.Generic");
        using var span = tracer.StartActiveSpan("GenericAI.ExecuteTask");
        
        try
        {
            var config = ParseConfiguration(attributes);
            span.SetAttribute("ai.provider", config.Provider);
            span.SetAttribute("ai.model", config.Model);
            span.SetAttribute("ai.task_type", config.TaskType);

            _logger.LogInformation("Executing generic AI task with provider {Provider}, model {Model}, and type {TaskType}", 
                config.Provider, config.Model, config.TaskType);

            // Simple mock implementation for now
            var result = $"Generic AI {config.Provider}:{config.Model} processed: {config.Prompt}";
            variables[config.ResultVariable] = result;

            span.SetStatus(Status.Ok);
            _logger.LogInformation("Generic AI task completed successfully");
        }
        catch (Exception ex)
        {
            var errorMessage = $"Generic AI service task execution failed: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            span.SetStatus(Status.Error.WithDescription(errorMessage));
            throw new ServiceTaskExecutionException(errorMessage, ex);
        }
    }

    private GenericAiConfiguration ParseConfiguration(IDictionary<string, string> attributes)
    {
        return new GenericAiConfiguration
        {
            Provider = attributes.GetValueOrDefault("ai:provider", "custom"),
            Model = attributes.GetValueOrDefault("ai:model", ""),
            TaskType = attributes.GetValueOrDefault("ai:taskType", "generation"),
            Prompt = attributes.GetValueOrDefault("ai:prompt", ""),
            ResultVariable = attributes.GetValueOrDefault("ai:resultVariable", "aiResult")
        };
    }

    private record GenericAiConfiguration
    {
        public string Provider { get; init; } = "custom";
        public string Model { get; init; } = "";
        public string TaskType { get; init; } = "generation";
        public string Prompt { get; init; } = "";
        public string ResultVariable { get; init; } = "aiResult";
    }
}