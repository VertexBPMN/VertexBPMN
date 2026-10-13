using Azure.Extensions.AspNetCore.DataProtection.Blobs;
using Azure.Extensions.AspNetCore.DataProtection.Keys;
using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Application;
using VertexBPMN.Application.Configuration;
using VertexBPMN.Application.Messaging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Messaging;
using VertexBPMN.Infrastructure.Operational;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Repositories;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Infrastructure.Stores;


namespace VertexBPMN.Infrastructure;

public static class InfrastructureModule
{
    private static readonly string[] BpmnContextSynonyms = { "BpmnDbContext" };
    private static readonly string[] TenantContextSynonyms = { "TenantDbContext" };
    private static readonly string[] SimulationContextSynonyms = { "SimulationScenarioDbContext" };
    private static readonly string[] ProcessMiningContextSynonyms = { "ProcessMiningEventsSqlite" };
    private static readonly string[] DecisionContextSynonyms = { "DecisionDbContext" };

	[Obsolete("AddBpmnPersistenceServices is superseded by AddAllEngineDbContexts plus the focused persistence registrations; it will be removed in a future release.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar Code Smell", "S1133", Justification = "Intentionally kept obsolete until callers migrate to AddAllEngineDbContexts.")]
	public static IServiceCollection AddBpmnPersistenceServices(this IServiceCollection services, IConfiguration configuration)
        => services.AddRuntimePersistenceServices(configuration);

    /// <summary>Registers durable runtime services; database contexts are configured separately by AddAllEngineDbContexts.</summary>
    public static IServiceCollection AddRuntimePersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        var mode = NormalizeMode(configuration["OperationalMode"]
                                 ?? configuration["ASPNETCORE_ENVIRONMENT"]
                                 ?? "Development");
        var configuredDependencyRegistry = configuration.GetConnectionString("DependencyRegistry")
                                           ?? configuration["DependencyRegistry:ConnectionString"];

        RegisterExternalTaskServices(services, configuration, mode);

