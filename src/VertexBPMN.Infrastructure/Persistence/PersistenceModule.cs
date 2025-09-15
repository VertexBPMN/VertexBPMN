using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence.Repositories;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Infrastructure.Stores;

namespace VertexBPMN.Infrastructure.Persistence;

/// <summary>
/// Extension methods for registering persistence services.
/// </summary>
public static class PersistenceModule
{
    /// <summary>
    /// Adds the BPMN persistence layer (core BPMN DbContext + repositories) to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureDb">A delegate to configure the BpmnDbContext options.</param>
    public static IServiceCollection AddBpmnPersistenceServices(this IServiceCollection services)
    {
        services.AddScoped<IProcessDefinitionRepository, ProcessDefinitionRepository>();
        services.AddScoped<IProcessInstanceRepository, ProcessInstanceRepository>();
        services.AddScoped<IExecutionTokenRepository, ExecutionTokenRepository>();
        services.AddScoped<IVariableRepository, VariableRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IHistoryEventRepository, HistoryEventRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IDesignTimeDbContextFactory<ProcessMiningEventDbContext>, ProcessMiningEventDbContextFactory>();
        services.AddScoped<IProcessInstanceStore, ProductionProcessInstanceStore>();
        return services;
    }

    /// <summary>
    /// Registers all DbContexts used by the API (BPMN, Tenants, Simulation, Process Mining Events) using configuration.
    /// Connection string configuration keys (override with your own):
    ///  ConnectionStrings:Bpmn            (core engine persistence)
    ///  ConnectionStrings:Tenants         (tenant store)
    ///  ConnectionStrings:Simulation      (simulation scenarios)
    ///  ConnectionStrings:ProcessMiningEvents or ProcessMiningEventsSqlite (compat for earlier code)
    /// Fallback: if a key is missing, an in-memory SQLite database is used for that context.
    /// </summary>
    public static IServiceCollection AddAllEngineDbContexts(this IServiceCollection services, IConfiguration configuration)
    {
        // Core BPMN
        var bpmnConn = configuration.GetConnectionString("Bpmn");
        services.AddDbContext<BpmnDbContext>(o =>
        {
            if (!string.IsNullOrWhiteSpace(bpmnConn))
                o.UseSqlite(bpmnConn); // change to UseNpgsql for PostgreSQL in production
            else
                o.UseSqlite("Data Source=vertexbpmn.db");
        });

        // Tenants
        var tenantConn = configuration.GetConnectionString("Tenants");
        services.AddDbContext<TenantDbContext>(o =>
        {
            if (!string.IsNullOrWhiteSpace(tenantConn))
                o.UseSqlite(tenantConn);
            else
                o.UseSqlite("Data Source=tenants.db");
        });

        // Simulation Scenarios
        var simConn = configuration.GetConnectionString("Simulation");
        services.AddDbContext<SimulationScenarioDbContext>(o =>
        {
            if (!string.IsNullOrWhiteSpace(simConn))
                o.UseSqlite(simConn);
            else
                o.UseSqlite("Data Source=simulationscenarios.db");
        });

        // Process Mining Events (compat handling for old key name)
        var miningConn = configuration.GetConnectionString("ProcessMiningEventsSqlite") ?? configuration.GetConnectionString("ProcessMiningEvents");
        services.AddDbContext<ProcessMiningEventDbContext>(o =>
        {
            if (!string.IsNullOrWhiteSpace(miningConn))
                o.UseSqlite(miningConn);
            else
                o.UseSqlite("Data Source=processmining.db");
        });

        return services;
    }
}
