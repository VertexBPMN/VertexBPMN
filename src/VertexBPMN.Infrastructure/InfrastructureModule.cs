using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.Configuration;
using VertexBPMN.Application.Messaging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Repositories;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Infrastructure.Stores;

namespace VertexBPMN.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddBpmnPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DependencyRegistryDbContext>(options =>
            options.UseSqlite(DependencyConfigurationLoader.ResolveConnectionString(configuration)));
        services.AddScoped<IDependencyRegistry, DependencyRegistryService>();
        services.AddScoped<IDesignTimeDbContextFactory<ProcessMiningEventDbContext>, ProcessMiningEventDbContextFactory>();
        services.AddScoped<IProcessInstanceStore, ProductionProcessInstanceStore>();
        services.AddScoped<ISimulationScenarioService, SimulationScenarioService>();
        services.AddScoped<IMessageDispatcher, InMemoryMessageDispatcher>();
        services.AddScoped<IProcessDefinitionRepository, ProcessDefinitionRepository>();
        services.AddScoped<IProcessInstanceRepository, ProcessInstanceRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IExecutionTokenRepository, ExecutionTokenRepository>();
        services.AddScoped<IVariableRepository, VariableRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IHistoryEventRepository, HistoryEventRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IDecisionRepository, DecisionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        return services;
    }

    /// <summary>
    /// Registers all engine DbContexts. Provider is inferred from the resolved connection string:
    ///  - PostgreSQL: contains "Host=" or typical keywords (Host/Port/Username/Database)
    ///  - SQL Server: contains "Server=" or "Initial Catalog=" or ("Data Source=" + "Initial Catalog=")
    ///  - SQLite: "Data Source=" *.db / *.sqlite / :memory: OR "Filename="
    ///  - Missing/empty: EF InMemory provider
    /// Per-mode selection (Production/Stage/Development/Test) can still be handled outside by choosing the active connection string keys.
    /// </summary>
    public static IServiceCollection AddAllEngineDbContexts(this IServiceCollection services, IConfiguration configuration)
    {
        var mode = (configuration["OperationalMode"]
                    ?? configuration["ASPNETCORE_ENVIRONMENT"]
                    ?? "Development").Trim();

        var normalizedMode = NormalizeMode(mode);

        var contexts = new[]
        {
            new DbContextDescriptor("Bpmn",                new[] { "BpmnDbContext" }),
            new DbContextDescriptor("Tenants",             new[] { "TenantDbContext" }),
            new DbContextDescriptor("Simulation",          new[] { "SimulationScenarioDbContext" }),
            new DbContextDescriptor("ProcessMiningEvents", new[] { "ProcessMiningEventsSqlite" }),
            new DbContextDescriptor("Decision",            new[] { "DecisionDbContext" })
        };

        foreach (var ctx in contexts)
        {
            var cs = ResolveConnectionString(configuration, ctx, normalizedMode);
            var provider = InferProvider(cs);

            Register(services, ctx, provider, cs);
        }

        services.AddScoped<IDesignTimeDbContextFactory<ProcessMiningEventDbContext>, ProcessMiningEventDbContextFactory>();
        return services;
    }

        private static void Register(IServiceCollection services, DbContextDescriptor descriptor, string provider, string? cs)
        {
            switch (descriptor.LogicalName)
            {
                case "Bpmn":
                    RegisterDbContext<BpmnDbContext>(services, provider, cs, descriptor.LogicalName);
                    break;
                case "Tenants":
                    RegisterDbContext<TenantDbContext>(services, provider, cs, descriptor.LogicalName);
                    break;
                case "Simulation":
                    RegisterDbContext<SimulationScenarioDbContext>(services, provider, cs, descriptor.LogicalName);
                    break;
                case "ProcessMiningEvents":
                    RegisterDbContext<ProcessMiningEventDbContext>(services, provider, cs, descriptor.LogicalName);
                    break; 
                case "Decision":
                    RegisterDbContext<DecisionDbContext>(services, provider, cs, descriptor.LogicalName);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown context {descriptor.LogicalName}");
            }
        }

    private static void RegisterDbContext<TContext>(
        IServiceCollection services,
        string provider,
        string? connectionString,
        string logicalName) where TContext : DbContext
    {
        services.AddDbContext<TContext>(options =>
        {
            // InMemory fallback (no connection string or explicit inmemory provider)
            if (provider == "inmemory" || string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseInMemoryDatabase(logicalName + "Db");
                return;
            }

            switch (provider)
            {
                case "npgsql":
                    options.UseNpgsql(connectionString);
                    break;
                case "sqlserver":
                    options.UseSqlServer(connectionString);
                    break;
                case "sqlite":
                default:
                    options.UseSqlite(connectionString);
                    break;
            }
        });
    }
        

    private static string InferProvider(string? cs)
    {
        if (string.IsNullOrWhiteSpace(cs))
            return "inmemory";

        var lower = cs.ToLowerInvariant();

        // PostgreSQL detection
        if (lower.Contains("host=") || (lower.Contains("username=") && lower.Contains("database=")))
            return "npgsql";

        // SQL Server detection
        if (lower.Contains("server=") ||
            lower.Contains("initial catalog=") ||
            (lower.Contains("data source=") && (lower.Contains("initial catalog=") || lower.Contains("user id="))))
            return "sqlserver";

        // SQLite detection
        if (lower.Contains("data source=") &&
            (lower.Contains(".db") || lower.Contains(".sqlite") || lower.Contains(":memory:")))
            return "sqlite";
        if (lower.Contains("filename="))
            return "sqlite";
        if (lower.Contains(":memory:"))
            return "sqlite";

        // Default fallback
        return "sqlite";
    }

    private static string? ResolveConnectionString(IConfiguration configuration, DbContextDescriptor descriptor, string mode)
    {
        var csSection = configuration.GetSection("ConnectionStrings");
        if (!csSection.Exists()) return null;

        var candidates = new List<string>
        {
            $"{descriptor.LogicalName}_{mode}"
        };
        candidates.AddRange(descriptor.Synonyms.Select(s => $"{s}_{mode}"));
        candidates.Add(descriptor.LogicalName);
        candidates.AddRange(descriptor.Synonyms);

        foreach (var key in candidates)
        {
            var value = configuration.GetConnectionString(key) ?? csSection[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string NormalizeMode(string raw) =>
        raw.Trim().ToLowerInvariant() switch
        {
            "prod" or "production" => "Production",
            "stage" or "staging"   => "Stage",
            "test" or "unittest"   => "Test",
            _                      => "Development"
        };

    private sealed record DbContextDescriptor(string LogicalName, string[] Synonyms)
    {
        public Type GetDbContextType() =>
            LogicalName switch
            {
                "Bpmn"                => typeof(BpmnDbContext),
                "Tenants"             => typeof(TenantDbContext),
                "Simulation"          => typeof(SimulationScenarioDbContext),
                "ProcessMiningEvents" => typeof(ProcessMiningEventDbContext),
                "Decision"            => typeof(DecisionDbContext),
                _ => throw new InvalidOperationException($"Unknown context {LogicalName}")
            };
    }
}