        if (mode is "Production" or "Stage" && string.IsNullOrWhiteSpace(configuredDependencyRegistry))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DependencyRegistry is required in Production and Stage; the local file fallback is forbidden.");
        }

        var depRegistryCs = DependencyConfigurationLoader.ResolveConnectionString(configuration);
        var depRegistryProvider = DependencyRegistryProvider.Resolve(depRegistryCs, configuration["DependencyRegistry:Provider"]);
        services.AddDbContext<DependencyRegistryDbContext>(options =>
            DependencyRegistryProvider.Configure(options, depRegistryProvider, depRegistryCs));
        services.AddScoped<IDependencyRegistry, DependencyRegistryService>();
        services.AddScoped<IDesignTimeDbContextFactory<ProcessMiningEventDbContext>, ProcessMiningEventDbContextFactory>();
        services.AddScoped<IProcessInstanceStore, ProductionProcessInstanceStore>();
        services.AddScoped<ISimulationScenarioService, SimulationScenarioService>();
        services.AddScoped<IMessageDispatcher, PersistentMessageDispatcher>();
        services.AddScoped<PersistentProcessMiningEventSink>();
        services.AddSingleton<RuntimeOutboxAnalyticsProjectionService>();
        services.AddHostedService(serviceProvider =>
            serviceProvider.GetRequiredService<RuntimeOutboxAnalyticsProjectionService>());
        services.AddScoped<IRuntimeMetricsReader, RuntimeMetricsReader>();
        services.AddSingleton<RuntimeMetricsState>();
        if (mode != "Test" && configuration.GetValue("Operational:Metrics:Enabled", true))
        {
            services.AddHostedService<RuntimeMetricsCollectorService>();
        }

        if (mode != "Test")
        {
            services.AddHostedService<PollingSchedulerService>();
        }

        ConfigureRuntimeOutbox(services, configuration, mode);
        RegisterRepositoryServices(services);
        var dataProtection = services.AddDataProtection().SetApplicationName("VertexBPMN");
        ConfigureDataProtection(dataProtection, configuration, mode);
        RegisterSourceControlServices(services, configuration, mode);
        services.AddScoped<VertexBPMN.Application.SourceControl.ISourceControlAccessStore>(sp =>
            sp.GetRequiredService<SourceControl.PersistentSourceControlStore>());
        services.AddScoped<IConnectorService, PersistentConnectorService>();
        services.AddScoped<IConnectorTemplateService, PersistentConnectorTemplateService>();
        services.AddScoped<IFormDefinitionService, PersistentFormDefinitionService>();
        services.AddScoped<ITaskIoSnapshotRecorder, TaskIoSnapshotRecorder>();
        services.AddScoped<IOAuth2CredentialFlowService, OAuth2CredentialFlowService>();
        if (mode != "Test")
        {
            services.AddHostedService<OAuth2FlowStateCleanupService>();
        }

        return services;
    }

    private static void RegisterExternalTaskServices(
        IServiceCollection services,
        IConfiguration configuration,
        string mode)
    {
        var externalTasksEnabled = configuration.GetValue<bool>("ExternalTasks:Enabled");
        var schedulingPreviewEnabled = configuration.GetValue<bool>("ExternalTasks:EnableSchedulingPreview");
        if (schedulingPreviewEnabled)
        {
            var previewEnvironment = configuration["OperationalMode"] ?? configuration["ASPNETCORE_ENVIRONMENT"];
            if (previewEnvironment?.Trim().ToLowerInvariant() is not ("development" or "test" or "unittest"))
            {
                throw new InvalidOperationException("External-task scheduling preview is restricted to Development and Test.");
            }
        }
        if (externalTasksEnabled || schedulingPreviewEnabled)
        {
            services.AddScoped<IExternalTaskContractResolver, ConfiguredExternalTaskContractResolver>();
            services.AddScoped<IExternalTaskLeaseService, ExternalTaskLeaseService>();
            services.AddScoped<IExternalTaskRecoveryService, ExternalTaskRecoveryService>();
            if (mode != "Test")
            {
                services.AddHostedService<ExternalTaskRecoveryHostedService>();
            }
        }
    }

    private static void RegisterRepositoryServices(IServiceCollection services)
    {
        services.AddScoped<IProcessDefinitionRepository, ProcessDefinitionRepository>();
        services.AddScoped<IWorkflowTriggerRepository, WorkflowTriggerRepository>();
        services.AddScoped<IProcessInstanceRepository, ProcessInstanceRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IExecutionTokenRepository, ExecutionTokenRepository>();
        services.AddScoped<IVariableRepository, VariableRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IPollingTriggerRepository, PollingTriggerRepository>();
        services.AddScoped<IRecordedOutputQueryService, RecordedOutputQueryService>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IHistoryEventRepository, HistoryEventRepository>();
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IDecisionRepository, DecisionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
    }

    private static void ConfigureDataProtection(
        IDataProtectionBuilder dataProtection,
        IConfiguration configuration,
        string mode)
    {
        var dataProtectionProvider = configuration["DataProtection:Provider"];
        var dataProtectionIsAzure = string.Equals(
            dataProtectionProvider, "AzureBlobKeyVault", StringComparison.OrdinalIgnoreCase);
        if (mode is "Production" or "Stage")
        {
            if (dataProtectionIsAzure)
            {
                if (string.IsNullOrWhiteSpace(configuration["DataProtection:BlobUri"])
                    || string.IsNullOrWhiteSpace(configuration["DataProtection:KeyVaultKeyIdentifier"]))
                {
                    throw new InvalidOperationException(
                        "DataProtection:BlobUri and DataProtection:KeyVaultKeyIdentifier are required in Production/Stage "
                        + "when DataProtection:Provider=AzureBlobKeyVault so replicas share durable, encrypted keys.");
                }
            }
            else
            {
                var keyRingPath = configuration["DataProtection:KeyRingPath"];
                if (string.IsNullOrWhiteSpace(keyRingPath))
                {
                    throw new InvalidOperationException(
                        "DataProtection:KeyRingPath is required in Production and Stage so replicas share durable keys.");
                }

                var directory = Directory.CreateDirectory(Path.GetFullPath(keyRingPath));
                dataProtection.PersistKeysToFileSystem(directory);
            }
        }
        if (dataProtectionIsAzure)
        {
            var blobUri = configuration["DataProtection:BlobUri"]!;
            var keyIdentifier = new Uri(configuration["DataProtection:KeyVaultKeyIdentifier"]!);
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeInteractiveBrowserCredential = true,
            });
            dataProtection.PersistKeysToAzureBlobStorage(new Uri(blobUri), credential);
            dataProtection.ProtectKeysWithAzureKeyVault(keyIdentifier, credential);
        }
    }

    private static void RegisterSourceControlServices(
        IServiceCollection services,
        IConfiguration configuration,
        string mode)
    {
        services.AddScoped<ICredentialService, PersistentCredentialService>();
        services.AddScoped<SourceControl.PersistentSourceControlStore>();
		services.AddScoped<SourceControl.SourceControlAuditExporter>();
        services.AddSingleton<VertexBPMN.SourceControl.Abstractions.IRepositoryHostingTargetResolver, VertexBPMN.Application.SourceControl.RepositoryHostingTargetPolicy>();
        services.AddScoped<SourceControl.ProfiledHostingCredentialBroker>();
        services.AddScoped<SourceControl.GitHubAppTokenBroker>();
		services.AddScoped<SourceControl.IRepositoryHostingAdapter, SourceControl.GitHubHostingAdapter>();
		services.AddScoped<SourceControl.IRepositoryHostingAdapter, SourceControl.AzureDevOpsHostingAdapter>();
		services.AddScoped<SourceControl.RepositoryHostingRegistry>();
        services.AddScoped<SourceControl.ProfiledGitCredentialBroker>();
		services.AddScoped<SourceControl.ProfiledSshCredentialBroker>();
		services.AddScoped<SourceControl.GitSshPolicyRegistry>();
		services.AddScoped<SourceControl.ProfiledGitSshTransport>();
		services.AddScoped<SourceControl.SourceControlSshReadExecutor>();
		services.AddScoped<SourceControl.SourceControlClaimedSshReadRunner>();
		services.AddScoped<SourceControl.ProfiledSshReadService>();
        services.AddScoped<SourceControl.IAzureDevOpsTokenIssuer, SourceControl.AzureDevOpsTokenIssuer>();
        services.AddScoped<SourceControl.GitHttpsPolicyRegistry>();
        services.AddScoped<SourceControl.ProfiledGitHttpsTransport>();
        services.AddScoped<SourceControl.ControlledGitProcess>();
        services.AddScoped<SourceControl.SourceControlWorkspace>();
        services.AddScoped<SourceControl.SourceControlCommitExecutor>();
        services.AddScoped<SourceControl.SourceControlClaimedCommitRunner>();
		services.AddScoped<SourceControl.SourceControlHttpsReadExecutor>();
		services.AddScoped<SourceControl.SourceControlClaimedReadRunner>();
		services.AddScoped<SourceControl.ProfiledGitReadService>();
		services.AddScoped<SourceControl.ProfiledGitWriteTransport>();
        services.AddScoped<SourceControl.SourceControlPushExecutor>();
        services.AddScoped<SourceControl.SourceControlClaimedPushRunner>();
        services.AddScoped<SourceControl.SourceControlPullRequestExecutor>();
        services.AddScoped(sp => new SourceControl.SourceControlPullRequestStatusReader(
            sp.GetRequiredService<SourceControl.PersistentSourceControlStore>(),
            sp.GetRequiredService<SourceControl.GitHubAppTokenBroker>(),
            sp.GetRequiredService<VertexBPMN.Application.SourceControl.ISourceControlActorResolver>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VertexBPMN.SourceControl.Abstractions.SourceControlOptions>>(),
			sp.GetRequiredService<SourceControl.RepositoryHostingRegistry>()));
        services.AddScoped<SourceControl.SourceControlClaimedPullRequestRunner>();
        services.AddScoped<VertexBPMN.Application.SourceControl.ISourceControlActorResolver, SourceControl.KeycloakSourceControlActorResolver>();
        services.AddScoped<VertexBPMN.SourceControl.Abstractions.IModelSourceControlProvider, SourceControl.NativeGitModelSourceControlProvider>();
		ConfigureSshAuthenticationFiles(services, mode);
        if (mode != "Test" && configuration.GetValue<bool>("SourceControl:Enabled"))
        {
            services.AddHostedService<SourceControl.SourceControlMaintenanceHostedService>();
            services.AddHostedService<SourceControl.SourceControlJobsHostedService>();
			services.AddHostedService<SourceControl.SourceControlAuditHostedService>();
        }
    }

	private static void ConfigureSshAuthenticationFiles(IServiceCollection services, string mode)
	{
		services.AddScoped<SourceControl.GitSshAuthenticationFileRecovery>();
		if (mode != "Test")
		{
			services.AddHostedService<SourceControl.SourceControlSshAuthenticationCleanupHostedService>();
		}
	}

    private static void ConfigureRuntimeOutbox(
        IServiceCollection services,
        IConfiguration configuration,
        string mode)
    {
        var options = new RuntimeOutboxOptions();
        configuration.GetSection("Runtime:Outbox").Bind(options);
        options.ConnectionString ??=
            configuration.GetConnectionString("messaging");
        var productionMode = mode is "Production" or "Stage";
        var provider = options.Provider.Trim().ToLowerInvariant();
        var azureManagedIdentity =
            provider == "azureservicebus"
            && options.AuthenticationMode == RuntimeOutboxAuthenticationMode.ManagedIdentity;

        ValidateRuntimeOutbox(provider, productionMode, options, azureManagedIdentity);

        services.AddSingleton(options);
        services.AddSingleton<IRuntimeOutboxTransport>(_ =>
            provider switch
            {
                "kafka" => new KafkaRuntimeOutboxTransport(options),
                "rabbitmq" => new RabbitMqRuntimeOutboxTransport(options),
                "azureservicebus" => new AzureServiceBusRuntimeOutboxTransport(options),
                _ => new DisabledRuntimeOutboxTransport()
            });
        if (options.Enabled)
        {
            services.AddHostedService<RuntimeOutboxPublisherService>();
        }

        RegisterRuntimeInboxConsumer(services, configuration, provider, options);
    }

    private static void ValidateRuntimeOutbox(
        string provider,
        bool productionMode,
        RuntimeOutboxOptions options,
        bool azureManagedIdentity)
    {
        if (productionMode && !options.Enabled)
        {
            throw new InvalidOperationException(
                "Runtime:Outbox:Enabled must be true in Production and Stage.");
        }

        if (options.Enabled
            && string.IsNullOrWhiteSpace(options.ConnectionString)
            && !azureManagedIdentity)
        {
            throw new InvalidOperationException(
                "Runtime:Outbox:ConnectionString is required when the outbox publisher is enabled, " +
                "unless AuthenticationMode=ManagedIdentity is used with Azure Service Bus.");
        }

        if (productionMode && provider is not ("kafka" or "rabbitmq" or "azureservicebus"))
        {
            throw new InvalidOperationException(
                "Runtime:Outbox:Provider must be Kafka, RabbitMq or AzureServiceBus in Production and Stage.");
        }
    }

    private static void RegisterRuntimeInboxConsumer(
        IServiceCollection services,
        IConfiguration configuration,
        string provider,
        RuntimeOutboxOptions options)
    {
        // Produktioneller Inbox-Konsument (idempotent). Optional separat
        // zuschaltbar ueber Runtime:Inbox:Enabled; faellt ohne Flag zurueck
        // auf den Outbox-Enabled-Zustand, damit der at-least-once-Zustellungs-
        // und Wiederanlauf-Kreis im Produktivpfad geschlossen ist.
        // Registrierung pro aktivem Provider: genau EIN Inbox-Konsument. Ein Service-Bus-
        // oder Kafka-Deployment darf keinen RabbitMQ-Consumer starten.
        var inboxOptions = new RuntimeInboxOptions();
        configuration.GetSection("Runtime:Inbox").Bind(inboxOptions);
        services.AddSingleton(inboxOptions);
        // Optionales TypeSafe-Envelope-Konformitäts-Judgment (P3, config-gated via Runtime:TypeSafeConformance).
        // Standardmäßig deaktiviert => der Inbox-Pfad bleibt unverändert; Aktivierung erst mit explizitem
        // TypeSafe:Enabled + ApiKey (server-seitig). Der Key steht niemals in Repos/Clients.
        var tsOptions = new TypeSafeConformanceOptions();
        configuration.GetSection("Runtime:TypeSafeConformance").Bind(tsOptions);
        services.AddSingleton(tsOptions);
        services.AddSingleton<ITypeSafeConformanceClient>(sp =>
        {
            var o = sp.GetRequiredService<TypeSafeConformanceOptions>();
            return new TypeSafeConformanceClient(
                null,
                o,
                sp.GetRequiredService<global::Microsoft.Extensions.Logging.ILogger<TypeSafeConformanceClient>>());
        });
        services.AddSingleton<ITypeSafeEnvelopeConformanceValidator, TypeSafeEnvelopeConformanceValidator>();
        var inboxEnabled = inboxOptions.Enabled ?? options.Enabled;
        if (inboxEnabled)
        {
            switch (provider)
            {
                case "rabbitmq":
                    if (string.IsNullOrWhiteSpace(options.ConnectionString))
                    {
                        throw new InvalidOperationException(
                            "Runtime:Outbox:ConnectionString is required for the RabbitMQ inbox consumer.");
                    }

                    services.AddHostedService<RuntimeInboxConsumerService>();
                    break;
                case "azureservicebus":
                    services.AddHostedService<AzureServiceBusRuntimeInboxConsumerService>();
                    break;
                case "kafka":
                    // Kafka-Outbox-Publisher bleibt; ein Kafka-Inbox-Konsument existiert nicht.
                    // Kein stiller Fallback auf einen anderen Consumer - explizit validieren.
                    throw new InvalidOperationException(
                        "Runtime inbox consumption is not supported for Provider=Kafka. "
                        + "Use RabbitMQ or AzureServiceBus for inbox; the Kafka outbox publisher remains available.");
                default:
                    throw new InvalidOperationException(
                        $"Runtime inbox consumer cannot be enabled for Provider '{options.Provider}'. "
                        + "Use RabbitMQ or AzureServiceBus.");
            }
        }
    }

    /// <summary>
    /// Registers all engine DbContexts. Provider is inferred from the resolved connection string:
    ///  - PostgreSQL: contains "Host=" or typical keywords (Host/Port/Username/Database)
    ///  - SQL Server: contains "Server=" or "Initial Catalog=" or ("Data Source=" + "Initial Catalog=")
    ///  - SQLite: "Data Source=" *.db / *.sqlite / :memory: OR "Filename="
    ///  - Missing/empty: EF InMemory provider
    /// Per-mode selection (Production/Stage/Development/Test) can still be handled outside by choosing the active connection string keys.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public static IServiceCollection AddAllEngineDbContexts(this IServiceCollection services, IConfiguration configuration)
    {
        var mode = (configuration["OperationalMode"]
                    ?? configuration["ASPNETCORE_ENVIRONMENT"]
                    ?? "Development").Trim();

        var normalizedMode = NormalizeMode(mode);

        var contexts = new[]
        {
            new DbContextDescriptor("Bpmn",                BpmnContextSynonyms),
            new DbContextDescriptor("Tenants",             TenantContextSynonyms),
            new DbContextDescriptor("Simulation",          SimulationContextSynonyms),
            new DbContextDescriptor("ProcessMiningEvents", ProcessMiningContextSynonyms),
            new DbContextDescriptor("Decision",            DecisionContextSynonyms)
        };

        foreach (var ctx in contexts)
        {
            var cs = ResolveConnectionString(configuration, ctx, normalizedMode);
            var provider = InferProvider(cs);

            if (normalizedMode is "Production" or "Stage"
                && (string.IsNullOrWhiteSpace(cs) || provider == "inmemory"
                    || cs.Contains(":memory:", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"A durable connection string for '{ctx.LogicalName}' is required in {normalizedMode}. " +
                    "InMemory and SQLite :memory: providers are forbidden.");
            }

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
                    options.UseVertexNpgsql(connectionString);
                    break;
                case "sqlserver":
                    options.UseVertexSqlServer(connectionString);
                    break;
                default:
                    options.UseSqlite(connectionString);
                    break;
            }
        });
    }


    private static string InferProvider(string? cs)
    {
        if (string.IsNullOrWhiteSpace(cs))
		{
			return "inmemory";
		}

		var lower = cs.ToLowerInvariant();

        // PostgreSQL detection
        if (lower.Contains("host=", StringComparison.Ordinal) || (lower.Contains("username=", StringComparison.Ordinal) && lower.Contains("database=", StringComparison.Ordinal)))
		{
			return "npgsql";
		}

		// SQL Server detection
		if (lower.Contains("server=", StringComparison.Ordinal) ||
            lower.Contains("initial catalog=", StringComparison.Ordinal) ||
            (lower.Contains("data source=", StringComparison.Ordinal) && (lower.Contains("initial catalog=", StringComparison.Ordinal) || lower.Contains("user id=", StringComparison.Ordinal))))
		{
			return "sqlserver";
		}

		// SQLite detection
		if (lower.Contains("data source=", StringComparison.Ordinal) &&
            (lower.Contains(".db", StringComparison.Ordinal) || lower.Contains(".sqlite", StringComparison.Ordinal) || lower.Contains(":memory:", StringComparison.Ordinal)))
		{
			return "sqlite";
		}

		if (lower.Contains("filename=", StringComparison.Ordinal))
		{
			return "sqlite";
		}

		if (lower.Contains(":memory:", StringComparison.Ordinal))
		{
			return "sqlite";
		}

		// Default fallback
		return "sqlite";
    }

    private static string? ResolveConnectionString(IConfiguration configuration, DbContextDescriptor descriptor, string mode)
    {
        var csSection = configuration.GetSection("ConnectionStrings");
        if (!csSection.Exists())
		{
			return null;
		}

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
			{
				return value;
			}
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

    private sealed record DbContextDescriptor(string LogicalName, string[] Synonyms);
}
