using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Domain;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Core.Engine;

/// <summary>
/// Distributed token execution engine for enterprise scalability
/// Olympic-level feature: Enterprise Scalability - Distributed processing
/// </summary>
public interface IDistributedTokenEngine
{
    Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default);
    Task<bool> CanExecuteAsync(string nodeId, CancellationToken cancellationToken = default);
    Task DistributeTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default);
    Task<List<ExecutionToken>> GetPendingTokensAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Execution token for distributed processing
/// </summary>
public record ExecutionToken(
    Guid Id,
    Guid ProcessInstanceId,
    string CurrentNodeId,
    string NodeType,
    IDictionary<string, object> Variables,
    DateTime CreatedAt,
    string? AssignedWorker = null,
    DateTime? AssignedAt = null,
    int RetryCount = 0
);

/// <summary>
/// Worker node for distributed execution
/// </summary>
public record WorkerNode(
    string Id,
    string HostName,
    int Port,
    DateTime LastHeartbeat,
    List<string> SupportedNodeTypes,
    int CurrentLoad,
    int MaxCapacity
);

/// <summary>
/// In-memory implementation of distributed token engine
/// In production, this would use Redis, RabbitMQ, or Apache Kafka
/// </summary>
public class DistributedTokenEngine : IDistributedTokenEngine
{
    private readonly ConcurrentQueue<ExecutionToken> _tokenQueue = new();
    private readonly ConcurrentDictionary<string, WorkerNode> _workers = new();
    private readonly ConcurrentDictionary<Guid, ExecutionToken> _processingTokens = new();
    private readonly ILogger<DistributedTokenEngine> _logger;
    private readonly Timer _heartbeatTimer;

    public DistributedTokenEngine(ILogger<DistributedTokenEngine> logger)
    {
        _logger = logger;
        
        // Initialize with current node as worker
        var currentWorker = new WorkerNode(
            Environment.MachineName,
            Environment.MachineName,
            5000,
            DateTime.UtcNow,
            new List<string> { "userTask", "serviceTask", "scriptTask", "businessRuleTask" },
            0,
            10
        );
        _workers[currentWorker.Id] = currentWorker;

        // Start heartbeat timer
        _heartbeatTimer = new Timer(ProcessHeartbeats, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    public async Task<List<string>> ExecuteAsync(BpmnModel model, CancellationToken cancellationToken = default)
    {
        var trace = new List<string>();
        var processInstanceId = Guid.NewGuid();
        
        // Find start event
        var startEvent = model.Events.FirstOrDefault(e => e.Type == "startEvent");
        if (startEvent == null)
        {
            trace.Add("No start event found");
            return trace;
        }

        trace.Add($"DistributedExecution: Starting process {processInstanceId}");
        
        // Create initial token
        var initialToken = new ExecutionToken(
            Guid.NewGuid(),
            processInstanceId,
            startEvent.Id,
            "startEvent",
            new Dictionary<string, object>(),
            DateTime.UtcNow
        );

        await DistributeTokenAsync(initialToken, cancellationToken);
        trace.Add($"TokenDistributed: {initialToken.Id} -> {initialToken.CurrentNodeId}");

        // Simulate distributed processing
        await ProcessDistributedTokensAsync(model, trace, cancellationToken);
        
        return trace;
    }

    public async Task<bool> CanExecuteAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        // Check if any worker can handle this node type
        var availableWorker = _workers.Values
            .Where(w => w.CurrentLoad < w.MaxCapacity)
            .Where(w => DateTime.UtcNow - w.LastHeartbeat < TimeSpan.FromMinutes(2))
            .Any();

        return await Task.FromResult(availableWorker);
    }

    public async Task DistributeTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default)
    {
        // Find best worker for this token
        var bestWorker = FindBestWorker(token.NodeType);
        
        if (bestWorker != null)
        {
            var assignedToken = token with 
            { 
                AssignedWorker = bestWorker.Id, 
                AssignedAt = DateTime.UtcNow 
            };
            
            _tokenQueue.Enqueue(assignedToken);
            _logger.LogInformation("Token {TokenId} assigned to worker {WorkerId}", 
                token.Id, bestWorker.Id);
        }
        else
        {
            // No worker available, queue for later
            _tokenQueue.Enqueue(token);
            _logger.LogWarning("No worker available for token {TokenId}, queued for later", token.Id);
        }

        await Task.CompletedTask;
    }

