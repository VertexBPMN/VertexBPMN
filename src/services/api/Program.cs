using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using Polly;
using VertexBPMN.Api.Config;
using VertexBPMN.Api.Debug;
using VertexBPMN.Api.Mcp;
using VertexBPMN.Api.Migration;
using VertexBPMN.Api.ML;
using VertexBPMN.Api.Plugins;
using VertexBPMN.Api.Security;
using VertexBPMN.Api.Services;
using VertexBPMN.Core;
using VertexBPMN.Core.Contracts;
using VertexBPMN.Core.Contracts.Repositories;
using VertexBPMN.Core.Engine;
using VertexBPMN.EngineServices;
using VertexBPMN.EngineServices.Extensions;
using VertexBPMN.EngineServices.Messaging;
using VertexBPMN.Persistence;
using VertexBPMN.Persistence.Repositories;
using VertexBPMN.Persistence.Services;
using ICachingService = VertexBPMN.Core.Contracts.ICachingService;
using IHealthMonitoringService = VertexBPMN.Core.Contracts.IHealthMonitoringService;
using IRateLimitingService = VertexBPMN.Core.Contracts.IRateLimitingService;
using IResilienceService = VertexBPMN.Core.Contracts.IResilienceService;
using RepositoryService = VertexBPMN.EngineServices.RepositoryService;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IMultiInstanceExecutionRepository, MultiInstanceExecutionRepository>();
builder.Services.AddScoped<IProcessMigrationService>(sp =>
	new ProcessMigrationService(
		sp.GetRequiredService<IRuntimeService>(),
		sp.GetRequiredService<IHistoryService>()
	)
);

builder.Services.AddServiceTaskHandlers();
OpenTelemetryConfig.AddVertexBPMNTelemetry(
    builder.Services,
    builder.Configuration
);
builder.Services.AddGrpc();
builder.Services.AddLogging();
// Register VisualDebuggerController dependencies
builder.Services.AddScoped<VertexBPMN.Api.Controllers.VisualDebuggerController>();
// Register SemanticValidationService for diagnostics
builder.Services.AddScoped<ISemanticValidationService, SemanticValidationService>();
// Register TenantDbContext (SQLite for demo, can be extended)
builder.Services.AddDbContext<TenantDbContext>(options =>
	options.UseSqlite("Data Source=tenants.db"));

// Register SimulationScenarioDbContext (SQLite)
builder.Services.AddDbContext<SimulationScenarioDbContext>(options =>
	options.UseSqlite("Data Source=simulationscenarios.db"));

// Register SimulationScenarioService
builder.Services.AddScoped<ISimulationScenarioService, SimulationScenarioService>();

	// Olympic-level Production-Grade Features: Security, Caching, Resilience, Rate Limiting, Health Monitoring
	builder.Services.AddMemoryCache(); // Required for ProductionCachingService
	builder.Services.AddSingleton<ICachingService, ProductionCachingService>();
	builder.Services.AddSingleton<IResilienceService, ProductionResilienceService>();
	builder.Services.AddSingleton<IRateLimitingService, ProductionRateLimitingService>();
	builder.Services.AddScoped<IHealthMonitoringService, ProductionHealthMonitoringService>();

	// Register TenantDbContext (SQLite for demo, can be extended)
	builder.Services.AddDbContext<TenantDbContext>(options =>
		options.UseSqlite("Data Source=tenants.db"));

	// Add services to the container.
	builder.Services.AddControllers();
	// Add OAuth2/OIDC authentication
	OAuth2AuthenticationExtensions.AddOAuth2Authentication(
		builder.Services,
		options => { /* configure JwtBearerOptions if needed */ }
	);
	// Register BpmnDbContext for all BPMN persistence with SQLite
	//builder.Services.AddDbContext<VertexBPMN.Persistence.BpmnDbContext>(options =>
	//	options.UseSqlite("Data Source=vertexbpmn.db"));
    builder.Services.AddBpmnPersistence(options =>
	{
		options.UseSqlite("Data Source=vertexbpmn.db");
		// Enable retry on failure for transient faults
		//options.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
	});
