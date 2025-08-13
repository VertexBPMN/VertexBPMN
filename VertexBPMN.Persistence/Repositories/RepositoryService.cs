using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Persistence.Repositories;

/// <summary>
/// Persistent implementation of IRepositoryService using IProcessDefinitionRepository.
/// </summary>
public class RepositoryService : IRepositoryService
{
    private readonly IProcessDefinitionRepository _repo;
    public RepositoryService(IProcessDefinitionRepository repo) => _repo = repo;

    public async ValueTask<ProcessDefinition> DeployAsync(string bpmnXml, string name, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        // Versioning logic should be handled in the repository or DB
        var def = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Key = name,
            Name = name,
            Version = 1, // TODO: Implement versioning
            BpmnXml = bpmnXml,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(def, cancellationToken);
        return def;
    }

    public ValueTask<ProcessDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(id, cancellationToken);

    public ValueTask<ProcessDefinition?> GetLatestByKeyAsync(string key, string? tenantId = null, CancellationToken cancellationToken = default)
        => _repo.GetLatestByKeyAsync(key, tenantId, cancellationToken);

    public IAsyncEnumerable<ProcessDefinition> ListAsync(string? key = null, string? tenantId = null, CancellationToken cancellationToken = default)
        => _repo.ListAsync(key, tenantId, cancellationToken);

    public ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => _repo.DeleteAsync(id, cancellationToken);
}
