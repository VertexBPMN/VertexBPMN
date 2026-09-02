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
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
            .WithEnvironment("OperationalMode", "Development")
            .WithEnvironment("Database__ApplyMigrationsOnStartup", "true")
            .WithReference(bpmnDb)
            .WithReference(tenantDb)
            .WithReference(simulationDb)
            .WithReference(eventsDb)
            .WithReference(decisionDb)
            .WithReference(messaging)
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
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("StudioAuthentication__LocalDevelopmentEnabled", "true")
            .WithEnvironment("StudioHttpsRedirection__Enabled", "false")
            .WithEnvironment("ApiBaseUrl", api.GetEndpoint("http"));
    }
}
