using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Core.Contracts;
using VertexBPMN.Domain;
using VertexBPMN.Domain.Modeling;
using VertexBPMN.Persistence;

namespace VertexBPMN.EngineServices;

public sealed class ProductionProcessInstanceStore : IProcessInstanceStore
{
    private readonly BpmnDbContext _db;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ProductionProcessInstanceStore(BpmnDbContext db) => _db = db;

    // BPMN
    public async Task SaveBpmnModelAsync(string processId, string bpmnXml)
    {
        var existing = await _db.ProcessDefinitions.FirstOrDefaultAsync(p => p.Key == processId);
        if (existing == null)
        {
            _db.ProcessDefinitions.Add(new ProcessDefinition
            {
                Id = Guid.NewGuid(),
                Key = processId,
                Name = processId,
                Version = 1,
                BpmnXml = bpmnXml,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.BpmnXml = bpmnXml;
            existing.Version += 1;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<string> GetBpmnModelAsync(string processId)
        => (await _db.ProcessDefinitions.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Key == processId))
            ?.BpmnXml ?? throw new InvalidOperationException($"Process {processId} not found.");

    public async Task<IEnumerable<string>> ListProcessesAsync()
        => await _db.ProcessDefinitions.AsNoTracking()
            .Select(p => p.Key)
            .Distinct()
            .ToListAsync();

    // Instances
    public async Task SaveInstanceAsync(ProcessInstance instance)
    {
        var tracked = await _db.ProcessInstances.FindAsync(instance.Id);
        if (tracked == null)
            _db.ProcessInstances.Add(instance);
        else
        {
            tracked.State = instance.State;
            tracked.EndedAt = instance.EndedAt;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<ProcessInstance> GetInstanceAsync(string instanceId)
    {
        var guid = Guid.Parse(instanceId);
        return await _db.ProcessInstances.AsNoTracking()
                   .FirstOrDefaultAsync(p => p.Id == guid)
               ?? throw new InvalidOperationException($"Instance {instanceId} not found.");
    }

    // Tokens
    public async Task SaveTokenAsync(ExecutionToken token)
    {
        var existing = await _db.ExecutionTokens.FindAsync(token.Id);
        if (existing == null)
            _db.ExecutionTokens.Add(token);
        else
        {
            existing.State = token.State;
            existing.AssignedWorker = token.AssignedWorker;
            existing.RetryCount = token.RetryCount;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<ExecutionToken> GetTokenAsync(Guid tokenId)
        => await _db.ExecutionTokens.AsNoTracking()
               .FirstOrDefaultAsync(t => t.Id == tokenId)
           ?? throw new InvalidOperationException($"Token {tokenId} not found.");

    public async Task<List<ExecutionToken>> GetPendingTokensAsync()
        => await _db.ExecutionTokens.AsNoTracking()
            .Where(t => t.State == "Pending")
            .OrderBy(t => t.CreatedAt)
            .Take(500)
            .ToListAsync();

    // Worker
    public Task SaveWorkerAsync(WorkerNode worker) => Task.CompletedTask; // TODO: Implement persistence (Upsert)
    public Task<WorkerNode> GetWorkerAsync(string workerId)
    {
        // TODO: Replace with real retrieval from persistence layer once WorkerNodes are stored
        var worker = new WorkerNode(
            workerId,                // Id
            Environment.MachineName, // HostName
            0,                       // Port (unknown/default)
            DateTime.UtcNow,         // LastSeen
            new List<string>(),      // Capabilities
            0,                       // ActiveTasks
            1,                       // MaxTasks
            true,                    // IsActive
            false,                   // IsBusy
            true                     // IsHealthy
        );
        return Task.FromResult(worker);
    }
    public Task<List<WorkerNode>> GetActiveWorkersAsync() => Task.FromResult(new List<WorkerNode>());
    public Task RemoveWorkerAsync(string workerId) => Task.CompletedTask;

    // Dead Letter
    public Task SaveToDeadLetterQueueAsync<T>(T token, string errorMessage)
    {
        // Insert into DeadLetters table
        return Task.CompletedTask;
    }

    // DMN / CMMN / Case – analog (separate Tabellen / Repos)
    public Task SaveDmnModelAsync(string decisionId, string dmnXml) => Task.CompletedTask;
    public Task<string> GetDmnModelAsync(string decisionId, CancellationToken cancellationToken = default) => Task.FromResult("");

    public Task SaveCaseTokenAsync(CaseToken token) => Task.CompletedTask;
    public Task<CaseToken> GetCaseTokenAsync(Guid tokenId) => Task.FromResult<CaseToken>(new CaseToken() { Id = tokenId });
    public Task<List<CaseToken>> GetPendingCaseTokensAsync() => Task.FromResult(new List<CaseToken>());
    public Task SaveCmmnModelAsync(string caseId, string cmmnXml) => Task.CompletedTask;
    public Task<string> GetCmmnModelAsync(string caseId) => Task.FromResult("");

    public Task UpdateCaseModelAsync(CaseModel model) => Task.CompletedTask;
    public Task SaveHistoricalCaseDataAsync(HistoricalCaseData data) => Task.CompletedTask;
    public Task<List<HistoricalCaseData>> GetHistoricalCaseDataAsync(string caseId) => Task.FromResult(new List<HistoricalCaseData>());
}