// Register persistence-based services
builder.Services.AddScoped<IProcessDefinitionRepository, ProcessDefinitionRepository>();
	builder.Services.AddScoped<IProcessInstanceRepository, ProcessInstanceRepository>();
	builder.Services.AddScoped<ITaskRepository, TaskRepository>();
	builder.Services.AddScoped<IHistoryEventRepository, HistoryEventRepository>();
	builder.Services.AddScoped<IJobRepository, JobRepository>();
	builder.Services.AddScoped<IRepositoryService, RepositoryService>();
	// Conditional registration for ProcessMiningEventDbContext (PostgreSQL or SQLite)
	var sqliteConn = builder.Configuration.GetConnectionString("ProcessMiningEventsSqlite");
	if (!string.IsNullOrWhiteSpace(sqliteConn))
	{
		builder.Services.AddDbContext<ProcessMiningEventDbContext>(options =>
			options.UseSqlite(sqliteConn));
	}
	else
	{
		builder.Services.AddDbContext<ProcessMiningEventDbContext>(options =>
			options.UseSqlite(builder.Configuration.GetConnectionString("ProcessMiningEvents")));
	}
	builder.Services.AddScoped<IProcessMiningEventSink, PersistentProcessMiningEventSink>();
	builder.Services.AddScoped<IRuntimeService, RuntimeService>();
	builder.Services.AddScoped<ITaskService, TaskService>();
	builder.Services.AddScoped<IHistoryService, HistoryService>();
	builder.Services.AddScoped<IIncidentService, IncidentService>();
	// Register JobExecutor as background service
	builder.Services.AddHostedService<JobExecutorService>();
	builder.Services.AddEndpointsApiExplorer();
	builder.Services.AddScoped<IManagementService, ManagementService>();
	builder.Services.AddSingleton<IIdentityService, IdentityService>();
	builder.Services.AddSingleton<IDecisionService, DecisionService>();

	// Register SimulationService
	builder.Services.AddScoped<ISimulationService, SimulationService>();

	builder.Services.AddScoped<IPredictiveAnalyticsService, MLPredictiveAnalyticsService>();
	builder.Services.AddScoped<ILiveProcessMigrationService, LiveProcessMigrationService>();
	builder.Services.AddScoped<IVisualDebuggingService, VisualDebuggingService>();
	builder.Services.AddSingleton<IPluginManager, PluginManager>();

	// Olympic-level Enterprise Scalability: SignalR real-time monitoring
	builder.Services.AddSignalR();
    builder.Services.AddSingleton<IServiceTaskRegistry,ServiceTaskRegistry>();
    builder.Services.AddSingleton<IMessageDispatcher, InMemoryMessageDispatcher>();
	builder.Services.AddSingleton<IAiDecisionService, FakeAiDecisionService>();
	builder.Services.AddHttpClient<IAiDecisionService, XAiDecisionService>();
	builder.Services.AddHttpClient<IMcpAgentService, McpAgentService>();
	builder.Services.AddSingleton<IDmnEngine, DmnEngine>();
	builder.Services.AddSingleton<IDmnParser, DmnParser>();
	builder.Services.AddSingleton<ICmmnParser, CmmnParser>();
	builder.Services.AddSingleton<IBpmnParser, BpmnParser>();
	builder.Services.AddSingleton<IDistributedTokenEngine,DistributedTokenEngine>();
	builder.Services.AddSingleton<ILoadBalancingService, LoadBalancingService>();
	builder.Services.AddSingleton<IWorkerNodeManager, WorkerNodeManager>();

		// Observability: HealthChecks, Logging, Metrics
	builder.Services.AddHealthChecks();
	builder.Logging.ClearProviders();
	builder.Logging.AddConsole();
	// OpenTelemetry metrics temporarily disabled due to .NET 9 API instability
		// OpenTelemetry und Prometheus-Registrierung temporär entfernt für Test-Kompatibilität
		builder.Services.AddSwaggerGen(options =>
		{
			// Add JWT Bearer security definition
			options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
			{
				Description = "JWT Authorization header using the Bearer scheme. Example: 'Authorization: Bearer {token}'",
				Name = "Authorization",
				In = Microsoft.OpenApi.Models.ParameterLocation.Header,
				Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
				Scheme = "bearer",
				BearerFormat = "JWT"
			});
			options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
			{
				{
					new Microsoft.OpenApi.Models.OpenApiSecurityScheme
					{
						Reference = new Microsoft.OpenApi.Models.OpenApiReference
						{
							Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
							Id = "Bearer"
						}
					},
					new string[] {}
				}
			});
			// Add Simulation API tag
			options.DocumentFilter<VertexBPMN.Api.SimulationTagDocumentFilter>();
		});
		// OpenAPI/Swagger: XML-Kommentare für Camunda-kompatible Endpunkte einbinden
		var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
		var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
		builder.Services.AddSwaggerGen(c =>
		{
			c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
		});
		// OpenAPI/Swagger: Camunda-kompatible Endpunkte dokumentieren

		var app = builder.Build();

		// Ensure databases are created
		using (var scope = app.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			try
			{
				var bpmnContext = services.GetRequiredService<BpmnDbContext>();
				await bpmnContext.Database.EnsureCreatedAsync();
				
				var tenantContext = services.GetRequiredService<TenantDbContext>();
				await tenantContext.Database.EnsureCreatedAsync();
				
				var simulationContext = services.GetRequiredService<SimulationScenarioDbContext>();
				await simulationContext.Database.EnsureCreatedAsync();
				
				var processMiningContext = services.GetRequiredService<ProcessMiningEventDbContext>();
				await processMiningContext.Database.EnsureCreatedAsync();
			}
			catch (Exception ex)
			{
				var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
				logger.LogError(ex, "An error occurred while creating the database");
			}
		}

	var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
	if (!Directory.Exists(pluginsDir))
	{
		Directory.CreateDirectory(pluginsDir);
	}
	// Load plugins from the "plugins" directory
	using (var pluginScope = app.Services.CreateScope())
	{
		var pluginManager = pluginScope.ServiceProvider.GetRequiredService<IPluginManager>();
		var logger = pluginScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PluginLoader");
		var pluginFiles = Directory.GetFiles(pluginsDir, "*.dll", SearchOption.TopDirectoryOnly);
		foreach (var pluginPath in pluginFiles)
		{
			var result = await pluginManager.LoadPluginAsync(pluginPath);
			if (!result.Success)
			{
				logger.LogError("Failed to load plugin {PluginPath}: {Error}", pluginPath, result.Error);
			}
			else
			{
				logger.LogInformation("Loaded plugin: {PluginPath}", pluginPath);
			}
		}
	}
		// Configure the HTTP request pipeline.

		// Always enable Swagger for easier API exploration in all environments
		app.UseSwagger();
		app.UseSwaggerUI();

		// Health endpoint
		app.MapHealthChecks("/api/health");

		// Enable authentication
		app.UseAuthentication();

		// Request/Response Logging Middleware
		app.Use(async (context, next) =>
		{
			var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RequestLogger");
			logger.LogInformation("HTTP {Method} {Path}", context.Request.Method, context.Request.Path);
			await next();
			logger.LogInformation("HTTP {Method} {Path} responded {StatusCode}", context.Request.Method, context.Request.Path, context.Response.StatusCode);
		});

		app.UseAuthorization();

		// Olympic-level Enterprise Scalability: SignalR Hub mapping
		app.MapHub<VertexBPMN.Api.Hubs.ProcessMonitoringHub>("/api/monitoring-hub");
		// Olympic-level Innovation Differentiators: Visual Debugging Hub
		app.MapHub<DebugHub>("/api/debug-hub");

		app.MapControllers();

        app.MapGrpcService<VertexBPMN.Api.Mcp.VertexBpmnServiceImpl>();
        app.MapGrpcService<VertexBPMN.Api.Mcp.VertexBpmnServiceImpl>();
		app.MapGet("/", () => "gRPC endpoint. Use a gRPC client.");

app.Run();

namespace VertexBPMN.Api
{
	public partial class Program { }
}

