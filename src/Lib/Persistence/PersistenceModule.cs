
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Persistence.Repositories;

namespace VertexBPMN.Persistence;

/// <summary>
/// Extension methods for registering persistence services.
/// </summary>
public static class PersistenceModule
{
    /// <summary>
    /// Adds the BPMN persistence layer to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureDb">A delegate to configure the DbContext options.</param>
    public static IServiceCollection AddBpmnPersistence(this IServiceCollection services, Action<DbContextOptionsBuilder> configureDb)
    {
        services.AddDbContext<BpmnDbContext>(configureDb);
        services.AddScoped<IProcessDefinitionRepository, Repositories.Impl.ProcessDefinitionRepository>();
        services.AddScoped<IProcessInstanceRepository, Repositories.Impl.ProcessInstanceRepository>();
        services.AddScoped<IExecutionTokenRepository, Repositories.Impl.ExecutionTokenRepository>();
        services.AddScoped<IVariableRepository, Repositories.Impl.VariableRepository>();
        services.AddScoped<IJobRepository, Repositories.Impl.JobRepository>();
        services.AddScoped<ITaskRepository, Repositories.Impl.TaskRepository>();
        services.AddScoped<IHistoryEventRepository, Repositories.Impl.HistoryEventRepository>();
        services.AddScoped<IIncidentRepository, Repositories.Impl.IncidentRepository>();
        return services;
    }
}
