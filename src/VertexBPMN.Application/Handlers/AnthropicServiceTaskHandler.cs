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
    private readonly TracerProvider? _tracerProvider;
    private const string BaseUrl = "https://api.anthropic.com/v1/messages";

    public AnthropicServiceTaskHandler(HttpClient httpClient, ILogger<AnthropicServiceTaskHandler> logger, TracerProvider? tracerProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracerProvider = tracerProvider;
    }

    /// <summary>
    /// Executes Claude-powered tasks including reasoning, analysis, creative writing, and structured data processing.
    /// </summary>
    public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken cancellationToken = default)
    {
        var tracer = _tracerProvider?.GetTracer("VertexBPMN.AI.Anthropic");
        using var span = tracer?.StartActiveSpan("Anthropic.ExecuteTask");
        
        try
        {
            var config = ParseConfiguration(attributes);
            span?.SetAttribute("ai.model", config.Model);
            span?.SetAttribute("ai.provider", "anthropic");
            span?.SetAttribute("ai.task_type", config.TaskType);

            _logger.LogInformation("Executing Anthropic Claude task with model {Model} and type {TaskType}", config.Model, config.TaskType);

            // Check if we should use mock mode (for testing)
            if (config.UseMockMode)
            {
                var mockResult = $"Claude {config.Model} processed: {config.Prompt}";
                variables[config.ResultVariable] = mockResult;
                span?.SetStatus(Status.Ok);
                return;
            }

            // Get API key
            var apiKey = GetApiKey(attributes);

            // Build context from input variables
            var context = BuildContextString(config.InputVariables, variables);
            var userMessage = BuildClaudePrompt(config, context);

            // Prepare Anthropic API request
            var requestBody = new
            {
                model = config.Model,
                max_tokens = config.MaxTokens,
                temperature = config.Temperature,
                system = config.SystemMessage,
                messages = new[]
                {
                    new { role = "user", content = userMessage }
                }
            };

            var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // Add Anthropic API key and headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            span?.SetAttribute("ai.request_size", requestJson.Length);

            // Execute Anthropic API call
            var response = await _httpClient.PostAsync(BaseUrl, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"Anthropic API error: {response.StatusCode} - {errorContent}";
                _logger.LogError(errorMessage);
                span?.SetStatus(Status.Error.WithDescription(errorMessage));
                throw new ServiceTaskExecutionException(errorMessage);
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseJson, JsonOptions);

            if (claudeResponse?.Content?.Any() != true)
            {
                throw new ServiceTaskExecutionException("Anthropic API returned no content");
            }

            // Process and store results
            ProcessClaudeResponse(claudeResponse, config, variables);

            // Log usage metrics
            if (claudeResponse.Usage != null)
            {
                span?.SetAttribute("ai.tokens.input", claudeResponse.Usage.InputTokens);
                span?.SetAttribute("ai.tokens.output", claudeResponse.Usage.OutputTokens);
                
                _logger.LogInformation("Claude task completed. Tokens used: {InputTokens} input, {OutputTokens} output",
                    claudeResponse.Usage.InputTokens, claudeResponse.Usage.OutputTokens);
            }

            span?.SetStatus(Status.Ok);
        }
        catch (Exception ex) when (ex is not ServiceTaskExecutionException)
        {
            var errorMessage = $"Anthropic service task execution failed: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            span?.SetStatus(Status.Error.WithDescription(errorMessage));
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
            SystemMessage = attributes.GetValueOrDefault("ai:systemMessage", "You are Claude, an AI assistant created by Anthropic to help with business process automation tasks."),
            Temperature = double.TryParse(attributes.GetValueOrDefault("ai:temperature", "0.7"), out var temp) ? temp : 0.7,
            MaxTokens = int.TryParse(attributes.GetValueOrDefault("ai:maxTokens", "1000"), out var maxTokens) ? maxTokens : 1000,
            ResultVariable = attributes.GetValueOrDefault("ai:resultVariable", "claudeResult"),
            InputVariables = attributes.GetValueOrDefault("ai:inputVariables", "").Split(',', StringSplitOptions.RemoveEmptyEntries),
            IncludeUsage = attributes.GetValueOrDefault("ai:includeUsage", "false").ToLowerInvariant() == "true",
            UseMockMode = attributes.GetValueOrDefault("ai:useMockMode", "false").ToLowerInvariant() == "true"
        };
    }

    private string BuildClaudePrompt(ClaudeConfiguration config, string context)
    {
        var promptBuilder = new StringBuilder();

        // Add task-specific instructions for Claude
        switch (config.TaskType.ToLowerInvariant())
        {
            case "reasoning":
                promptBuilder.AppendLine("Please think through this step by step and provide your reasoning:");
                break;
            case "analysis":
                promptBuilder.AppendLine("Please provide a thorough analysis of the following:");
                break;
            case "creative":
                promptBuilder.AppendLine("Please be creative and innovative in your response:");
                break;
        }

        promptBuilder.AppendLine(config.Prompt);

        if (!string.IsNullOrEmpty(context))
        {
            promptBuilder.AppendLine("\nContext information:");
            promptBuilder.AppendLine(context);
        }

        return promptBuilder.ToString();
    }

    private void ProcessClaudeResponse(ClaudeResponse response, ClaudeConfiguration config, IDictionary<string, object> variables)
    {
        var content = response.Content.FirstOrDefault()?.Text ?? "";

        // Store main result
        variables[config.ResultVariable] = content;

        // Store response metadata
        variables[$"{config.ResultVariable}_model"] = response.Model;
        variables[$"{config.ResultVariable}_stopReason"] = response.StopReason;

        if (config.IncludeUsage && response.Usage != null)
        {
            variables[$"{config.ResultVariable}_usage"] = new Dictionary<string, object>
            {
                ["inputTokens"] = response.Usage.InputTokens,
                ["outputTokens"] = response.Usage.OutputTokens
            };
        }
    }

    private string BuildContextString(string[] inputVariables, IDictionary<string, object> variables)
    {
        if (inputVariables.Length == 0) return string.Empty;

        var contextBuilder = new StringBuilder();
        foreach (var varName in inputVariables)
        {
            if (variables.TryGetValue(varName.Trim(), out var value))
            {
                contextBuilder.AppendLine($"{varName}: {value}");
            }
        }
        return contextBuilder.ToString();
    }

    private string GetApiKey(IDictionary<string, string> attributes)
    {
        var apiKey = attributes.GetValueOrDefault("ai:apiKey", "") 
                     ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                     ?? throw new ServiceTaskExecutionException("Anthropic API key not found in attributes or environment variables");
        
        return apiKey;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    #region Configuration and Response Models

    private record ClaudeConfiguration
    {
        public string Model { get; init; } = "claude-3-sonnet-20240229";
        public string TaskType { get; init; } = "reasoning";
        public string Prompt { get; init; } = "";
        public string SystemMessage { get; init; } = "";
        public double Temperature { get; init; } = 0.7;
        public int MaxTokens { get; init; } = 1000;
        public string ResultVariable { get; init; } = "claudeResult";
        public string[] InputVariables { get; init; } = [];
        public bool IncludeUsage { get; init; } = false;
        public bool UseMockMode { get; init; } = false;
    }

    private record ClaudeResponse
    {
        public string Id { get; init; } = "";
        public string Type { get; init; } = "";
        public string Role { get; init; } = "";
        public ClaudeContent[] Content { get; init; } = [];
        public string Model { get; init; } = "";
        public string StopReason { get; init; } = "";
        public ClaudeUsage? Usage { get; init; }
    }

    private record ClaudeContent
    {
        public string Type { get; init; } = "";
        public string Text { get; init; } = "";
    }

    private record ClaudeUsage
    {
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }
    }

    #endregion
}