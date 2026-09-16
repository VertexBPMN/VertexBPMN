var builder = DistributedApplication.CreateBuilder(args);
VertexBpmnAppHostTopology.Configure(builder);
builder.Build().Run();

/// <summary>
/// Defines the VertexBPMN distributed application topology.
/// </summary>
public static class VertexBpmnAppHostTopology
{
    public static void Configure(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var hostingMode = builder.Configuration["VertexBPMN:ApiHostingMode"]?.Trim();

        if (hostingMode?.Equals("Container", StringComparison.OrdinalIgnoreCase) == true)
        {
            ConfigureContainerMode(builder);
            return;
        }

        if (hostingMode?.Equals("ExternalServices", StringComparison.OrdinalIgnoreCase) == true
            || hostingMode?.Equals("ExternalWslc", StringComparison.OrdinalIgnoreCase) == true)
        {
            ConfigureExternalServicesMode(builder);
            return;
        }

        ConfigureProjectMode(builder);
    }

    public static void ConfigureContainerMode(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var api = builder
            .AddDockerfile(
                "api",
                contextPath: "../..",
                dockerfilePath: "Dockerfile")
            .WithHttpEndpoint(
                targetPort: 8080,
                port: 51870,
                name: "http")
            .WithHttpHealthCheck("/api/ready")
            .WithEnvironment(
                "ASPNETCORE_ENVIRONMENT",
                "Development")
            .WithEnvironment(
                "OperationalMode",
                "Development")
            .WithEnvironment(
                "Database__ApplyMigrationsOnStartup",
                "true")
            .WithEnvironment(
                "ConnectionStrings__Bpmn",
                "Data Source=/var/lib/vertexbpmn/dev-bpmn.db")
            .WithEnvironment(
                "ConnectionStrings__Tenants",
                "Data Source=/var/lib/vertexbpmn/dev-tenants.db")
            .WithEnvironment(
                "ConnectionStrings__Simulation",
                "Data Source=/var/lib/vertexbpmn/dev-simulation.db")
            .WithEnvironment(
                "ConnectionStrings__ProcessMiningEvents",
                "Data Source=/var/lib/vertexbpmn/dev-events.db")
            .WithEnvironment(
                "ConnectionStrings__Decision",
                "Data Source=/var/lib/vertexbpmn/dev-decision.db")
            .WithEnvironment(
                "ConnectionStrings__DependencyRegistry",
                "Data Source=/var/lib/vertexbpmn/dev-dependencies.db")
            .WithVolume(
                "vertexbpmn-state",
                "/var/lib/vertexbpmn");

        builder
            .AddProject<Projects.VertexBPMN_Studio>("studio")
            .WithHttpEndpoint(
                port: 5263,
                name: "http")
            .WithHttpHealthCheck("/health")
            .WaitFor(api)
            .WithEnvironment(
                "ASPNETCORE_ENVIRONMENT",
                "Development")
            .WithEnvironment(
                "StudioAuthentication__LocalDevelopmentEnabled",
                "true")
            .WithEnvironment(
                "ApiBaseUrl",
                api.GetEndpoint("http"));
    }

    public static void ConfigureProjectMode(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var postgres = builder
            .AddPostgres("postgres")
            .WithDataVolume();

        var bpmnDb = postgres.AddDatabase(
            "BpmnDbContext",
            "vertexbpmn_bpmn");

        var tenantDb = postgres.AddDatabase(
            "TenantDbContext",
            "vertexbpmn_tenants");

        var simulationDb = postgres.AddDatabase(
            "SimulationScenarioDbContext",
            "vertexbpmn_simulation");

        var eventsDb = postgres.AddDatabase(
            "ProcessMiningEvents",
            "vertexbpmn_events");

        var decisionDb = postgres.AddDatabase(
            "DecisionDbContext",
            "vertexbpmn_decision");

        var rabbitMq = builder
            .AddRabbitMQ("messaging")
            .WithManagementPlugin()
            .WithDataVolume();

        var api = builder
            .AddProject<Projects.VertexBPMN_Api>("api")
            .WithHttpEndpoint(
                port: 51870,
                name: "http")
            .WithHttpHealthCheck("/api/ready")
            .WithEnvironment(
                "ASPNETCORE_ENVIRONMENT",
                "Development")
            .WithEnvironment(
                "DOTNET_ENVIRONMENT",
                "Development")
            .WithEnvironment(
                "OperationalMode",
                "Development")
            .WithEnvironment(
                "Database__ApplyMigrationsOnStartup",
                "true")
            .WithReference(bpmnDb)
            .WithReference(tenantDb)
            .WithReference(simulationDb)
            .WithReference(eventsDb)
            .WithReference(decisionDb)
            .WaitFor(postgres)
            .WithReference(rabbitMq)
            .WaitFor(rabbitMq)
            .WithEnvironment("Runtime__Outbox__Enabled", "true")
            .WithEnvironment("Runtime__Outbox__Provider", "RabbitMq");

        builder
            .AddProject<Projects.VertexBPMN_Studio>("studio")
            .WithHttpEndpoint(
                port: 5263,
                name: "http")
            .WithHttpHealthCheck("/health")
            .WithReference(api)
            .WaitFor(api)
            .WithEnvironment(
                "ASPNETCORE_ENVIRONMENT",
                "Development")
            .WithEnvironment(
                "StudioAuthentication__LocalDevelopmentEnabled",
                "true")
            .WithEnvironment(
                "ApiBaseUrl",
                api.GetEndpoint("http"));
    }

