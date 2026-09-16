using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;

public sealed class HttpExternalTaskApiClient(
    IHttpClientFactory clients,
    IWorkerAccessTokenProvider tokens,
    IOptions<ExternalTaskWorkerOptions> options) : IExternalTaskApiClient
{
    public async ValueTask<IReadOnlyList<ExternalTaskLease>> ClaimAsync(
        IReadOnlyCollection<string> topics, int maxTasks, int leaseSeconds, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/external-tasks/claim")
        {
            Content = JsonContent.Create(new { topics, maxTasks, leaseSeconds })
        };
        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw Transport(response, "External task claim was rejected.");
        var result = await ReadResponseAsync<ClaimResponse>(response, cancellationToken);
        if (result?.Jobs is null)
            throw new ExternalTaskTransportException("External task claim response is invalid.");
        return result.Jobs;
    }

    public async ValueTask<ExternalTaskWorkerHeartbeatResult> HeartbeatAsync(
        ExternalTaskLease lease, int leaseSeconds, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/external-tasks/{lease.JobId:D}/heartbeat")
        {
            Content = JsonContent.Create(new { leaseId = lease.LeaseId, leaseGeneration = lease.LeaseGeneration, leaseSeconds })
        };
        using var response = await SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var result = await ReadResponseAsync<ExternalTaskHeartbeatResult>(response, cancellationToken);
            if (result is null || result.JobId != lease.JobId || result.LeaseGeneration != lease.LeaseGeneration
                || result.LeaseExpiresAt <= result.ServerTime)
                throw new ExternalTaskTransportException("External task heartbeat response is invalid.");
            return new ExternalTaskWorkerHeartbeatResult(ExternalTaskHeartbeatOutcome.Accepted,
                result.LeaseExpiresAt);
        }
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.Forbidden)
            return new ExternalTaskWorkerHeartbeatResult(ExternalTaskHeartbeatOutcome.LeaseLost);
        throw Transport(response, "External task heartbeat failed.");
    }

    public ValueTask<ExternalTaskWorkerMutationResult> CompleteAsync(
        ExternalTaskLease lease, Guid completionId, JsonElement result, CancellationToken cancellationToken)
        => MutateAsync(lease, "complete", new
        {
            leaseId = lease.LeaseId, leaseGeneration = lease.LeaseGeneration, completionId, result
        }, cancellationToken);

    public ValueTask<ExternalTaskWorkerMutationResult> FailAsync(
        ExternalTaskLease lease, Guid failureId, string kind, string code, CancellationToken cancellationToken)
        => MutateAsync(lease, "fail", new
        {
            leaseId = lease.LeaseId, leaseGeneration = lease.LeaseGeneration, failureId, kind, code
        }, cancellationToken);

    private async ValueTask<ExternalTaskWorkerMutationResult> MutateAsync(
        ExternalTaskLease lease, string operation, object body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"api/external-tasks/{lease.JobId:D}/{operation}") { Content = JsonContent.Create(body) };
        using var response = await SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var result = await ReadResponseAsync<ExternalTaskMutationResult>(response, cancellationToken);
            if (result is null || result.JobId != lease.JobId)
                throw new ExternalTaskTransportException("External task mutation response is invalid.");
            return new ExternalTaskWorkerMutationResult(ExternalTaskMutationOutcome.Accepted);
        }
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.Forbidden)
            return new ExternalTaskWorkerMutationResult(ExternalTaskMutationOutcome.LeaseLost);
        if ((int)response.StatusCode is >= 400 and < 500)
            return new ExternalTaskWorkerMutationResult(ExternalTaskMutationOutcome.Rejected);
        throw Transport(response, "External task mutation failed.");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
                await tokens.GetAccessTokenAsync(cancellationToken));
            var client = clients.CreateClient("VertexBPMN.ExternalTasks");
            client.BaseAddress = new Uri(options.Value.BaseAddress, UriKind.Absolute);
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (ExternalTaskTransportException) { throw; }
        catch (HttpRequestException exception)
        {
            throw new ExternalTaskTransportException("External task API is unavailable.", inner: exception);
        }
    }

    private static ExternalTaskTransportException Transport(HttpResponseMessage response, string message)
        => new(message, response.Headers.RetryAfter?.Delta);

    private static async Task<T?> ReadResponseAsync<T>(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try { return await response.Content.ReadFromJsonAsync<T>(cancellationToken); }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new ExternalTaskTransportException("External task API response is invalid.", inner: exception);
        }
    }

    private sealed record ClaimResponse(IReadOnlyList<ExternalTaskLease> Jobs);
}