    public async Task<List<ExecutionToken>> GetPendingTokensAsync(CancellationToken cancellationToken = default)
    {
        var pendingTokens = new List<ExecutionToken>();
        
        while (_tokenQueue.TryDequeue(out var token))
        {
            pendingTokens.Add(token);
        }

        return await Task.FromResult(pendingTokens);
    }

    /// <summary>
    /// Register a new worker node
    /// </summary>
    public void RegisterWorker(WorkerNode worker)
    {
        _workers[worker.Id] = worker;
        _logger.LogInformation("Registered worker {WorkerId} with capacity {Capacity}", 
            worker.Id, worker.MaxCapacity);
    }

    /// <summary>
    /// Unregister a worker node
    /// </summary>
    public void UnregisterWorker(string workerId)
    {
        _workers.TryRemove(workerId, out _);
        _logger.LogInformation("Unregistered worker {WorkerId}", workerId);
    }

    /// <summary>
    /// Update worker heartbeat
    /// </summary>
    public void UpdateWorkerHeartbeat(string workerId)
    {
        if (_workers.TryGetValue(workerId, out var worker))
        {
            _workers[workerId] = worker with { LastHeartbeat = DateTime.UtcNow };
        }
    }

    private WorkerNode? FindBestWorker(string nodeType)
    {
        return _workers.Values
            .Where(w => w.SupportedNodeTypes.Contains(nodeType))
            .Where(w => w.CurrentLoad < w.MaxCapacity)
            .Where(w => DateTime.UtcNow - w.LastHeartbeat < TimeSpan.FromMinutes(2))
            .OrderBy(w => w.CurrentLoad)
            .FirstOrDefault();
    }

    private async Task ProcessDistributedTokensAsync(BpmnModel model, List<string> trace, CancellationToken cancellationToken)
    {
        var maxIterations = 50; // Prevent infinite loops
        var iteration = 0;

        while (iteration < maxIterations && !cancellationToken.IsCancellationRequested)
        {
            var pendingTokens = await GetPendingTokensAsync(cancellationToken);
            if (!pendingTokens.Any())
                break;

            foreach (var token in pendingTokens)
            {
                await ProcessTokenAsync(token, model, trace, cancellationToken);
            }

            iteration++;
            await Task.Delay(100, cancellationToken); // Simulate processing time
        }
    }

