using Microsoft.Extensions.Diagnostics.HealthChecks;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Health;

public sealed class ServiceDependenciesHealthCheck(IServiceProvider serviceProvider) : IHealthCheck
{
    private static readonly Type[] CriticalServices =
    {
        typeof(IRuntimeService),
        typeof(IRepositoryService),
        typeof(IHistoryService),
        typeof(IDecisionService),
        typeof(IJobRepository),
        typeof(IProcessDefinitionRepository)
        //typeof(IPluginManager)
    };

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var sp = serviceProvider;
        var missing = new List<string>();
        var data = new Dictionary<string, object?>();

        foreach (var t in CriticalServices)
        {
            try
            {
                _ = sp.GetService(t) ?? throw new InvalidOperationException("Not registered");
            }
            catch
            {
                missing.Add(t.Name);
            }
        }

        data["missingServices"] = missing;
        data["serviceCountResolved"] = CriticalServices.Length - missing.Count;

        //if (sp.GetService(typeof(IPluginManager)) is IPluginManager pluginManager)
        //{
        //    data["pluginsLoaded"] = pluginManager.LoadedPluginCount;
        //}

        try
        {
            if (sp.GetService(typeof(BpmnDbContext)) is BpmnDbContext db)
            {
                _ = db.Model;
            }
        }
        catch (Exception ex)
        {
            data["dbModelError"] = ex.Message;
            return Task.FromResult(HealthCheckResult.Unhealthy("Failed to access BpmnDbContext model", data: data));
        }

        if (missing.Count > 0)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy($"Missing {missing.Count} critical services", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("All critical services resolved", data));
    }
}