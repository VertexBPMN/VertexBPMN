namespace VertexBPMN.Core.Fakes;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

/// <summary>
/// A fake implementation of ISendGridClient for offline and unit testing.
/// </summary>
public class FakeSendGridClient : ISendGridClient
{
    private readonly ILogger<FakeSendGridClient> _logger;

    public FakeSendGridClient(ILogger<FakeSendGridClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string UrlPath { get; set; } = "https://api.sendgrid.com/v3/mail/send";
    public string Version { get; set; } = "v3";
    public string MediaType { get; set; } = "application/json";

    public AuthenticationHeaderValue AddAuthorization(KeyValuePair<string, string> header)
    {
        _logger.LogDebug("Adding fake authorization header for key: {Key}", header.Key);
        return new AuthenticationHeaderValue("Bearer", "fake-token");
    }

    public Task<Response> MakeRequest(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Simulating API request to {UrlPath} with method {Method}.", request.RequestUri, request.Method);
        return Task.FromResult(new Response(System.Net.HttpStatusCode.OK, null, null));
    }

    public Task<Response> RequestAsync(SendGridClient.Method method, string requestBody = null, string queryParams = null, string urlPath = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Simulating API request with method {Method}, body: {RequestBody}, queryParams: {QueryParams}, urlPath: {UrlPath}.",
            method, requestBody, queryParams, urlPath ?? UrlPath);
        return Task.FromResult(new Response(System.Net.HttpStatusCode.OK, null, null));
    }

    public Task<Response> SendEmailAsync(SendGridMessage msg, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Simulating sending email to {To} from {From} with subject '{Subject}'.",
            string.Join(", ", msg.Personalizations[0].Tos), msg.From.Email, msg.Subject);

        // Simulate success response
        return Task.FromResult(new Response(System.Net.HttpStatusCode.Accepted, null, null));
    }
}
