using Microsoft.Extensions.Configuration;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Application;

/// <summary>
/// Persistent implementation of IRepositoryService using IProcessDefinitionRepository.
/// </summary>
public class RepositoryService : IRepositoryService
{
    private readonly IProcessDefinitionRepository _repo;
    private readonly IBpmnParser _parser;
    private readonly bool _scriptsEnabled;

    public RepositoryService(
        IProcessDefinitionRepository repo,
        IBpmnParser parser,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _repo = repo;
        _parser = parser;
        _scriptsEnabled = configuration.GetValue("Runtime:Scripts:Enabled", false);
    }

    public async ValueTask<ProcessDefinition> DeployAsync(string bpmnXml, string name, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bpmnXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        tenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();

        var model = await _parser.ParseAsync(bpmnXml, cancellationToken);
        if (string.IsNullOrWhiteSpace(model.ProcessId))
            throw new InvalidOperationException("The BPMN model does not contain a process id.");

        var errors = model.ValidationDiagnostics?
            .Where(diagnostic => diagnostic.Severity >= ValidationSeverity.Error)
            .Select(diagnostic => diagnostic.Message)
            .ToArray() ?? [];
        if (errors.Length > 0)
            throw new InvalidOperationException($"The BPMN model is not executable: {string.Join("; ", errors)}");

        if (!_scriptsEnabled && model.Tasks.Any(task => task.Type.Equals("scriptTask", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("BPMN script tasks are disabled for the in-process production runtime.");

        var processId = model.ProcessId;
        var latest = await _repo.GetLatestByKeyAsync(processId, tenantId, cancellationToken);
        var deploymentId = Guid.NewGuid();
        var def = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Key = processId,
            Name = name,
            Version = (latest?.Version ?? 0) + 1,
            BpmnXml = bpmnXml,
            TenantId = tenantId,
            TenantScope = string.IsNullOrWhiteSpace(tenantId) ? "$global" : tenantId.Trim(),
            CreatedAt = DateTime.UtcNow,
            DeploymentId = deploymentId,
            Deployment = new EngineDeployment
            {
                Id = deploymentId,
                CreatedAt = DateTime.UtcNow,
                Name = $"Deployment_{name}_{DateTime.UtcNow:yyyyMMddHHmmss}",
                TenantId = tenantId
            }

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
