using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Infrastructure.Operational;

public sealed class RuntimeMetricsReader(BpmnDbContext db) : IRuntimeMetricsReader
{
    public async ValueTask<IReadOnlyDictionary<string, long>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var activeWorkerCutoff = DateTime.UtcNow.AddMinutes(-2);

        return new Dictionary<string, long>
        {
            ["process_instances_total"] = await db.ProcessInstances.LongCountAsync(cancellationToken),
            ["process_instances_running"] = await db.ProcessInstances
                .LongCountAsync(item => item.Status == ProcessInstanceStatus.Running, cancellationToken),
            ["process_instances_completed"] = await db.ProcessInstances
                .LongCountAsync(item => item.Status == ProcessInstanceStatus.Completed, cancellationToken),
            ["jobs_total"] = await db.Jobs.LongCountAsync(cancellationToken),
            ["jobs_scheduled"] = await db.Jobs
                .LongCountAsync(item => item.State == "Scheduled", cancellationToken),
            ["jobs_executing"] = await db.Jobs
                .LongCountAsync(item => item.State == "Executing", cancellationToken),
            ["jobs_dead_letter"] = await db.Jobs
                .LongCountAsync(item => item.State == "DeadLetter", cancellationToken),
            ["incidents_open"] = await db.Incidents
                .LongCountAsync(item => item.State == "Open", cancellationToken),
            ["subscriptions_active"] = await db.EventSubscriptions
                .LongCountAsync(item => item.State == "Active" || item.State == "Compensation", cancellationToken),
            ["outbox_pending"] = await db.RuntimeOutbox
                .LongCountAsync(item => item.State == "Pending", cancellationToken),
            ["outbox_in_flight"] = await db.RuntimeOutbox
                .LongCountAsync(item => item.State == "InFlight", cancellationToken),
            ["outbox_dead_letter"] = await db.RuntimeOutbox
                .LongCountAsync(item => item.State == "DeadLetter", cancellationToken),
            ["workers_active"] = await db.WorkerRegistrations
                .LongCountAsync(item => item.LastHeartbeat >= activeWorkerCutoff, cancellationToken)
        };
    }
}
