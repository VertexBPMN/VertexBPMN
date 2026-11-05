namespace VertexBPMN.Domain.Model.Runtime;

/// <summary>Separate process-start action; not part of the external-task worker mutation API.</summary>
public sealed record ExternalTaskProcessStartNode(
    string ProcessDefinitionKey, IDictionary<string, object>? Variables, string? BusinessKey,
    string TenantId, string IdempotencyKey)
{
    public const string Route = "api/runtime/start";

    public bool CanExecute(string authenticatedTenant, IReadOnlySet<string> roles)
    {
        return authenticatedTenant == TenantId && roles.Contains("ProcessManager")
            && !string.IsNullOrWhiteSpace(ProcessDefinitionKey) && !string.IsNullOrWhiteSpace(IdempotencyKey);
    }
}
