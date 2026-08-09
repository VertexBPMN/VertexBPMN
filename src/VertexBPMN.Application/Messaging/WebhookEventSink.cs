using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Messaging;

public sealed class WebhookEventSink : IProcessMiningEventSink
{
    private readonly IProcessMiningEventSink _inner;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookEventSink> _logger;

    public WebhookEventSink(
        IProcessMiningEventSink inner,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<WebhookEventSink> logger)
    {
        _inner = inner;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async ValueTask<ProcessMiningEvent> EmitAsync(
        ProcessMiningEvent evt,
        CancellationToken cancellationToken = default)
    {
        var persisted = await _inner.EmitAsync(evt, cancellationToken).ConfigureAwait(false);
        var endpoints = _configuration.GetSection("Webhooks:Endpoints").Get<WebhookEndpoint[]>() ?? Array.Empty<WebhookEndpoint>();
        var matchingEndpoints = endpoints.Where(endpoint =>
            Uri.TryCreate(endpoint.Url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && (endpoint.Events.Length == 0 || endpoint.Events.Contains(evt.EventType, StringComparer.OrdinalIgnoreCase)));

        foreach (var endpoint in matchingEndpoints)
            await DeliverAsync(endpoint, evt, cancellationToken).ConfigureAwait(false);

        return persisted;
    }

    private async Task DeliverAsync(WebhookEndpoint endpoint, ProcessMiningEvent evt, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = evt.Id,
            eventType = evt.EventType,
            processInstanceId = evt.ProcessInstanceId,
            timestamp = evt.Timestamp,
            tenantId = evt.TenantId,
            taskId = evt.TaskId,
            activityId = evt.ActivityId,
            userId = evt.UserId,
            payload = evt.PayloadJson
        });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-VertexBPMN-Event", evt.EventType);

            var signingSecret = _configuration["Webhooks:SigningSecret"];
            if (!string.IsNullOrWhiteSpace(signingSecret))
            {
                var signature = HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(signingSecret),
                    Encoding.UTF8.GetBytes(payload));
                request.Headers.TryAddWithoutValidation("X-VertexBPMN-Signature", $"sha256={Convert.ToHexString(signature).ToLowerInvariant()}");
            }

            var response = await _httpClientFactory.CreateClient("webhooks").SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Webhook delivery to {WebhookUrl} returned {StatusCode} for event {EventType}", endpoint.Url, response.StatusCode, evt.EventType);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook delivery to {WebhookUrl} failed for event {EventType}", endpoint.Url, evt.EventType);
        }
    }

    public sealed class WebhookEndpoint
    {
        public string Url { get; set; } = string.Empty;
        public string[] Events { get; set; } = Array.Empty<string>();
    }
}