    /// <summary>
    /// Runs API and Studio as Aspire projects while consuming PostgreSQL and RabbitMQ
    /// instances whose lifecycle is managed outside DCP, for example by WSLC or local services.
    /// </summary>
    public static void ConfigureExternalServicesMode(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var useOidcTest = string.Equals(
            builder.Configuration["VertexBPMN:AuthenticationMode"],
            "OidcTest",
            StringComparison.OrdinalIgnoreCase);
        var contractReviewerEnabled = bool.TryParse(
            builder.Configuration["VertexBPMN:ContractReviewer:Enabled"], out var enabled) && enabled;
        var oidcAuthority = builder.Configuration["VertexBPMN:Oidc:Authority"];
        if (contractReviewerEnabled && !useOidcTest)
            throw new InvalidOperationException(
                "VertexBPMN:ContractReviewer:Enabled requires the OidcTest external-services profile.");
        if (useOidcTest)
        {
            if (!Uri.TryCreate(oidcAuthority, UriKind.Absolute, out var authorityUri)
                || !authorityUri.IsLoopback
                || !authorityUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "VertexBPMN:Oidc:Authority must be an HTTP loopback URI in the local OidcTest AppHost profile.");
            }

        }
        var studioClientSecret = useOidcTest
            ? builder.AddParameter("oidcStudioClientSecret", secret: true)
            : null;
        var workerClientSecret = contractReviewerEnabled
            ? builder.AddParameter("oidcWorkerClientSecret", secret: true)
            : null;

        var bpmnDb = builder.AddConnectionString("BpmnDbContext");
        var tenantDb = builder.AddConnectionString("TenantDbContext");
        var simulationDb = builder.AddConnectionString("SimulationScenarioDbContext");
        var eventsDb = builder.AddConnectionString("ProcessMiningEvents");
        var decisionDb = builder.AddConnectionString("DecisionDbContext");
        var messaging = builder.AddConnectionString("messaging");

        var api = builder
            .AddProject<Projects.VertexBPMN_Api>("api")
            .WithHttpEndpoint(
                port: 51870,
                name: "http")
            .WithHttpHealthCheck("/api/ready")
            .WithEnvironment("Database__ApplyMigrationsOnStartup", "true")
            .WithReference(bpmnDb)
            .WithReference(tenantDb)
            .WithReference(simulationDb)
            .WithReference(eventsDb)
            .WithReference(decisionDb)
            .WithReference(messaging)
            .WithEnvironment("Runtime__Outbox__Enabled", "true")
            .WithEnvironment("Runtime__Outbox__Provider", "RabbitMq");

        if (useOidcTest)
        {
            api.WithEnvironment("ASPNETCORE_ENVIRONMENT", "OidcTest")
                .WithEnvironment("DOTNET_ENVIRONMENT", "OidcTest")
                .WithEnvironment("OperationalMode", "OidcTest")
                .WithEnvironment("Jwt__Authority", oidcAuthority!)
                .WithEnvironment("Jwt__Issuer", oidcAuthority!)
                .WithEnvironment("Jwt__Audience", "vertexbpmn-api")
                .WithEnvironment("Jwt__RequireHttpsMetadata", "false")
                .WithEnvironment("Jwt__UseDevelopmentApiKey", "false");
        }
        else
        {
            api.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
                .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
                .WithEnvironment("OperationalMode", "Development");
        }

