using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Application.Extensions;

namespace VertexBPMN.Application.Handlers;

/// <summary>
/// OpenAI GPT-powered service task handler with support for GPT-3.5, GPT-4, and GPT-4 Turbo models.
/// Handles text generation, analysis, classification, and structured output generation.
/// </summary>
public class OpenAiServiceTaskHandler : IServiceTaskHandler
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiServiceTaskHandler> _logger;
    private readonly TracerProvider? _tracerProvider;
    private readonly ISecretProvider? _secretProvider;
    private const string BaseUrl = "https://api.openai.com/v1/chat/completions";

    public OpenAiServiceTaskHandler(HttpClient httpClient, ILogger<OpenAiServiceTaskHandler> logger, TracerProvider? tracerProvider = null, ISecretProvider? secretProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracerProvider = tracerProvider;
        _secretProvider = secretProvider;
    }

    /// <summary>
    /// Executes OpenAI-powered tasks including text generation, analysis, classification, and data extraction.
    /// </summary>
    public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken cancellationToken = default)
    {
        var tracer = _tracerProvider?.GetTracer("VertexBPMN.AI.OpenAI");
        using var span = tracer?.StartActiveSpan("OpenAI.ExecuteTask");
        
        try
        {
            var config = ParseConfiguration(attributes);
            span?.SetAttribute("ai.model", config.Model);
            span?.SetAttribute("ai.provider", "openai");
            span?.SetAttribute("ai.task_type", config.TaskType);

            _logger.LogInformation("Executing OpenAI task with model {Model} and type {TaskType}", config.Model, config.TaskType);

            // Check if we should use mock mode (for testing)
            if (config.UseMockMode)
            {
                var mockResult = $"OpenAI {config.Model} processed: {config.Prompt}";
                variables[config.ResultVariable] = mockResult;
                span?.SetStatus(Status.Ok);
                return;
            }

            // Get API key
            var apiKey = GetApiKey(attributes);

            // Build context from input variables
            var context = BuildContextString(config.InputVariables, variables);
            
            // Prepare messages
            var messages = new List<object>
            {
                new { role = "system", content = config.SystemMessage }
            };

            // Add user message with prompt and context
            var userMessage = string.IsNullOrEmpty(context) 
                ? config.Prompt 
                : $"{config.Prompt}\n\nContext:\n{context}";
                
            messages.Add(new { role = "user", content = userMessage });

            // Build request
            var requestBody = new
            {
                model = config.Model,
                messages = messages,
                temperature = config.Temperature,
                max_tokens = config.MaxTokens
            };

            var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // Add OpenAI API key
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);

            span?.SetAttribute("ai.request_size", requestJson.Length);

            // Execute OpenAI API call
            var response = await _httpClient.PostAsync(BaseUrl, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"OpenAI API error: {response.StatusCode} - {errorContent}";
                _logger.LogError(errorMessage);
                span?.SetStatus(Status.Error.WithDescription(errorMessage));
                throw new ServiceTaskExecutionException(errorMessage);
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseJson, JsonOptions);

            if (openAiResponse?.Choices?.Any() != true)
            {
                throw new ServiceTaskExecutionException("OpenAI API returned no choices");
            }

            // Process and store results
            ProcessOpenAiResponse(openAiResponse, config, variables);

            // Log usage metrics
            if (openAiResponse.Usage != null)
            {
                span?.SetAttribute("ai.tokens.prompt", openAiResponse.Usage.PromptTokens);
                span?.SetAttribute("ai.tokens.completion", openAiResponse.Usage.CompletionTokens);
                span?.SetAttribute("ai.tokens.total", openAiResponse.Usage.TotalTokens);
                
                _logger.LogInformation("OpenAI task completed. Tokens used: {PromptTokens} prompt, {CompletionTokens} completion, {TotalTokens} total",
                    openAiResponse.Usage.PromptTokens, openAiResponse.Usage.CompletionTokens, openAiResponse.Usage.TotalTokens);
            }

            span?.SetStatus(Status.Ok);
        }
        catch (Exception ex) when (ex is not ServiceTaskExecutionException)
        {
            var errorMessage = $"OpenAI service task execution failed: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            span?.SetStatus(Status.Error.WithDescription(errorMessage));
            throw new ServiceTaskExecutionException(errorMessage, ex);
        }
    }

    private OpenAiConfiguration ParseConfiguration(IDictionary<string, string> attributes)
    {
        return new OpenAiConfiguration
        {
            Model = attributes.GetValueOrDefault("ai:model", "gpt-4"),
            TaskType = attributes.GetValueOrDefault("ai:taskType", "generation"),
            Prompt = attributes.GetValueOrDefault("ai:prompt", ""),
            SystemMessage = attributes.GetValueOrDefault("ai:systemMessage", "You are a helpful assistant for business process automation."),
            Temperature = double.TryParse(attributes.GetValueOrDefault("ai:temperature", "0.7"), out var temp) ? temp : 0.7,
            MaxTokens = int.TryParse(attributes.GetValueOrDefault("ai:maxTokens", "1000"), out var maxTokens) ? maxTokens : 1000,
            ResultVariable = attributes.GetValueOrDefault("ai:resultVariable", "aiResult"),
            InputVariables = attributes.GetValueOrDefault("ai:inputVariables", "").Split(',', StringSplitOptions.RemoveEmptyEntries),
            IncludeUsage = attributes.GetValueOrDefault("ai:includeUsage", "false").ToLowerInvariant() == "true",
            UseMockMode = attributes.GetValueOrDefault("ai:useMockMode", "false").ToLowerInvariant() == "true"
        };
    }

    private void ProcessOpenAiResponse(OpenAiResponse response, OpenAiConfiguration config, IDictionary<string, object> variables)
    {
        var mainChoice = response.Choices.First();
        var content = mainChoice.Message.Content;

        // Store main result
        variables[config.ResultVariable] = content;

        // Store additional response metadata
        variables[$"{config.ResultVariable}_finishReason"] = mainChoice.FinishReason;
        
        if (config.IncludeUsage && response.Usage != null)
        {
            variables[$"{config.ResultVariable}_usage"] = new Dictionary<string, object>
            {
                ["promptTokens"] = response.Usage.PromptTokens,
                ["completionTokens"] = response.Usage.CompletionTokens,
                ["totalTokens"] = response.Usage.TotalTokens
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
        var configuredKey = attributes.GetValueOrDefault("ai:apiKey", "");
        var apiKey = !string.IsNullOrWhiteSpace(configuredKey)
            ? configuredKey
            : _secretProvider?.GetSecret("AI:OpenAI:ApiKey", "OPENAI_API_KEY");

        return apiKey ?? throw new ServiceTaskExecutionException("OpenAI API key not found in attributes, configuration, or environment variables");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    #region Configuration and Response Models

    private record OpenAiConfiguration
    {
        public string Model { get; init; } = "gpt-4";
        public string TaskType { get; init; } = "generation";
        public string Prompt { get; init; } = "";
        public string SystemMessage { get; init; } = "";
        public double Temperature { get; init; } = 0.7;
        public int MaxTokens { get; init; } = 1000;
        public string ResultVariable { get; init; } = "aiResult";
        public string[] InputVariables { get; init; } = [];
        public bool IncludeUsage { get; init; } = false;
        public bool UseMockMode { get; init; } = false;
    }

    private record OpenAiResponse
    {
        public string Id { get; init; } = "";
        public string Object { get; init; } = "";
        public long Created { get; init; }
        public string Model { get; init; } = "";
        public OpenAiChoice[] Choices { get; init; } = [];
        public OpenAiUsage? Usage { get; init; }
    }

    private record OpenAiChoice
    {
        public int Index { get; init; }
        public OpenAiMessage Message { get; init; } = new();
        public string FinishReason { get; init; } = "";
    }

    private record OpenAiMessage
    {
        public string Role { get; init; } = "";
        public string Content { get; init; } = "";
    }

    private record OpenAiUsage
    {
        public int PromptTokens { get; init; }
        public int CompletionTokens { get; init; }
        public int TotalTokens { get; init; }
    }

    #endregion
}