using System.Net.Http.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpWorkflowTriggerService(IHttpClientFactory httpClientFactory) : IWorkflowTriggerService
{
    public async Task<IReadOnlyList<StudioWorkflowTrigger>> ListAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        return await client.GetFromJsonAsync<List<StudioWorkflowTrigger>>(
            $"/api/triggers?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken) ?? [];
    }

    public async Task<StudioWorkflowTriggerCreated> CreateAsync(string tenantId, string name, string processDefinitionKey, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync("/api/triggers",
            new { tenantId, name, processDefinitionKey }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudioWorkflowTriggerCreated>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned no trigger details.");
    }

    public async Task UpdateAsync(string tenantId, Guid id, string? name, bool? enabled, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PutAsJsonAsync(
            $"/api/triggers/{id}?tenantId={Uri.EscapeDataString(tenantId)}",
            new { name, enabled }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.DeleteAsync(
            $"/api/triggers/{id}?tenantId={Uri.EscapeDataString(tenantId)}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<StudioProcessInstance?> InvokeAsync(Guid id, string secret, IDictionary<string, object?>? variables = null, string? businessKey = null, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/triggers/{id}/invoke")
        {
            Content = JsonContent.Create(new { variables, businessKey })
        };
        request.Headers.Add("X-VertexBPMN-Trigger-Secret", secret);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StudioProcessInstance>(cancellationToken);
    }
}
