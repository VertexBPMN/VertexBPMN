using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Persistence.Services;

public class IncidentService : IIncidentService
{
    private readonly List<Incident> _incidents = new(); // Platzhalter, später DB
    private readonly IProcessMiningEventSink _eventSink;

    public IncidentService(IProcessMiningEventSink eventSink)
    {
        _eventSink = eventSink;
    }

    public IAsyncEnumerable<Incident> ListAsync()
    {
        return GetAllAsync();
    }

    private async IAsyncEnumerable<Incident> GetAllAsync()
    {
        foreach (var i in _incidents)
            yield return i;
        await System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task<Incident?> GetByIdAsync(Guid id)
    {
        return System.Threading.Tasks.Task.FromResult(_incidents.Find(i => i.Id == id));
    }

    public async Task<Incident> CreateIncidentAsync(Guid processInstanceId, string type, string message, string? tenantId = null)
    {
        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = processInstanceId,
            Type = type,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            TenantId = tenantId
        };
        _incidents.Add(incident);
        await _eventSink.EmitAsync(new ProcessMiningEvent {
            EventType = "IncidentCreated",
            ProcessInstanceId = processInstanceId.ToString(),
            TenantId = tenantId,
            Timestamp = DateTimeOffset.UtcNow,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { { "Type", type }, { "Message", message } })
        });
        return incident;
    }

    public async System.Threading.Tasks.Task ResolveIncidentAsync(Guid incidentId)
    {
        var incident = _incidents.Find(i => i.Id == incidentId);
        if (incident != null)
        {
            // For demo: just emit event, no status tracking
            await _eventSink.EmitAsync(new ProcessMiningEvent {
                EventType = "IncidentResolved",
                ProcessInstanceId = incident.ProcessInstanceId.ToString(),
                TenantId = incident.TenantId,
                Timestamp = DateTimeOffset.UtcNow,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { { "IncidentId", incidentId.ToString() } })
            });
        }
    }
}
