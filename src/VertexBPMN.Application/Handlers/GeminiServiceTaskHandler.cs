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
    private readonly TracerProvider? _tracerProvider;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1/models/";

    public GeminiServiceTaskHandler(HttpClient httpClient, ILogger<GeminiServiceTaskHandler> logger, TracerProvider? tracerProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracerProvider = tracerProvider;
    }

    /// <summary>
    /// Executes Gemini-powered tasks including text generation, multimodal analysis, coding, and structured reasoning.
    /// </summary>
    public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken cancellationToken = default)
    {
        var tracer = _tracerProvider?.GetTracer("VertexBPMN.AI.Gemini");
        using var span = tracer?.StartActiveSpan("Gemini.ExecuteTask");
        
        try
        {
            var config = ParseConfiguration(attributes);
            span?.SetAttribute("ai.model", config.Model);
            span?.SetAttribute("ai.provider", "google");
            span?.SetAttribute("ai.task_type", config.TaskType);

            _logger.LogInformation("Executing Gemini task with model {Model} and type {TaskType}", config.Model, config.TaskType);

            // Check if we should use mock mode (for testing)
            if (config.UseMockMode)
            {
                var mockResult = $"Gemini {config.Model} processed: {config.Prompt}";
                variables[config.ResultVariable] = mockResult;
                span?.SetStatus(Status.Ok);
                return;
            }

            // Get API key
            var apiKey = GetApiKey(attributes);
            
            // Build context from input variables
            var context = BuildContextString(config.InputVariables, variables);
            var fullPrompt = BuildGeminiPrompt(config, context);

            // Prepare Gemini API request
            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = fullPrompt } } }
                },
                generationConfig = new
                {
                    temperature = config.Temperature,
                    maxOutputTokens = config.MaxTokens
                }
            };

            var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            
            // Build request URL with API key
            var requestUrl = $"{BaseUrl}{config.Model}:generateContent?key={apiKey}";

            span?.SetAttribute("ai.request_size", requestJson.Length);

            // Execute Gemini API call
            var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"Gemini API error: {response.StatusCode} - {errorContent}";
                _logger.LogError(errorMessage);
                span?.SetStatus(Status.Error.WithDescription(errorMessage));
                throw new ServiceTaskExecutionException(errorMessage);
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseJson, JsonOptions);

            if (geminiResponse?.Candidates?.Any() != true)
            {
                throw new ServiceTaskExecutionException("Gemini API returned no candidates");
            }

            // Process and store results
            ProcessGeminiResponse(geminiResponse, config, variables);

            // Log usage metrics
            if (geminiResponse.UsageMetadata != null)
            {
                span?.SetAttribute("ai.tokens.prompt", geminiResponse.UsageMetadata.PromptTokenCount);
                span?.SetAttribute("ai.tokens.completion", geminiResponse.UsageMetadata.CandidatesTokenCount);
                span?.SetAttribute("ai.tokens.total", geminiResponse.UsageMetadata.TotalTokenCount);
                
                _logger.LogInformation("Gemini task completed. Tokens used: {PromptTokens} prompt, {CandidatesTokens} completion, {TotalTokens} total",
                    geminiResponse.UsageMetadata.PromptTokenCount, geminiResponse.UsageMetadata.CandidatesTokenCount, geminiResponse.UsageMetadata.TotalTokenCount);
            }

            span?.SetStatus(Status.Ok);
        }
        catch (Exception ex) when (ex is not ServiceTaskExecutionException)
        {
            var errorMessage = $"Gemini service task execution failed: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            span?.SetStatus(Status.Error.WithDescription(errorMessage));
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
            Temperature = double.TryParse(attributes.GetValueOrDefault("ai:temperature", "0.7"), out var temp) ? temp : 0.7,
            MaxTokens = int.TryParse(attributes.GetValueOrDefault("ai:maxTokens", "1000"), out var maxTokens) ? maxTokens : 1000,
            ResultVariable = attributes.GetValueOrDefault("ai:resultVariable", "geminiResult"),
            InputVariables = attributes.GetValueOrDefault("ai:inputVariables", "").Split(',', StringSplitOptions.RemoveEmptyEntries),
            IncludeUsage = attributes.GetValueOrDefault("ai:includeUsage", "false").ToLowerInvariant() == "true",
            UseMockMode = attributes.GetValueOrDefault("ai:useMockMode", "false").ToLowerInvariant() == "true"
        };
    }

    private string BuildGeminiPrompt(GeminiConfiguration config, string context)
    {
        var promptBuilder = new StringBuilder();

        // Add task-specific instructions for Gemini
        switch (config.TaskType.ToLowerInvariant())
        {
            case "code":
                promptBuilder.AppendLine("You are an expert programmer. Please provide clean, well-commented code:");
                break;
            case "analysis":
                promptBuilder.AppendLine("Please provide a detailed analysis:");
                break;
            case "multimodal":
                promptBuilder.AppendLine("Please analyze both text and any provided media content:");
                break;
        }

        promptBuilder.AppendLine(config.Prompt);

        if (!string.IsNullOrEmpty(context))
        {
            promptBuilder.AppendLine("\nContext:");
            promptBuilder.AppendLine(context);
        }

        return promptBuilder.ToString();
    }

    private void ProcessGeminiResponse(GeminiResponse response, GeminiConfiguration config, IDictionary<string, object> variables)
    {
        var mainCandidate = response.Candidates.First();
        var content = mainCandidate.Content?.Parts?.FirstOrDefault()?.Text ?? "";

        // Store main result
        variables[config.ResultVariable] = content;

        // Store response metadata
        variables[$"{config.ResultVariable}_finishReason"] = mainCandidate.FinishReason ?? "";

        if (config.IncludeUsage && response.UsageMetadata != null)
        {
            variables[$"{config.ResultVariable}_usage"] = new Dictionary<string, object>
            {
                ["promptTokenCount"] = response.UsageMetadata.PromptTokenCount,
                ["candidatesTokenCount"] = response.UsageMetadata.CandidatesTokenCount,
                ["totalTokenCount"] = response.UsageMetadata.TotalTokenCount
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
                     ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                     ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
                     ?? throw new ServiceTaskExecutionException("Gemini API key not found in attributes or environment variables");
        
        return apiKey;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    #region Configuration and Response Models

    private record GeminiConfiguration
    {
        public string Model { get; init; } = "gemini-pro";
        public string TaskType { get; init; } = "generation";
        public string Prompt { get; init; } = "";
        public double Temperature { get; init; } = 0.7;
        public int MaxTokens { get; init; } = 1000;
        public string ResultVariable { get; init; } = "geminiResult";
        public string[] InputVariables { get; init; } = [];
        public bool IncludeUsage { get; init; } = false;
        public bool UseMockMode { get; init; } = false;
    }

    private record GeminiResponse
    {
        public GeminiCandidate[] Candidates { get; init; } = [];
        public GeminiUsageMetadata? UsageMetadata { get; init; }
    }

    private record GeminiCandidate
    {
        public GeminiContent? Content { get; init; }
        public string? FinishReason { get; init; }
    }

    private record GeminiContent
    {
        public GeminiPart[]? Parts { get; init; }
    }

    private record GeminiPart
    {
        public string? Text { get; init; }
    }

    private record GeminiUsageMetadata
    {
        public int PromptTokenCount { get; init; }
        public int CandidatesTokenCount { get; init; }
        public int TotalTokenCount { get; init; }
    }

    #endregion
}