    private async Task ProcessTokenAsync(ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
    {
        _processingTokens[token.Id] = token;
        
        try
        {
            trace.Add($"ProcessingToken: {token.Id} on {token.AssignedWorker ?? "unassigned"}");
            
            // Find current node
            var currentNode = FindNode(model, token.CurrentNodeId);
            if (currentNode == null)
            {
                trace.Add($"NodeNotFound: {token.CurrentNodeId}");
                return;
            }

            // Process the node
            await ProcessNodeAsync(currentNode, token, model, trace, cancellationToken);
            
            // Update worker load
            if (token.AssignedWorker != null && _workers.TryGetValue(token.AssignedWorker, out var worker))
            {
                _workers[token.AssignedWorker] = worker with { CurrentLoad = Math.Max(0, worker.CurrentLoad - 1) };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing token {TokenId}", token.Id);
            trace.Add($"TokenError: {token.Id} - {ex.Message}");
        }
        finally
        {
            _processingTokens.TryRemove(token.Id, out _);
        }
    }

    private object? FindNode(BpmnModel model, string nodeId)
    {
        return model.Events.FirstOrDefault(e => e.Id == nodeId) as object
            ?? model.Tasks.FirstOrDefault(t => t.Id == nodeId) as object
            ?? model.Gateways.FirstOrDefault(g => g.Id == nodeId) as object
            ?? model.Subprocesses.FirstOrDefault(s => s.Id == nodeId) as object;
    }

    private async Task ProcessNodeAsync(object node, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
    {
        switch (node)
        {
            case BpmnEvent evt when evt.Type == "endEvent":
                trace.Add($"EndEvent: {evt.Id}");
                break;
                
            case BpmnTask task:
                trace.Add($"DistributedTask: {task.Type} {task.Id} on worker {token.AssignedWorker}");
                await Task.Delay(50, cancellationToken); // Simulate task execution
                await ContinueToNextNode(task.Id, token, model, trace, cancellationToken);
                break;
                
            case BpmnGateway gateway:
                trace.Add($"DistributedGateway: {gateway.Type} {gateway.Id}");
                await ProcessGateway(gateway, token, model, trace, cancellationToken);
                break;
                
            case BpmnEvent evt:
                trace.Add($"DistributedEvent: {evt.Type} {evt.Id}");
                await ContinueToNextNode(evt.Id, token, model, trace, cancellationToken);
                break;
        }
    }

    private async Task ProcessGateway(BpmnGateway gateway, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
    {
        var outgoingFlows = model.SequenceFlows.Where(f => f.SourceRef == gateway.Id).ToList();
        
        switch (gateway.Type)
        {
            case "parallelGateway":
                // Create tokens for all outgoing flows
                foreach (var flow in outgoingFlows)
                {
                    var newToken = new ExecutionToken(
                        Guid.NewGuid(),
                        token.ProcessInstanceId,
                        flow.TargetRef,
                        GetNodeType(model, flow.TargetRef),
                        new Dictionary<string, object>(token.Variables),
                        DateTime.UtcNow
                    );
                    await DistributeTokenAsync(newToken, cancellationToken);
                    trace.Add($"ParallelBranch: {flow.TargetRef}");
                }
                break;
                
            case "exclusiveGateway":
                // Select first flow (simplified)
                if (outgoingFlows.Any())
                {
                    var selectedFlow = outgoingFlows[0];
                    var newToken = token with 
                    { 
                        CurrentNodeId = selectedFlow.TargetRef,
                        NodeType = GetNodeType(model, selectedFlow.TargetRef)
                    };
                    await DistributeTokenAsync(newToken, cancellationToken);
                    trace.Add($"ExclusiveBranch: {selectedFlow.TargetRef}");
                }
                break;
        }
    }

    private async Task ContinueToNextNode(string currentNodeId, ExecutionToken token, BpmnModel model, List<string> trace, CancellationToken cancellationToken)
    {
        var outgoingFlows = model.SequenceFlows.Where(f => f.SourceRef == currentNodeId).ToList();
        
        foreach (var flow in outgoingFlows)
        {
            trace.Add($"SequenceFlow: {flow.Id}");
            var newToken = token with 
            { 
                CurrentNodeId = flow.TargetRef,
                NodeType = GetNodeType(model, flow.TargetRef)
            };
            await DistributeTokenAsync(newToken, cancellationToken);
        }
    }

    private string GetNodeType(BpmnModel model, string nodeId)
    {
        if (model.Events.Any(e => e.Id == nodeId))
            return model.Events.First(e => e.Id == nodeId).Type;
        if (model.Tasks.Any(t => t.Id == nodeId))
            return model.Tasks.First(t => t.Id == nodeId).Type;
        if (model.Gateways.Any(g => g.Id == nodeId))
            return model.Gateways.First(g => g.Id == nodeId).Type;
        if (model.Subprocesses.Any(s => s.Id == nodeId))
            return "subprocess";
        return "unknown";
    }

    private void ProcessHeartbeats(object? state)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-2);
        var deadWorkers = _workers.Values
            .Where(w => w.LastHeartbeat < cutoffTime)
            .Select(w => w.Id)
            .ToList();

        foreach (var deadWorkerId in deadWorkers)
        {
            UnregisterWorker(deadWorkerId);
        }
    }

    public void Dispose()
    {
        _heartbeatTimer?.Dispose();
    }
}
