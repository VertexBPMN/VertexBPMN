using Microsoft.Extensions.Diagnostics.HealthChecks;
using VertexBPMN.Api.Operational;
using VertexBPMN.Infrastructure.Messaging;

namespace VertexBPMN.Api.Health;

public sealed class OperationalReadinessHealthCheck(
    IServiceProvider services,
    IRuntimeOutboxTransport outboxTransport,
    RuntimeOutboxOptions outboxOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var databases = await DatabaseMigrationCoordinator.InspectAsync(services, cancellationToken);
        var databaseFailures = databases
            .Where(status => !status.CanConnect || status.PendingMigrations.Count > 0)
            .ToArray();
        var broker = outboxOptions.Enabled
            ? await outboxTransport.CheckHealthAsync(cancellationToken)
            : new OutboxTransportHealth(true, "Outbox publishing is disabled for this environment.");

        var data = new Dictionary<string, object>
        {
            ["databases"] = databases.Select(status => new
            {
                status.Name,
                status.CanConnect,
                pendingMigrations = status.PendingMigrations.Count
            }).ToArray(),
            ["broker"] = broker.Description,
            ["brokerHealthy"] = broker.IsHealthy
        };

        if (databaseFailures.Length > 0 || !broker.IsHealthy)
        {
            var reasons = new List<string>();
            reasons.AddRange(databaseFailures.Select(status =>
                !status.CanConnect
                    ? $"{status.Name} cannot connect"
                    : $"{status.Name} has {status.PendingMigrations.Count} pending migration(s)"));
            if (!broker.IsHealthy)
                reasons.Add(broker.Description);
            return HealthCheckResult.Unhealthy(string.Join("; ", reasons), data: data);
        }

        return HealthCheckResult.Healthy("Databases, schemas and broker are ready.", data);
    }
}
