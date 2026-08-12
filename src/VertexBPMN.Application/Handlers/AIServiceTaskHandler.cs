using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VertexBPMN.Application.Extensions;
using VertexBPMN.Application.Configuration;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Handlers;

/// <summary>
/// Universeller AI Service Task Handler für BPMN AI-Tasks.
/// Unterstützt verschiedene AI-Provider (OpenAI, Anthropic, Gemini), Context Enrichment und MCP Integration.
/// </summary>
public class AIServiceTaskHandler : IServiceTaskHandler
{
    private readonly ILogger<AIServiceTaskHandler> _logger;
    private readonly HttpClient _httpClient;
    private readonly TracerProvider? _tracerProvider;
    private readonly IAiDecisionService? _aiDecisionService;
    private readonly AiDependencyOptions _aiOptions;

    public AIServiceTaskHandler(
        ILogger<AIServiceTaskHandler> logger,
        HttpClient httpClient,
        TracerProvider? tracerProvider = null,
        IAiDecisionService? aiDecisionService = null,
        DependencyOptions? dependencyOptions = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tracerProvider = tracerProvider;
        _aiDecisionService = aiDecisionService;
        _aiOptions = dependencyOptions?.Ai ?? new AiDependencyOptions();
    }

    /// <summary>
    /// Führt einen AI Service Task asynchron aus.
    /// Unterstützt OpenAI, Anthropic, Gemini und weitere AI-Provider.
    /// </summary>
    public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct = default)
    {
        var tracer = _tracerProvider?.GetTracer("VertexBPMN.AI");
        using var span = tracer?.StartActiveSpan("AIServiceTask.Execute");

        try
        {
            var config = ParseAIConfiguration(attributes);

            span?.SetAttribute("ai.provider", config.Provider);
            span?.SetAttribute("ai.model", config.Model);
            span?.SetAttribute("ai.task_type", config.TaskType);

            _logger.LogInformation("Executing AI service task with provider {Provider}, model {Model}",
                config.Provider, config.Model);

            // Context Enrichment vor AI-Ausführung
            if (config.ContextEnrichment && _aiDecisionService != null)
            {
                await EnrichContextAsync(config, variables, ct);
            }

            // AI Task ausführen basierend auf Provider
            var result = await ExecuteAITaskAsync(config, variables, ct);

            // Ergebnis in Variablen speichern
            variables[config.ResultVariable] = result.Content;

            if (config.IncludeMetadata && result.Metadata != null)
            {
                variables[$"{config.ResultVariable}_metadata"] = result.Metadata;
            }

            // MCP Integration nach AI-Ausführung
            if (config.McpIntegration && _aiDecisionService != null)
            {
                await ExecuteMcpIntegrationAsync(config, variables, result, ct);
            }

            _logger.LogInformation("AI service task completed successfully. Result stored in variable '{ResultVariable}'",
                config.ResultVariable);

            span?.SetStatus(Status.Ok);
        }
        catch (Exception ex)
        {
            var errorMessage = $"AI service task execution failed: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            span?.SetStatus(Status.Error.WithDescription(errorMessage));

            // Fehler in Variablen für Error Handling in BPMN
            variables["aiTask_error"] = errorMessage;
            variables["aiTask_failed"] = true;

            throw new ServiceTaskExecutionException(errorMessage, ex);
        }
    }

    /// <summary>
    /// Parst die AI-Konfiguration aus BPMN-Attributen.
    /// </summary>
    private AITaskConfiguration ParseAIConfiguration(IDictionary<string, string> attributes)
    {
        var provider = attributes.GetValueOrDefault("ai:provider", _aiOptions.DefaultProvider);
        var model = ResolveModel(attributes, provider);
        return new AITaskConfiguration
        {
            Provider = provider,
            Model = model,
            Endpoint = ResolveModelOption(provider, model)?.Endpoint,
            ApiKeyEnvironmentVariable = ResolveModelOption(provider, model)?.ApiKeyEnvironmentVariable,
            TaskType = attributes.GetValueOrDefault("ai:taskType", "analysis"),
            Prompt = attributes.GetValueOrDefault("ai:prompt", ""),
            SystemMessage = attributes.GetValueOrDefault("ai:systemMessage", "You are an AI assistant for business process automation."),
            Temperature = double.TryParse(attributes.GetValueOrDefault("ai:temperature", "0.7"), out var temp) ? temp : 0.7,
            MaxTokens = int.TryParse(attributes.GetValueOrDefault("ai:maxTokens", "1000"), out var maxTokens) ? maxTokens : 1000,
            ResultVariable = attributes.GetValueOrDefault("ai:resultVariable", "aiResult"),
            InputVariables = attributes.GetValueOrDefault("ai:inputVariables", "").Split(',', StringSplitOptions.RemoveEmptyEntries),
            ContextEnrichment = attributes.GetValueOrDefault("ai:contextEnrichment", "false").ToLowerInvariant() == "true",
            McpIntegration = attributes.GetValueOrDefault("ai:mcpIntegration", "false").ToLowerInvariant() == "true",
            McpServerUrl = attributes.GetValueOrDefault("ai:mcpServerUrl", "http://mcp-server:8080/api/mcp"),
            McpMethod = attributes.GetValueOrDefault("ai:mcpMethod", "process_result"),
            IncludeMetadata = attributes.GetValueOrDefault("ai:includeMetadata", "false").ToLowerInvariant() == "true",
            Timeout = int.TryParse(attributes.GetValueOrDefault("ai:timeout", "60"), out var timeout) ? timeout : 60,
            RetryCount = int.TryParse(attributes.GetValueOrDefault("ai:retryCount", "3"), out var retries) ? retries : 3
        };
    }

    private string ResolveModel(IDictionary<string, string> attributes, string provider)
    {
        if (attributes.TryGetValue("ai:model", out var configuredModel) &&
            !string.IsNullOrWhiteSpace(configuredModel))
            return configuredModel;

        if (_aiOptions.Models.TryGetValue(provider, out var model) &&
            model.Enabled && !string.IsNullOrWhiteSpace(model.Model))
            return model.Model;

        return _aiOptions.DefaultModel;
    }

    private AiModelOptions? ResolveModelOption(string provider, string model)
    {
        if (_aiOptions.Models.TryGetValue(provider, out var providerOptions) &&
            providerOptions.Enabled &&
            (string.IsNullOrWhiteSpace(providerOptions.Model) ||
             string.Equals(providerOptions.Model, model, StringComparison.OrdinalIgnoreCase)))
            return providerOptions;

        return _aiOptions.Models.Values.FirstOrDefault(options =>
            options.Enabled &&
            string.Equals(options.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(options.Model, model, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetApiKey(AITaskConfiguration config, params string[] fallbackNames)
    {
        var environmentVariable = string.IsNullOrWhiteSpace(config.ApiKeyEnvironmentVariable)
            ? fallbackNames.FirstOrDefault(name => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            : config.ApiKeyEnvironmentVariable;

        return Environment.GetEnvironmentVariable(environmentVariable ?? string.Empty)
            ?? throw new ServiceTaskExecutionException(
                $"{config.Provider} API key environment variable '{environmentVariable ?? fallbackNames[0]}' not set");
    }

    /// <summary>
    /// Führt Context Enrichment durch externe Datenquellen aus.
    /// </summary>
    private async Task EnrichContextAsync(AITaskConfiguration config, IDictionary<string, object> variables, CancellationToken ct)
    {
        if (_aiDecisionService == null) return;

        try
        {
            _logger.LogDebug("Enriching context for AI task");

            // Beispiel: Customer-Kontext anreichern
            if (variables.TryGetValue("customerId", out var customerIdValue))
            {
                var customerId = customerIdValue?.ToString();
                if (!string.IsNullOrEmpty(customerId))
                {
                    var customerContext = await _aiDecisionService.FetchExternalContextAsync(
                        customerId, "customer-profile", ct);

                    // Kontext zu Variablen hinzufügen
                    foreach (var (key, value) in customerContext)
                    {
                        variables[$"context_{key}"] = value;
                    }

                    _logger.LogDebug("Enriched context with {ContextKeys} customer data points", customerContext.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Context enrichment failed, continuing without enriched context");
        }
    }

    /// <summary>
    /// Führt AI Task basierend auf Provider aus.
    /// </summary>
    private async Task<AITaskResult> ExecuteAITaskAsync(AITaskConfiguration config, IDictionary<string, object> variables, CancellationToken ct)
    {
        return config.Provider.ToLowerInvariant() switch
        {
            "openai" => await ExecuteOpenAIAsync(config, variables, ct),
            "anthropic" => await ExecuteAnthropicAsync(config, variables, ct),
            "gemini" or "google" => await ExecuteGeminiAsync(config, variables, ct),
            "mock" or "test" => ExecuteMockAI(config, variables),
            _ => throw new ServiceTaskExecutionException($"Unsupported AI provider: {config.Provider}")
        };
    }

    /// <summary>
    /// OpenAI GPT Integration.
    /// </summary>
    private async Task<AITaskResult> ExecuteOpenAIAsync(AITaskConfiguration config, IDictionary<string, object> variables, CancellationToken ct)
    {
        var apiKey = GetApiKey(config, "OPENAI_API_KEY");

        var context = BuildContextString(config.InputVariables, variables);
        var userMessage = string.IsNullOrEmpty(context)
            ? config.Prompt
            : $"{config.Prompt}\n\nContext:\n{context}";

        var requestBody = new
        {
            model = config.Model,
            messages = new[]
            {
                new { role = "system", content = config.SystemMessage },
                new { role = "user", content = userMessage }
            },
            temperature = config.Temperature,
            max_tokens = config.MaxTokens
        };

        return await ExecuteHttpAIRequest(config.Endpoint ?? "https://api.openai.com/v1/chat/completions", requestBody,
            apiKey, "Bearer", config, ct);
    }

    /// <summary>
    /// Anthropic Claude Integration.
    /// </summary>
    private async Task<AITaskResult> ExecuteAnthropicAsync(AITaskConfiguration config, IDictionary<string, object> variables, CancellationToken ct)
    {
        var apiKey = GetApiKey(config, "ANTHROPIC_API_KEY");

        var context = BuildContextString(config.InputVariables, variables);
        var userMessage = string.IsNullOrEmpty(context)
            ? config.Prompt
            : $"{config.Prompt}\n\nContext:\n{context}";

        var requestBody = new
        {
            model = config.Model,
            max_tokens = config.MaxTokens,
            temperature = config.Temperature,
            system = config.SystemMessage,
            messages = new[] { new { role = "user", content = userMessage } }
        };

        // Anthropic verwendet x-api-key Header
        return await ExecuteHttpAIRequest(config.Endpoint ?? "https://api.anthropic.com/v1/messages", requestBody,
            apiKey, "x-api-key", config, ct);
    }

    /// <summary>
    /// Google Gemini Integration.
    /// </summary>
    private async Task<AITaskResult> ExecuteGeminiAsync(AITaskConfiguration config, IDictionary<string, object> variables, CancellationToken ct)
    {
        var apiKey = GetApiKey(config, "GEMINI_API_KEY", "GOOGLE_API_KEY");

        var context = BuildContextString(config.InputVariables, variables);
        var fullPrompt = string.IsNullOrEmpty(context)
            ? config.Prompt
            : $"{config.Prompt}\n\nContext:\n{context}";

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

        var endpoint = config.Endpoint ?? "https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={apiKey}";
        var url = endpoint.Replace("{model}", Uri.EscapeDataString(config.Model), StringComparison.OrdinalIgnoreCase)
            .Replace("{apiKey}", Uri.EscapeDataString(apiKey), StringComparison.OrdinalIgnoreCase);

        // Gemini verwendet API-Key in URL
        return await ExecuteHttpAIRequest(url, requestBody, null, null, config, ct);
    }

    /// <summary>
    /// Mock AI für Tests und Development.
    /// </summary>
    private static AITaskResult ExecuteMockAI(AITaskConfiguration config, IDictionary<string, object> variables)
    {
        var context = string.Join(", ", config.InputVariables
            .Where(v => variables.ContainsKey(v.Trim()))
            .Select(v => $"{v}={variables[v.Trim()]}"));

        var result = $"Mock AI ({config.Provider}:{config.Model}) analyzed: {config.Prompt}";
        if (!string.IsNullOrEmpty(context))
        {
            result += $" with context: {context}";
        }

        return new AITaskResult
        {
            Content = result,
            Metadata = new Dictionary<string, object>
            {
                ["provider"] = config.Provider,
                ["model"] = config.Model,
                ["tokens_used"] = 42,
                ["processing_time_ms"] = 150
            }
        };
    }

    /// <summary>
    /// Universelle HTTP AI Request Ausführung.
    /// </summary>
    private async Task<AITaskResult> ExecuteHttpAIRequest(string url, object requestBody, string? apiKey,
        string? authHeaderName, AITaskConfiguration config, CancellationToken ct)
    {
        var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

        // Authentication Header setzen
        if (!string.IsNullOrEmpty(apiKey))
        {
            if (authHeaderName == "Bearer")
            {
                request.Headers.Authorization = new("Bearer", apiKey);
            }
            else if (!string.IsNullOrEmpty(authHeaderName))
            {
                request.Headers.Add(authHeaderName, apiKey);
            }
        }

        // Anthropic spezifische Header
        if (url.Contains("anthropic"))
        {
            request.Headers.Add("anthropic-version", "2023-06-01");
        }

        // Retry-Logic mit Exponential Backoff
        var retryCount = 0;
        while (retryCount <= config.RetryCount)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(config.Timeout));
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

                var response = await _httpClient.SendAsync(request, combinedCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(combinedCts.Token);
                    return ParseAIResponse(responseJson, config.Provider);
                }

                var errorContent = await response.Content.ReadAsStringAsync(combinedCts.Token);

                // Bestimmte Status Codes sind nicht retry-bar
                if (!IsRetryableStatusCode(response.StatusCode))
                {
                    throw new ServiceTaskExecutionException($"AI API error: {response.StatusCode} - {errorContent}");
                }

                throw new HttpRequestException($"AI API error: {response.StatusCode} - {errorContent}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw new ServiceTaskExecutionException($"AI request timed out after {config.Timeout} seconds");
            }
            catch (Exception) when (retryCount < config.RetryCount)
            {
                retryCount++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
                _logger.LogWarning("AI request failed, retrying in {Delay}s (attempt {Retry}/{MaxRetries})",
                    delay.TotalSeconds, retryCount, config.RetryCount);
                await Task.Delay(delay, ct);
            }
        }

        throw new ServiceTaskExecutionException($"AI request failed after {config.RetryCount} retries");
    }

    /// <summary>
    /// Führt MCP Integration nach AI-Ausführung aus.
    /// </summary>
    private async Task ExecuteMcpIntegrationAsync(AITaskConfiguration config, IDictionary<string, object> variables,
        AITaskResult result, CancellationToken ct)
    {
        if (_aiDecisionService == null) return;

        try
        {
            _logger.LogDebug("Executing MCP integration with method {McpMethod}", config.McpMethod);

            var mcpParams = new Dictionary<string, object>
            {
                ["aiResult"] = result.Content,
                ["aiMetadata"] = result.Metadata ?? new Dictionary<string, object>(),
                ["processVariables"] = variables.ToDictionary(kv => kv.Key, kv => kv.Value),
                ["provider"] = config.Provider,
                ["model"] = config.Model
            };

            await _aiDecisionService.ExecuteMcpActionAsync("ai-task", config.McpServerUrl, config.McpMethod, mcpParams, ct);

            _logger.LogDebug("MCP integration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP integration failed, continuing without MCP processing");
        }
    }

    /// <summary>
    /// Parst AI Provider Response zu einheitlichem Format.
    /// </summary>
    private static AITaskResult ParseAIResponse(string responseJson, string provider)
    {
        using var doc = JsonDocument.Parse(responseJson);

        return provider.ToLowerInvariant() switch
        {
            "openai" => new AITaskResult
            {
                Content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "",
                Metadata = ExtractOpenAIMetadata(doc.RootElement)
            },
            "anthropic" => new AITaskResult
            {
                Content = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "",
                Metadata = ExtractAnthropicMetadata(doc.RootElement)
            },
            "gemini" or "google" => new AITaskResult
            {
                Content = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "",
                Metadata = ExtractGeminiMetadata(doc.RootElement)
            },
            _ => new AITaskResult { Content = responseJson }
        };
    }

    private static Dictionary<string, object> ExtractOpenAIMetadata(JsonElement element)
    {
        var metadata = new Dictionary<string, object>();

        if (element.TryGetProperty("usage", out var usage))
        {
            metadata["tokens_prompt"] = usage.GetProperty("prompt_tokens").GetInt32();
            metadata["tokens_completion"] = usage.GetProperty("completion_tokens").GetInt32();
            metadata["tokens_total"] = usage.GetProperty("total_tokens").GetInt32();
        }

        if (element.TryGetProperty("model", out var model))
        {
            metadata["model_used"] = model.GetString() ?? "";
        }

        return metadata;
    }

    private static Dictionary<string, object> ExtractAnthropicMetadata(JsonElement element)
    {
        var metadata = new Dictionary<string, object>();

        if (element.TryGetProperty("usage", out var usage))
        {
            metadata["tokens_input"] = usage.GetProperty("input_tokens").GetInt32();
            metadata["tokens_output"] = usage.GetProperty("output_tokens").GetInt32();
        }

        if (element.TryGetProperty("model", out var model))
        {
            metadata["model_used"] = model.GetString() ?? "";
        }

        return metadata;
    }

    private static Dictionary<string, object> ExtractGeminiMetadata(JsonElement element)
    {
        var metadata = new Dictionary<string, object>();

        if (element.TryGetProperty("usageMetadata", out var usage))
        {
            metadata["tokens_prompt"] = usage.GetProperty("promptTokenCount").GetInt32();
            metadata["tokens_candidates"] = usage.GetProperty("candidatesTokenCount").GetInt32();
            metadata["tokens_total"] = usage.GetProperty("totalTokenCount").GetInt32();
        }

        return metadata;
    }

    private static string BuildContextString(string[] inputVariables, IDictionary<string, object> variables)
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

    private static bool IsRetryableStatusCode(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.RequestTimeout or
                System.Net.HttpStatusCode.TooManyRequests or
                System.Net.HttpStatusCode.InternalServerError or
                System.Net.HttpStatusCode.BadGateway or
                System.Net.HttpStatusCode.ServiceUnavailable or
                System.Net.HttpStatusCode.GatewayTimeout => true,
            _ => false
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    #region Configuration Models

    private record AITaskConfiguration
    {
        public string Provider { get; init; } = "openai";
        public string Model { get; init; } = "gpt-4";
        public string? Endpoint { get; init; }
        public string? ApiKeyEnvironmentVariable { get; init; }
        public string TaskType { get; init; } = "analysis";
        public string Prompt { get; init; } = "";
        public string SystemMessage { get; init; } = "";
        public double Temperature { get; init; } = 0.7;
        public int MaxTokens { get; init; } = 1000;
        public string ResultVariable { get; init; } = "aiResult";
        public string[] InputVariables { get; init; } = [];
        public bool ContextEnrichment { get; init; } = false;
        public bool McpIntegration { get; init; } = false;
        public string McpServerUrl { get; init; } = "";
        public string McpMethod { get; init; } = "process_result";
        public bool IncludeMetadata { get; init; } = false;
        public int Timeout { get; init; } = 60;
        public int RetryCount { get; init; } = 3;
    }

    private record AITaskResult
    {
        public string Content { get; init; } = "";
        public Dictionary<string, object>? Metadata { get; init; }
    }

    #endregion
}