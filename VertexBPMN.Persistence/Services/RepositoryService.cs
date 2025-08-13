using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Services;
using VertexBPMN.Persistence.Repositories;

namespace VertexBPMN.Persistence.Services;

/// <summary>
/// Persistent implementation of IRepositoryService using IProcessDefinitionRepository.
/// </summary>
public class RepositoryService : IRepositoryService
{
    private readonly IProcessDefinitionRepository _repo;
    public RepositoryService(IProcessDefinitionRepository repo) => _repo = repo;

    public async ValueTask<ProcessDefinition> DeployAsync(string bpmnXml, string name, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        // Parse BPMN XML to extract process id
        string processId = name;
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(bpmnXml);
            var ns = doc.Root?.Name.Namespace ?? "";
            var process = doc.Descendants(ns + "process").FirstOrDefault();
            if (process != null)
                processId = (string?)process.Attribute("id") ?? name;
        }
        catch { /* fallback to name if parsing fails */ }

        var def = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Key = processId,
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
