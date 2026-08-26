using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Api.Operational;

public static class DatabaseMigrationCoordinator
{
    public static async Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        foreach (var context in ResolveContexts(scope.ServiceProvider))
        {
            if (context.Database.IsRelational())
                await context.Database.MigrateAsync(cancellationToken);
            else
                await context.Database.EnsureCreatedAsync(cancellationToken);
        }
    }

    public static async Task<IReadOnlyList<DatabaseSchemaStatus>> InspectAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var statuses = new List<DatabaseSchemaStatus>();
        foreach (var context in ResolveContexts(scope.ServiceProvider))
        {
            var name = context.GetType().Name;
            try
            {
                if (!await context.Database.CanConnectAsync(cancellationToken))
                {
                    statuses.Add(new DatabaseSchemaStatus(name, false, ["Database connection failed."]));
                    continue;
                }

                var pending = context.Database.IsRelational()
                    ? (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray()
                    : [];
                statuses.Add(new DatabaseSchemaStatus(name, true, pending));
            }
            catch (Exception ex)
            {
                statuses.Add(new DatabaseSchemaStatus(name, false, [], ex.Message));
            }
        }

        return statuses;
    }

    public static async Task EnsureCurrentAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var statuses = await InspectAsync(services, cancellationToken);
        var failures = statuses
            .Where(status => !status.CanConnect || status.PendingMigrations.Count > 0)
            .Select(status => status.CanConnect
                ? $"{status.Name}: pending migrations [{string.Join(", ", status.PendingMigrations)}]"
                : $"{status.Name}: {status.Error ?? "cannot connect"}")
            .ToArray();
        if (failures.Length > 0)
            throw new InvalidOperationException(
                $"Database schema readiness failed: {string.Join("; ", failures)}");
    }

    private static DbContext[] ResolveContexts(IServiceProvider services) =>
    [
        services.GetRequiredService<BpmnDbContext>(),
        services.GetRequiredService<TenantDbContext>(),
        services.GetRequiredService<SimulationScenarioDbContext>(),
        services.GetRequiredService<ProcessMiningEventDbContext>(),
        services.GetRequiredService<DecisionDbContext>(),
        services.GetRequiredService<DependencyRegistryDbContext>()
    ];
}

public sealed record DatabaseSchemaStatus(
    string Name,
    bool CanConnect,
    IReadOnlyList<string> PendingMigrations,
    string? Error = null);
