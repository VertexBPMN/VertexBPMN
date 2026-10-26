using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public interface IApplicationDefinitionService
{
    Task<IReadOnlyList<ApplicationDefinitionSummary>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<ApplicationDefinitionMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default);
    Task<ApplicationRevisionRecord?> GetRevisionAsync(string tenantId, string id, int version, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(string tenantId, string id, ApplicationDefinitionWriteRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
    Task<ApplicationReleaseCandidateRecord> CreateReleaseCandidateAsync(string tenantId, string id, int version, CancellationToken cancellationToken = default);
    Task<ApplicationReleaseCandidateRecord?> GetReleaseCandidateAsync(string tenantId, string id, CancellationToken cancellationToken = default);
    Task<bool> ReleaseAsync(string tenantId, string id, CancellationToken cancellationToken = default);
    Task<ApplicationRevisionRecord?> GetReleasedRevisionAsync(string tenantId, string id, CancellationToken cancellationToken = default);
    Task<ApplicationDefinitionMetadata> CreateAsync(string tenantId, ApplicationDefinitionWriteRequest request, CancellationToken cancellationToken = default);
}