        if (contractReviewerEnabled)
        {
            var tenant = builder.Configuration["VertexBPMN:ContractReviewer:TenantId"]?.Trim();
            var model = builder.Configuration["VertexBPMN:ContractReviewer:Model"]?.Trim();
            var modelEndpoint = builder.Configuration["VertexBPMN:ContractReviewer:Endpoint"]?.Trim()
                ?? "http://127.0.0.1:11434/";
            if (string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException(
                    "VertexBPMN:ContractReviewer:TenantId and Model are required when the worker is enabled.");

            api.WithEnvironment("ExternalTasks__Enabled", "true")
                .WithEnvironment("ExternalTasks__EnableSchedulingPreview", "false")
                .WithEnvironment("ExternalTasks__Contracts__0__TenantId", tenant)
                .WithEnvironment("ExternalTasks__Contracts__0__Enabled", "true")
                .WithEnvironment("ExternalTasks__Contracts__0__Topic", "agent.contract-review")
                .WithEnvironment("ExternalTasks__Contracts__0__Version", "contract-review.v1")
                .WithEnvironment("ExternalTasks__Contracts__0__AgentProfileRef", "contract-reviewer.v1")
                .WithEnvironment("ExternalTasks__Contracts__0__AgentProfileVersion", "contract-reviewer.v1")
                .WithEnvironment("ExternalTasks__Contracts__0__MaxAttempts", "2")
                .WithEnvironment("ExternalTasks__Contracts__0__MaxDeadlineSeconds", "300");
            ConfigureContractFields(api);

            builder.AddProject<Projects.VertexBPMN_AgentWorker>("agent-worker")
                .WithReference(api)
                .WaitFor(api)
                .WithEnvironment("ExternalTaskWorker__Enabled", "true")
                .WithEnvironment("ExternalTaskWorker__BaseAddress", api.GetEndpoint("http"))
                .WithEnvironment("ExternalTaskWorker__TokenEndpoint",
                    $"{oidcAuthority!.TrimEnd('/')}/protocol/openid-connect/token")
                .WithEnvironment("ExternalTaskWorker__ClientId", "vertexbpmn-contract-reviewer")
                .WithEnvironment("ExternalTaskWorker__ClientSecret", workerClientSecret!)
                .WithEnvironment("ExternalTaskWorker__Scope", "openid")
                .WithEnvironment("ExternalTaskWorker__Topics__0", "agent.contract-review")
                .WithEnvironment("ExternalTaskWorker__MaxConcurrency", "1")
                .WithEnvironment("ExternalTaskWorker__MaxTasksPerClaim", "1")
                .WithEnvironment("ExternalTaskWorker__LeaseSeconds", "60")
                .WithEnvironment("ExternalTaskWorker__HeartbeatSeconds", "20")
                .WithEnvironment("ContractReviewer__Enabled", "true")
                .WithEnvironment("ContractReviewer__Endpoint", modelEndpoint)
                .WithEnvironment("ContractReviewer__Model", model);
        }

        var studio = builder
            .AddProject<Projects.VertexBPMN_Studio>("studio")
            .WithHttpEndpoint(
                port: 5263,
                name: "http")
            .WithHttpHealthCheck("/health")
            .WithReference(api)
            .WaitFor(api)
            .WithEnvironment("StudioHttpsRedirection__Enabled", "false")
            .WithEnvironment("ApiBaseUrl", api.GetEndpoint("http"));

        if (useOidcTest)
        {
            studio.WithEnvironment("ASPNETCORE_ENVIRONMENT", "OidcTest")
                .WithEnvironment("DOTNET_ENVIRONMENT", "OidcTest")
                .WithEnvironment("StudioAuthentication__Authority", oidcAuthority!)
                .WithEnvironment("StudioAuthentication__ClientId", "vertexbpmn-studio")
                .WithEnvironment("StudioAuthentication__ClientSecret", studioClientSecret!)
                .WithEnvironment("StudioAuthentication__ClaimsScope", "vertexbpmn-claims")
                .WithEnvironment("StudioAuthentication__RequireClientSecret", "true")
                .WithEnvironment("StudioAuthentication__RequireHttpsMetadata", "false")
                .WithEnvironment("StudioAuthentication__LocalDevelopmentEnabled", "false")
                .WithEnvironment("StudioAuthentication__UiTestEnabled", "false");
        }
        else
        {
            studio.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
                .WithEnvironment("StudioAuthentication__LocalDevelopmentEnabled", "true");
        }
    }

    private static void ConfigureContractFields<T>(IResourceBuilder<T> api) where T : IResourceWithEnvironment
    {
        var inputs = new[]
        {
            ("document", "string", 65536), ("documentId", "string", 256),
            ("documentVersion", "string", 256)
        };
        var outputs = new[]
        {
            ("schemaVersion", "string", 64), ("documentVersion", "string", 256),
            ("summary", "string", 4096), ("findings", "string", 65536),
            ("uncertainties", "string", 32768), ("requiresHumanReview", "boolean", 1),
            ("promptVersion", "string", 128), ("documentHash", "string", 64)
        };
        ConfigureFields(api, "Inputs", inputs);
        ConfigureFields(api, "Outputs", outputs);
    }

    private static void ConfigureFields<T>(IResourceBuilder<T> api, string group,
        IReadOnlyList<(string Name, string Type, int MaxLength)> fields) where T : IResourceWithEnvironment
    {
        for (var index = 0; index < fields.Count; index++)
        {
            var prefix = $"ExternalTasks__Contracts__0__{group}__{index}";
            api.WithEnvironment($"{prefix}__Name", fields[index].Name)
                .WithEnvironment($"{prefix}__Type", fields[index].Type)
                .WithEnvironment($"{prefix}__MaxLength", fields[index].MaxLength.ToString())
                .WithEnvironment($"{prefix}__AllowExternalTransfer", "true");
        }
    }
}
