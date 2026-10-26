using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Domain.Model.Runtime;

/// <summary>Exportable configuration contains credential references, never bearer tokens or secrets.</summary>
public sealed record ExternalTaskNodeConfiguration(
    Uri EngineAddress, Uri TokenEndpoint, string ClientId, string CredentialReference,
    string TenantId, IReadOnlyCollection<string> Topics, IReadOnlyCollection<string> Profiles)
{
    public ExternalTaskNodeSession Load(ExternalTaskLease lease, ExternalTaskStatus status)
    {
        if (!Topics.Contains(lease.Topic, StringComparer.Ordinal) || status.JobId != lease.JobId
            || status.ActivityExecutionId != lease.ActivityExecutionId || status.State != "Leased"
            || status.LeaseGeneration != lease.LeaseGeneration || status.Topic != lease.Topic
            || status.ContractVersion != lease.ContractVersion
            || status.ApplicationReleaseId != lease.ApplicationReleaseId || status.FlowRevision != lease.FlowRevision)
        {
            throw new InvalidOperationException("lease_lost");
        }

        return new ExternalTaskNodeSession(lease);
    }

    public ExternalTaskClaimCommand Claim(ExternalTaskWorkerContext worker)
    {
        if (!CredentialReference.StartsWith("credential:", StringComparison.Ordinal)
            || CredentialReference.Length <= "credential:".Length
            || CredentialReference.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException("credential_reference_required");
        }

        if (Topics.Any(value => string.IsNullOrWhiteSpace(value) || value == "*" || value.Contains(',', StringComparison.Ordinal))
            || worker.Topics.Any(value => string.IsNullOrWhiteSpace(value) || value == "*" || value.Contains(',', StringComparison.Ordinal))
            || Profiles.Any(value => string.IsNullOrWhiteSpace(value) || value == "*" || value.Contains(',', StringComparison.Ordinal))
            || worker.Profiles.Any(value => string.IsNullOrWhiteSpace(value) || value == "*" || value.Contains(',', StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("worker_forbidden");
        }

        if (worker.TenantId != TenantId || Topics.Any(topic => !worker.Topics.Contains(topic))
            || Profiles.Any(profile => !worker.Profiles.Contains(profile)))
        {
            throw new InvalidOperationException("worker_forbidden");
        }

        return new ExternalTaskClaimCommand(Topics);
    }
}
