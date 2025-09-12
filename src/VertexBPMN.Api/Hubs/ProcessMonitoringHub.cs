using Microsoft.AspNetCore.SignalR;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time process monitoring and notifications
/// Olympic-level feature: Enterprise Scalability - Real-time monitoring
/// </summary>
public class ProcessMonitoringHub : Hub
{
    private readonly ILogger<ProcessMonitoringHub> _logger;

    public ProcessMonitoringHub(ILogger<ProcessMonitoringHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Join a process monitoring group for specific process
    /// </summary>
    public async Task JoinProcessGroup(string processInstanceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Process_{processInstanceId}");
        _logger.LogInformation("Client {ConnectionId} joined process group {ProcessInstanceId}", 
            Context.ConnectionId, processInstanceId);
    }

    /// <summary>
    /// Leave a process monitoring group
    /// </summary>
    public async Task LeaveProcessGroup(string processInstanceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Process_{processInstanceId}");
        _logger.LogInformation("Client {ConnectionId} left process group {ProcessInstanceId}", 
            Context.ConnectionId, processInstanceId);
    }

    /// <summary>
    /// Join tenant monitoring group
    /// </summary>
    public async Task JoinTenantGroup(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Tenant_{tenantId}");
        _logger.LogInformation("Client {ConnectionId} joined tenant group {TenantId}", 
            Context.ConnectionId, tenantId);
    }

    /// <summary>
    /// Join workers group for system administrators
    /// </summary>
    public async Task JoinWorkersGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Workers");
        _logger.LogInformation("Client {ConnectionId} joined workers monitoring group", Context.ConnectionId);
    }

    /// <summary>
    /// Client connection established
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to ProcessMonitoringHub", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Client disconnected
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected from ProcessMonitoringHub", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Notify process started
    /// </summary>
    public async Task NotifyProcessStarted(string processInstanceId, string processDefinitionKey, string tenantId)
    {
        var notification = new
        {
            Type = "ProcessStarted",
            ProcessInstanceId = processInstanceId,
            ProcessDefinitionKey = processDefinitionKey,
            TenantId = tenantId,
            Timestamp = DateTime.UtcNow
        };

        await Clients.Group($"Process_{processInstanceId}").SendAsync("ProcessEvent", notification);
        await Clients.Group($"Tenant_{tenantId}").SendAsync("ProcessEvent", notification);
        
        _logger.LogDebug("Notified process started: {ProcessInstanceId}", processInstanceId);
    }

    /// <summary>
    /// Notify task completed
    /// </summary>
    public async Task NotifyTaskCompleted(string processInstanceId, string taskId, string taskDefinitionKey, string tenantId)
    {
        var notification = new
        {
            Type = "TaskCompleted",
            ProcessInstanceId = processInstanceId,
            TaskId = taskId,
            TaskDefinitionKey = taskDefinitionKey,
            TenantId = tenantId,
            Timestamp = DateTime.UtcNow
        };

        await Clients.Group($"Process_{processInstanceId}").SendAsync("ProcessEvent", notification);
        await Clients.Group($"Tenant_{tenantId}").SendAsync("ProcessEvent", notification);
        
        _logger.LogDebug("Notified task completed: {TaskId} in process {ProcessInstanceId}", taskId, processInstanceId);
    }

    /// <summary>
    /// Notify process completed
    /// </summary>
    public async Task NotifyProcessCompleted(string processInstanceId, string processDefinitionKey, string tenantId)
    {
        var notification = new
        {
            Type = "ProcessCompleted",
            ProcessInstanceId = processInstanceId,
            ProcessDefinitionKey = processDefinitionKey,
            TenantId = tenantId,
            Timestamp = DateTime.UtcNow
        };

        await Clients.Group($"Process_{processInstanceId}").SendAsync("ProcessEvent", notification);
        await Clients.Group($"Tenant_{tenantId}").SendAsync("ProcessEvent", notification);
        
        _logger.LogDebug("Notified process completed: {ProcessInstanceId}", processInstanceId);
    }

    /// <summary>
    /// Notify incident occurred
    /// </summary>
    public async Task NotifyIncident(string processInstanceId, string incidentType, string message, string tenantId)
    {
        var notification = new
        {
            Type = "Incident",
            ProcessInstanceId = processInstanceId,
            IncidentType = incidentType,
            Message = message,
            TenantId = tenantId,
            Timestamp = DateTime.UtcNow
        };

        await Clients.Group($"Process_{processInstanceId}").SendAsync("ProcessEvent", notification);
        await Clients.Group($"Tenant_{tenantId}").SendAsync("ProcessEvent", notification);
        await Clients.All.SendAsync("SystemAlert", notification); // System-wide alert
        
        _logger.LogWarning("Notified incident in process {ProcessInstanceId}: {IncidentType} - {Message}", 
            processInstanceId, incidentType, message);
    }

    /// <summary>
    /// Broadcast system message to all connected clients
    /// </summary>
    public async Task BroadcastSystemMessage(string message, string severity = "Info")
    {
        var notification = new
        {
            Type = "SystemMessage",
            Message = message,
            Severity = severity,
            Timestamp = DateTime.UtcNow
        };

        await Clients.All.SendAsync("SystemMessage", notification);
        _logger.LogInformation("Broadcasted system message: {Message} (Severity: {Severity})", message, severity);
    }

    /// <summary>
    /// Send performance metrics update
    /// </summary>
    public async Task SendPerformanceUpdate(object metrics)
    {
        await Clients.All.SendAsync("PerformanceUpdate", new
        {
            Type = "PerformanceMetrics",
            Data = metrics,
            Timestamp = DateTime.UtcNow
        });
    }
}
