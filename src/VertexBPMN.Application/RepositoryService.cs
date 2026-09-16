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
    private readonly bool _allowCSharp;
    private readonly bool _externalTasksEnabled;
    private readonly IExternalTaskContractResolver? _externalContracts;

    public RepositoryService(
        IProcessDefinitionRepository repo,
        IBpmnParser parser,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        IExternalTaskContractResolver? externalContracts = null)
    {
        _repo = repo;
        _parser = parser;
        _externalContracts = externalContracts;
        _externalTasksEnabled = configuration.GetValue<bool>("ExternalTasks:Enabled")
            || configuration.GetValue<bool>("ExternalTasks:EnableSchedulingPreview");
        _scriptsEnabled = configuration.GetValue("Runtime:Scripts:Enabled", true);
        // Roslyn C# script execution is NOT sandboxed. Keep it off unless an operator
        // explicitly opts in, so untrusted tenant-deployed BPMN cannot get RCE.
        _allowCSharp = configuration.GetValue("Runtime:Scripts:AllowCSharp", false);
    }

    public async ValueTask<ProcessDefinition> DeployAsync(string bpmnXml, string name, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bpmnXml);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (bpmnXml.Contains(ModelExportRedaction.Marker, StringComparison.Ordinal))
            throw new VertexBPMN.Domain.Exceptions.SecurityException("A redacted export cannot be deployed. Replace inline credentials with credential references first.");
        tenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();

        BpmnModel model;
        try
        {
            model = await _parser.ParseAsync(bpmnXml, cancellationToken);
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("external_task_", StringComparison.Ordinal))
        {
            throw new BpmnDeploymentValidationException([new ValidationDiagnostic(
                Code: "VEN-EXTERNAL-TASK-CONTRACT", Severity: ValidationSeverity.Error,
                Message: error.Message, Category: "Vertex")]);
        }
        if (string.IsNullOrWhiteSpace(model.ProcessId))
            throw new InvalidOperationException("The BPMN model does not contain a process id.");

        var errors = (model.ValidationDiagnostics ?? [])
            .Concat(BpmnDeploymentValidator.Validate(model))
            .Where(diagnostic => diagnostic.Severity >= ValidationSeverity.Error)
            .ToArray();
        if (errors.Length > 0)
            throw new BpmnDeploymentValidationException(errors);

        foreach (var task in model.Tasks.Where(task => task.ExternalTask is not null))
        {
            try
            {
                if (!_externalTasksEnabled || _externalContracts is null)
                    throw new InvalidOperationException("external_task_feature_not_enabled");
                if (tenantId is null)
                    throw new InvalidOperationException("external_task_tenant_required");
                var inputs = (task.Attributes ?? []).Where(item => item.Key.StartsWith("vertex:ioMapping.input.", StringComparison.Ordinal)).ToArray();
                if (inputs.Any(item => string.IsNullOrWhiteSpace(item.Value)
                    || item.Value.Any(character => !(char.IsLetterOrDigit(character) || character == '_'))))
                    throw new InvalidOperationException("external_task_input_expression_unsupported");
                await _externalContracts.ValidateDeploymentAsync(tenantId, task.ExternalTask!,
                    inputs.Select(item => item.Key["vertex:ioMapping.input.".Length..]).ToArray(), cancellationToken);
            }
            catch (InvalidOperationException error)
            {
                throw new BpmnDeploymentValidationException([new ValidationDiagnostic(
                    Code: "VEN-EXTERNAL-TASK-CONTRACT", Severity: ValidationSeverity.Error,
                    Message: error.Message, ElementId: task.Id, Category: "Vertex")]);
            }
        }

        if (!_scriptsEnabled && model.Tasks.Any(task => task.Type.Equals("scriptTask", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("BPMN script tasks are disabled for the in-process production runtime.");

        if (!_allowCSharp && model.Tasks.Any(RequestsCSharpScript))
            throw new InvalidOperationException(
                "BPMN C# script tasks are disabled for the in-process production runtime because Roslyn is not sandboxed. " +
                "Set Runtime:Scripts:AllowCSharp=true only if all tenants are trusted.");

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

    private static bool RequestsCSharpScript(BpmnTask task)
    {
        if (!task.Type.Equals("scriptTask", StringComparison.OrdinalIgnoreCase))
            return false;
        if (task.Attributes is null || !task.Attributes.TryGetValue("scriptFormat", out var format))
            return false;
        return format.Equals("C#", StringComparison.OrdinalIgnoreCase)
            || format.Equals("CSharp", StringComparison.OrdinalIgnoreCase);
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
