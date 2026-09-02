namespace VertexBPMN.Domain.Interfaces;

public interface IFormDefinitionService
{
    Task<IReadOnlyList<FormDefinitionMetadata>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<FormDefinitionMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default);
    Task<FormDefinitionMetadata> CreateAsync(string tenantId, FormDefinitionWriteRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(string tenantId, string id, FormDefinitionWriteRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}

public sealed record FormDefinitionMetadata(string Id, string TenantId, string Key, string Name, string Schema, int Version, DateTime CreatedAt, DateTime LastModified);
public sealed record FormDefinitionWriteRequest(string Key, string Name, string Schema);
public sealed class FormDefinitionConflictException(string message) : Exception(message);
