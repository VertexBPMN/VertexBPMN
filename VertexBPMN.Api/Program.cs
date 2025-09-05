using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using Polly;
using VertexBPMN.Api.Debugging;
using VertexBPMN.Api.Migration;
using VertexBPMN.Api.ML;
using VertexBPMN.Api.Plugins;
using VertexBPMN.Api.Security;
using VertexBPMN.Api.Services;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Extensions;
using VertexBPMN.Core.Infrastructure;
using VertexBPMN.Core.Messaging;
using VertexBPMN.Core.Services;
using VertexBPMN.Persistence.Repositories;
using VertexBPMN.Persistence.Services;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IMultiInstanceExecutionRepository, VertexBPMN.Persistence.Repositories.Impl.MultiInstanceExecutionRepository>();
builder.Services.AddScoped<IProcessMigrationService>(sp =>
	new ProcessMigrationService(
		sp.GetRequiredService<IRuntimeService>(),
		sp.GetRequiredService<IHistoryService>()
	)
);

builder.Services.AddServiceTaskHandlers();
builder.Services.AddVertexBPMNTelemetry();
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
	builder.Services.AddOAuth2Authentication();
	// Register BpmnDbContext for all BPMN persistence with SQLite
	builder.Services.AddDbContext<VertexBPMN.Persistence.BpmnDbContext>(options =>
		options.UseSqlite("Data Source=vertexbpmn.db"));
	// Register persistence-based services
	builder.Services.AddScoped<IProcessDefinitionRepository, VertexBPMN.Persistence.Repositories.Impl.ProcessDefinitionRepository>();
	builder.Services.AddScoped<IProcessInstanceRepository, VertexBPMN.Persistence.Repositories.Impl.ProcessInstanceRepository>();
	builder.Services.AddScoped<ITaskRepository, VertexBPMN.Persistence.Repositories.Impl.TaskRepository>();
	builder.Services.AddScoped<IHistoryEventRepository, VertexBPMN.Persistence.Repositories.Impl.HistoryEventRepository>();
	builder.Services.AddScoped<VertexBPMN.Persistence.Repositories.IJobRepository, VertexBPMN.Persistence.Repositories.Impl.JobRepository>();
	builder.Services.AddScoped<IRepositoryService, VertexBPMN.Persistence.Services.RepositoryService>();
	// Register Core IJobRepository abstraction to persistence implementation
	builder.Services.AddScoped<VertexBPMN.Core.Services.IJobRepository, VertexBPMN.Persistence.Repositories.Impl.JobRepository>();
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
	builder.Services.AddScoped<IRuntimeService, VertexBPMN.Persistence.Services.RuntimeService>();
	builder.Services.AddScoped<ITaskService, VertexBPMN.Persistence.Services.TaskService>();
	builder.Services.AddScoped<IHistoryService, VertexBPMN.Persistence.Services.HistoryService>();
	builder.Services.AddScoped<IIncidentService, IncidentService>();
	// Register JobExecutor as background service
	builder.Services.AddHostedService<VertexBPMN.Core.JobExecutor.JobExecutorService>();
	builder.Services.AddEndpointsApiExplorer();
	builder.Services.AddScoped<IManagementService, ManagementService>();
	builder.Services.AddSingleton<IIdentityService, VertexBPMN.Core.Services.IdentityService>();
	builder.Services.AddSingleton<IDecisionService, DecisionService>();

	// Register SimulationService
	builder.Services.AddScoped<ISimulationService, SimulationService>();

	builder.Services.AddScoped<IPredictiveAnalyticsService, MLPredictiveAnalyticsService>();
	builder.Services.AddScoped<ILiveProcessMigrationService, LiveProcessMigrationService>();
	builder.Services.AddScoped<IVisualDebuggingService, VisualDebuggingService>();
	builder.Services.AddSingleton<IPluginManager, PluginManager>();

	// Olympic-level Enterprise Scalability: SignalR real-time monitoring
	builder.Services.AddSignalR();
    builder.Services.AddSingleton<ServiceTaskRegistry>();
    builder.Services.AddSingleton<IMessageDispatcher, InMemoryMessageDispatcher>();
	builder.Services.AddSingleton<IAiDecisionService, FakeAiDecisionService>();
	builder.Services.AddHttpClient<IAiDecisionService, XAiDecisionService>();
    builder.Services.AddHttpClient<McpServiceTaskHandler>();
    builder.Services.AddSingleton<IProcessInstanceStore, InMemoryProcessInstanceStore>();
	builder.Services.AddSingleton<IDmnEngine, DmnEngine>();
	builder.Services.AddSingleton<IDmnParser, DmnParser>();
	builder.Services.AddSingleton<ICmmnParser, CmmnParser>();
	builder.Services.AddSingleton<IBpmnParser, BpmnParser>();
	builder.Services.AddSingleton<IDistributedTokenEngine,DistributedTokenEngine>();
	builder.Services.AddSingleton<VertexBPMN.Api.Controllers.ILoadBalancingService, VertexBPMN.Api.Controllers.LoadBalancingService>();
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
				var bpmnContext = services.GetRequiredService<VertexBPMN.Persistence.BpmnDbContext>();
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

		//app.UseRouting();
		//app.UseEndpoints(endpoints =>
		//{
		//	endpoints.MapGrpcService<VertexBPMNMCPService>();
		//	endpoints.MapGrpcService<VertexBPMNService>();
		//	endpoints.MapVertexBPMNApi(app.ApplicationServices.GetRequiredService<IDistributedTokenEngine>());
		//}); endpoints.MapVertexBPMNApi(app.ApplicationServices.GetRequiredService<IDistributedTokenEngine>());
        //});
// Map Prometheus metrics endpoint
// Prometheus-Scraping-Endpoint entfernt

app.Run();


		namespace VertexBPMN.Api
		{
			public partial class Program { }
		}

