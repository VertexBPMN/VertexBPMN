using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VertexBPMN.Api.Security;
using VertexBPMN.Domain.Interfaces;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time process monitoring and notifications
/// Olympic-level feature: Enterprise Scalability - Real-time monitoring
/// </summary>
[Authorize(Policy = "TenantReadOnly")]
public class ProcessMonitoringHub : Hub
{
    private readonly ILogger<ProcessMonitoringHub> _logger;
    private readonly IRuntimeService _runtime;

    public ProcessMonitoringHub(ILogger<ProcessMonitoringHub> logger, IRuntimeService runtime)
    {
        _logger = logger;
        _runtime = runtime;
    }

    /// <summary>
    /// Join a process monitoring group for specific process
    /// </summary>
    public async Task JoinProcessGroup(string processInstanceId)
    {
        var id = await HubTenantAccess.RequireProcessAsync(Context, _runtime, processInstanceId);
        processInstanceId = id.ToString();
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
        HubTenantAccess.RequireTenant(Context, tenantId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Tenant_{tenantId}");
        _logger.LogInformation("Client {ConnectionId} joined tenant group {TenantId}",
            Context.ConnectionId, tenantId);
    }

    /// <summary>
    /// Join workers group for system administrators
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    public async Task JoinWorkersGroup()
    {
        if (Context.User?.IsInRole("Admin") != true) throw new HubException("Access denied.");
        await Groups.AddToGroupAsync(Context.ConnectionId, "Workers");
        _logger.LogInformation("Client {ConnectionId} joined workers monitoring group", Context.ConnectionId);
    }

    /// <summary>
    /// Join user-specific channel for notifications
    /// </summary>
    public async Task JoinUserChannel(string userId)
    {
        var own = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub") ?? Context.User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(own) || !string.Equals(own, userId, StringComparison.Ordinal))
            throw new HubException("Access denied.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        _logger.LogInformation("Connection {ConnectionId} joined user notification groups for {UserId}", Context.ConnectionId, userId);
    }

    /// <summary>
    /// Leave user-specific channel
    /// </summary>
    public async Task LeaveUserChannel(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
        _logger.LogInformation("Connection {ConnectionId} left user notification groups for {UserId}", Context.ConnectionId, userId);
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
    [Authorize(Policy = "AdminOnly")]
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
    [Authorize(Policy = "AdminOnly")]
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
    [Authorize(Policy = "AdminOnly")]
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
    [Authorize(Policy = "AdminOnly")]
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
        await Clients.Group("Workers").SendAsync("SystemAlert", notification);

        _logger.LogWarning("Notified incident in process {ProcessInstanceId}: {IncidentType} - {Message}",
            processInstanceId, incidentType, message);
    }

    /// <summary>
    /// Broadcast system message to all connected clients
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
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
    [Authorize(Policy = "AdminOnly")]
    public async Task SendPerformanceUpdate(object metrics)
    {
        await Clients.Group("Workers").SendAsync("PerformanceUpdate", new
        {
            Type = "PerformanceMetrics",
            Data = metrics,
            Timestamp = DateTime.UtcNow
        });
    }
}
