var builder = DistributedApplication.CreateBuilder(args);

var useContainer =
    builder.Configuration["VertexBPMN:ApiHostingMode"]
        ?.Equals("Container", StringComparison.OrdinalIgnoreCase)
    == true;

if (useContainer)
{
    ConfigureContainerMode(builder);
}
else
{
    ConfigureProjectMode(builder);
}

builder.Build().Run();

static void ConfigureContainerMode( IDistributedApplicationBuilder builder)
{
    var api = builder
        .AddDockerfile(
            "api",
            contextPath: "../..",
            dockerfilePath: "Dockerfile")
        .WithHttpEndpoint(
            targetPort: 8080,
            port: 51870,
            name: "http")
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
        .WaitFor(api)
        .WithEnvironment(
            "ApiBaseUrl",
            api.GetEndpoint("http"))
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

static void ConfigureProjectMode( IDistributedApplicationBuilder builder)
{
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
