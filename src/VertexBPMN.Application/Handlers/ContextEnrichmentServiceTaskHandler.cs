using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Application.Extensions;

namespace VertexBPMN.Application.Handlers;

/// <summary>
/// Context enrichment service task handler that fetches and integrates external data sources
/// to enhance process variables with additional context information from APIs, databases, or services.
/// </summary>
public class ContextEnrichmentServiceTaskHandler : IServiceTaskHandler
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ContextEnrichmentServiceTaskHandler> _logger;
    private readonly Tracer _tracer;

    public ContextEnrichmentServiceTaskHandler(HttpClient httpClient, ILogger<ContextEnrichmentServiceTaskHandler> logger, TracerProvider tracerProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracer = tracerProvider.GetTracer("VertexBPMN.ContextEnrichment");
    }

    /// <summary>
    /// Executes context enrichment by fetching data from external sources and merging it into process variables.
    /// </summary>
    public async Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.StartActiveSpan("ContextEnrichment.ExecuteTask");
        
        try
        {
            var config = ParseConfiguration(attributes);
            span.SetAttribute("context.source_type", config.SourceType);
            span.SetAttribute("context.enrichment_type", config.EnrichmentType);

            _logger.LogInformation("Executing context enrichment from {SourceType} with type {EnrichmentType}", 
                config.SourceType, config.EnrichmentType);

            switch (config.SourceType.ToLowerInvariant())
            {
                case "api":
                case "rest":
                    await EnrichFromRestApiAsync(config, variables, span, cancellationToken);
                    break;
                case "database":
                case "db":
                    await EnrichFromDatabaseAsync(config, variables, span, cancellationToken);
                    break;
                case "file":
                case "filesystem":
                    await EnrichFromFileSystemAsync(config, variables, span, cancellationToken);
                    break;
                case "memory":
                case "cache":
                    await EnrichFromMemoryAsync(config, variables, span, cancellationToken);
                    break;
                case "composite":
                    await EnrichFromMultipleSourcesAsync(config, variables, span, cancellationToken);
                    break;
                default:
                    throw new ServiceTaskExecutionException($"Unsupported source type: {config.SourceType}");
            }

            span.SetStatus(Status.Ok);
            _logger.LogInformation("Context enrichment completed successfully");
        }
        catch (Exception ex) when (ex is not ServiceTaskExecutionException)
        {
            var errorMessage = $"Context enrichment execution failed: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            span.SetStatus(Status.Error.WithDescription(errorMessage));
            throw new ServiceTaskExecutionException(errorMessage, ex);
        }
    }

    private ContextEnrichmentConfiguration ParseConfiguration(IDictionary<string, string> attributes)
    {
        return new ContextEnrichmentConfiguration
        {
            SourceType = attributes.GetValueOrDefault("context:sourceType", "api"),
            EnrichmentType = attributes.GetValueOrDefault("context:type", "general"),
            SourceUrl = attributes.GetValueOrDefault("context:sourceUrl", ""),
            SourceQuery = attributes.GetValueOrDefault("context:query", ""),
            HttpMethod = attributes.GetValueOrDefault("context:httpMethod", "GET"),
            Headers = ParseHeaders(attributes.GetValueOrDefault("context:headers", "")),
            Authentication = ParseAuthentication(attributes),
            Timeout = int.TryParse(attributes.GetValueOrDefault("context:timeout", "30"), out var timeout) ? timeout : 30,
            ResultVariable = attributes.GetValueOrDefault("context:resultVariable", "enrichedContext"),
            MergeStrategy = attributes.GetValueOrDefault("context:mergeStrategy", "replace"),
            FilterExpression = attributes.GetValueOrDefault("context:filter", ""),
            TransformExpression = attributes.GetValueOrDefault("context:transform", ""),
            InputVariables = attributes.GetValueOrDefault("context:inputVariables", "").Split(',', StringSplitOptions.RemoveEmptyEntries),
            CacheKey = attributes.GetValueOrDefault("context:cacheKey", ""),
            CacheTtl = int.TryParse(attributes.GetValueOrDefault("context:cacheTtl", "0"), out var ttl) ? ttl : 0,
            RetryCount = int.TryParse(attributes.GetValueOrDefault("context:retryCount", "3"), out var retries) ? retries : 3,
            FailOnError = attributes.GetValueOrDefault("context:failOnError", "true").ToLowerInvariant() == "true"
        };
    }

    private AuthenticationConfig? ParseAuthentication(IDictionary<string, string> attributes)
    {
        var authType = attributes.GetValueOrDefault("context:authType", "");
        if (string.IsNullOrEmpty(authType)) return null;

        return authType.ToLowerInvariant() switch
        {
            "bearer" => new AuthenticationConfig 
            { 
                Type = "Bearer", 
                Token = attributes.GetValueOrDefault("context:authToken", "") 
            },
            "basic" => new AuthenticationConfig 
            { 
                Type = "Basic", 
                Username = attributes.GetValueOrDefault("context:authUsername", ""),
                Password = attributes.GetValueOrDefault("context:authPassword", "") 
            },
            "apikey" => new AuthenticationConfig 
            { 
                Type = "ApiKey", 
                ApiKey = attributes.GetValueOrDefault("context:authApiKey", ""),
                ApiKeyHeader = attributes.GetValueOrDefault("context:authApiKeyHeader", "X-API-Key")
            },
            _ => null
        };
    }

    private async Task EnrichFromRestApiAsync(ContextEnrichmentConfiguration config, IDictionary<string, object> variables, 
        TelemetrySpan span, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(config.SourceUrl))
            throw new ServiceTaskExecutionException("Source URL is required for REST API enrichment");

        span.SetAttribute("context.api.url", config.SourceUrl);
        span.SetAttribute("context.api.method", config.HttpMethod);

        var url = BuildUrlWithParameters(config.SourceUrl, config.InputVariables, variables);
        
        using var request = new HttpRequestMessage(new HttpMethod(config.HttpMethod), url);
        
        // Add headers
        foreach (var (key, value) in config.Headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        // Add authentication
        if (config.Authentication != null)
        {
            ApplyAuthentication(request, config.Authentication);
        }

        // Add request body for POST/PUT
        if ((config.HttpMethod.ToUpperInvariant() == "POST" || config.HttpMethod.ToUpperInvariant() == "PUT") 
            && !string.IsNullOrEmpty(config.SourceQuery))
        {
            var requestBody = BuildRequestBody(config.SourceQuery, variables);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        }

        // Configure timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(config.Timeout));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

        var retryCount = 0;
        HttpResponseMessage? response = null;

        while (retryCount <= config.RetryCount)
        {
            try
            {
                response = await _httpClient.SendAsync(request, combinedCts.Token);
                
                if (response.IsSuccessStatusCode)
                    break;
                    
                if (!IsRetryableStatusCode(response.StatusCode))
                    break;
                    
                retryCount++;
                if (retryCount <= config.RetryCount)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), combinedCts.Token);
                }
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                throw new ServiceTaskExecutionException($"Context enrichment API call timed out after {config.Timeout} seconds");
            }
            catch (HttpRequestException)
            {
                retryCount++;
                if (retryCount <= config.RetryCount)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), combinedCts.Token);
                }
                else
                {
                    throw;
                }
            }
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            var errorMessage = $"Context enrichment API call failed: {response?.StatusCode} - {response?.ReasonPhrase}";
            if (config.FailOnError)
            {
                throw new ServiceTaskExecutionException(errorMessage);
            }
            else
            {
                _logger.LogWarning(errorMessage);
                variables[config.ResultVariable] = new Dictionary<string, object> { ["error"] = errorMessage };
                return;
            }
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        span.SetAttribute("context.api.response_size", responseContent.Length);

        // Process response
        var enrichedData = ProcessApiResponse(responseContent, config);
        MergeEnrichedData(enrichedData, config, variables);

        _logger.LogDebug("Successfully enriched context from REST API: {Url}", url);
    }

    private async Task EnrichFromDatabaseAsync(ContextEnrichmentConfiguration config, IDictionary<string, object> variables,
        TelemetrySpan span, CancellationToken cancellationToken)
    {
        // Placeholder for database enrichment
        // In a real implementation, this would use Entity Framework or direct SQL
        span.SetAttribute("context.db.query", config.SourceQuery);
        
        var mockDatabaseData = new Dictionary<string, object>
        {
            ["customerInfo"] = new Dictionary<string, object>
            {
                ["tier"] = "Gold",
                ["creditScore"] = 750,
                ["accountType"] = "Premium"
            },
            ["enrichmentSource"] = "database",
            ["enrichedAt"] = DateTime.UtcNow
        };

        MergeEnrichedData(mockDatabaseData, config, variables);
        await Task.CompletedTask;
    }

    private async Task EnrichFromFileSystemAsync(ContextEnrichmentConfiguration config, IDictionary<string, object> variables,
        TelemetrySpan span, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(config.SourceUrl))
            throw new ServiceTaskExecutionException("File path is required for filesystem enrichment");

        span.SetAttribute("context.file.path", config.SourceUrl);

        try
        {
            if (File.Exists(config.SourceUrl))
            {
                var fileContent = await File.ReadAllTextAsync(config.SourceUrl, cancellationToken);
                
                Dictionary<string, object> enrichedData;
                if (config.SourceUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    enrichedData = JsonSerializer.Deserialize<Dictionary<string, object>>(fileContent, JsonOptions) 
                                   ?? new Dictionary<string, object>();
                }
                else
                {
                    enrichedData = new Dictionary<string, object> { ["fileContent"] = fileContent };
                }

                enrichedData["enrichmentSource"] = "filesystem";
                enrichedData["filePath"] = config.SourceUrl;
                enrichedData["enrichedAt"] = DateTime.UtcNow;

                MergeEnrichedData(enrichedData, config, variables);
            }
            else if (config.FailOnError)
            {
                throw new ServiceTaskExecutionException($"File not found: {config.SourceUrl}");
            }
            else
            {
                _logger.LogWarning("File not found: {FilePath}", config.SourceUrl);
                variables[config.ResultVariable] = new Dictionary<string, object> { ["error"] = "File not found" };
            }
        }
        catch (Exception ex) when (!(ex is ServiceTaskExecutionException))
        {
            if (config.FailOnError)
            {
                throw new ServiceTaskExecutionException($"Failed to read file {config.SourceUrl}: {ex.Message}", ex);
            }
            else
            {
                _logger.LogWarning(ex, "Failed to read file: {FilePath}", config.SourceUrl);
                variables[config.ResultVariable] = new Dictionary<string, object> { ["error"] = ex.Message };
            }
        }
    }

    private async Task EnrichFromMemoryAsync(ContextEnrichmentConfiguration config, IDictionary<string, object> variables,
        TelemetrySpan span, CancellationToken cancellationToken)
    {
        // Placeholder for in-memory cache enrichment
        span.SetAttribute("context.cache.key", config.CacheKey);
        
        var mockCacheData = new Dictionary<string, object>
        {
            ["processHistory"] = new List<object>
            {
                new { processId = "proc-001", completedAt = DateTime.UtcNow.AddDays(-1), outcome = "success" },
                new { processId = "proc-002", completedAt = DateTime.UtcNow.AddDays(-2), outcome = "success" }
            },
            ["enrichmentSource"] = "memory",
            ["cacheKey"] = config.CacheKey,
            ["enrichedAt"] = DateTime.UtcNow
        };

        MergeEnrichedData(mockCacheData, config, variables);
        await Task.CompletedTask;
    }

    private async Task EnrichFromMultipleSourcesAsync(ContextEnrichmentConfiguration config, IDictionary<string, object> variables,
        TelemetrySpan span, CancellationToken cancellationToken)
    {
        span.SetAttribute("context.composite.sources", "api,database,memory");
        
        var compositeData = new Dictionary<string, object>();

        // Combine data from multiple sources
        try
        {
            // API data
            if (!string.IsNullOrEmpty(config.SourceUrl))
            {
                var apiConfig = config with { SourceType = "api" };
                await EnrichFromRestApiAsync(apiConfig, compositeData, span, cancellationToken);
            }

            // Database data (mock)
            var dbData = new Dictionary<string, object>
            {
                ["dbCustomerData"] = new { id = 123, segment = "Premium" }
            };
            foreach (var (key, value) in dbData)
            {
                compositeData[key] = value;
            }

            // Memory data (mock)
            var memoryData = new Dictionary<string, object>
            {
                ["recentActivity"] = new { lastLogin = DateTime.UtcNow.AddHours(-2), activityScore = 85 }
            };
            foreach (var (key, value) in memoryData)
            {
                compositeData[key] = value;
            }

            compositeData["enrichmentSource"] = "composite";
            compositeData["enrichedAt"] = DateTime.UtcNow;

            MergeEnrichedData(compositeData, config, variables);
        }
        catch (Exception ex)
        {
            if (config.FailOnError)
            {
                throw;
            }
            else
            {
                _logger.LogWarning(ex, "Partial failure in composite enrichment");
                variables[config.ResultVariable] = compositeData;
            }
        }
    }

    private Dictionary<string, object> ProcessApiResponse(string responseContent, ContextEnrichmentConfiguration config)
    {
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent, JsonOptions) 
                       ?? new Dictionary<string, object>();

            // Apply filter expression if provided
            if (!string.IsNullOrEmpty(config.FilterExpression))
            {
                data = ApplyJsonPathFilter(data, config.FilterExpression);
            }

            // Apply transform expression if provided
            if (!string.IsNullOrEmpty(config.TransformExpression))
            {
                data = ApplyTransformation(data, config.TransformExpression);
            }

            return data;
        }
        catch (JsonException)
        {
            // If not JSON, store as text
            return new Dictionary<string, object> { ["rawResponse"] = responseContent };
        }
    }

    private void MergeEnrichedData(Dictionary<string, object> enrichedData, ContextEnrichmentConfiguration config, 
        IDictionary<string, object> variables)
    {
        switch (config.MergeStrategy.ToLowerInvariant())
        {
            case "replace":
                variables[config.ResultVariable] = enrichedData;
                break;
            case "merge":
                if (variables.TryGetValue(config.ResultVariable, out var existingValue) && 
                    existingValue is Dictionary<string, object> existingDict)
                {
                    foreach (var (key, value) in enrichedData)
                    {
                        existingDict[key] = value;
                    }
                    variables[config.ResultVariable] = existingDict;
                }
                else
                {
                    variables[config.ResultVariable] = enrichedData;
                }
                break;
            case "flatten":
                foreach (var (key, value) in enrichedData)
                {
                    variables[$"context_{key}"] = value;
                }
                break;
            default:
                variables[config.ResultVariable] = enrichedData;
                break;
        }
    }

    private Dictionary<string, string> ParseHeaders(string headersString)
    {
        var headers = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(headersString)) return headers;

        var headerPairs = headersString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in headerPairs)
        {
            var keyValue = pair.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
            if (keyValue.Length == 2)
            {
                headers[keyValue[0].Trim()] = keyValue[1].Trim();
            }
        }

        return headers;
    }

    private void ApplyAuthentication(HttpRequestMessage request, AuthenticationConfig auth)
    {
        switch (auth.Type)
        {
            case "Bearer":
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
                break;
            case "Basic":
                var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.Username}:{auth.Password}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuth);
                break;
            case "ApiKey":
                request.Headers.TryAddWithoutValidation(auth.ApiKeyHeader, auth.ApiKey);
                break;
        }
    }

    private string BuildUrlWithParameters(string baseUrl, string[] inputVariables, IDictionary<string, object> variables)
    {
        if (inputVariables.Length == 0) return baseUrl;

        var uriBuilder = new UriBuilder(baseUrl);
        var queryBuilder = new StringBuilder(uriBuilder.Query.TrimStart('?'));

        foreach (var varName in inputVariables)
        {
            if (variables.TryGetValue(varName.Trim(), out var value))
            {
                if (queryBuilder.Length > 0) queryBuilder.Append('&');
                queryBuilder.Append($"{varName}={Uri.EscapeDataString(value?.ToString() ?? "")}");
            }
        }

        uriBuilder.Query = queryBuilder.ToString();
        return uriBuilder.ToString();
    }

    private string BuildRequestBody(string template, IDictionary<string, object> variables)
    {
        var body = template;
        foreach (var (key, value) in variables)
        {
            body = body.Replace($"{{{key}}}", value?.ToString() ?? "");
        }
        return body;
    }

    private Dictionary<string, object> ApplyJsonPathFilter(Dictionary<string, object> data, string filterExpression)
    {
        // Simplified JSONPath-like filtering
        // In a real implementation, use a proper JSONPath library like JsonPath.Net
        return data;
    }

    private Dictionary<string, object> ApplyTransformation(Dictionary<string, object> data, string transformExpression)
    {
        // Simplified transformation logic
        // In a real implementation, use a transformation engine like Jint or similar
        return data;
    }

    private bool IsRetryableStatusCode(System.Net.HttpStatusCode statusCode)
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

    private record ContextEnrichmentConfiguration
    {
        public string SourceType { get; init; } = "api";
        public string EnrichmentType { get; init; } = "general";
        public string SourceUrl { get; init; } = "";
        public string SourceQuery { get; init; } = "";
        public string HttpMethod { get; init; } = "GET";
        public Dictionary<string, string> Headers { get; init; } = new();
        public AuthenticationConfig? Authentication { get; init; }
        public int Timeout { get; init; } = 30;
        public string ResultVariable { get; init; } = "enrichedContext";
        public string MergeStrategy { get; init; } = "replace";
        public string FilterExpression { get; init; } = "";
        public string TransformExpression { get; init; } = "";
        public string[] InputVariables { get; init; } = [];
        public string CacheKey { get; init; } = "";
        public int CacheTtl { get; init; } = 0;
        public int RetryCount { get; init; } = 3;
        public bool FailOnError { get; init; } = true;
    }

    private record AuthenticationConfig
    {
        public string Type { get; init; } = "";
        public string Token { get; init; } = "";
        public string Username { get; init; } = "";
        public string Password { get; init; } = "";
        public string ApiKey { get; init; } = "";
        public string ApiKeyHeader { get; init; } = "X-API-Key";
    }

    #endregion
}