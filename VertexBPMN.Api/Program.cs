using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using VertexBPMN.Persistence.Services;
using VertexBPMN.Persistence.Repositories;
using VertexBPMN.Api.Security;
using VertexBPMN.Api.Services;
using VertexBPMN.Api.ML;
using VertexBPMN.Api.Migration;
using VertexBPMN.Api.Debugging;
using VertexBPMN.Api.Plugins;
using Polly;
// ...existing top-level statements and code...
// Place this at the end of the file, after all top-level statements

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<VertexBPMN.Persistence.Repositories.IMultiInstanceExecutionRepository, VertexBPMN.Persistence.Repositories.Impl.MultiInstanceExecutionRepository>();
builder.Services.AddScoped<VertexBPMN.Core.Services.IProcessMigrationService>(sp =>
	new VertexBPMN.Core.Services.ProcessMigrationService(
		sp.GetRequiredService<VertexBPMN.Core.Services.IRuntimeService>(),
		sp.GetRequiredService<VertexBPMN.Core.Services.IHistoryService>()
	)
);
// Register VisualDebuggerController dependencies
builder.Services.AddScoped<VertexBPMN.Api.Controllers.VisualDebuggerController>();
// Register SemanticValidationService for diagnostics
builder.Services.AddScoped<VertexBPMN.Core.Services.ISemanticValidationService, VertexBPMN.Core.Services.SemanticValidationService>();
// Register TenantDbContext (SQLite for demo, can be extended)
builder.Services.AddDbContext<VertexBPMN.Persistence.Services.TenantDbContext>(options =>
	options.UseSqlite("Data Source=tenants.db"));

// Register SimulationScenarioDbContext (SQLite)
builder.Services.AddDbContext<VertexBPMN.Persistence.Services.SimulationScenarioDbContext>(options =>
	options.UseSqlite("Data Source=simulationscenarios.db"));

// Register SimulationScenarioService
builder.Services.AddScoped<VertexBPMN.Core.Services.ISimulationScenarioService, VertexBPMN.Persistence.Services.SimulationScenarioService>();

		// Olympic-level Production-Grade Features: Security, Caching, Resilience, Rate Limiting, Health Monitoring
		builder.Services.AddMemoryCache(); // Required for ProductionCachingService
		builder.Services.AddSingleton<ICachingService, ProductionCachingService>();
		builder.Services.AddSingleton<IResilienceService, ProductionResilienceService>();
		builder.Services.AddSingleton<IRateLimitingService, ProductionRateLimitingService>();
		builder.Services.AddScoped<IHealthMonitoringService, ProductionHealthMonitoringService>();

		// Register TenantDbContext (SQLite for demo, can be extended)
		builder.Services.AddDbContext<VertexBPMN.Persistence.Services.TenantDbContext>(options =>
			options.UseSqlite("Data Source=tenants.db"));

		// Add services to the container.
		builder.Services.AddControllers();
		// Add OAuth2/OIDC authentication
		builder.Services.AddOAuth2Authentication();
		// Register BpmnDbContext for all BPMN persistence with SQLite
		builder.Services.AddDbContext<VertexBPMN.Persistence.BpmnDbContext>(options =>
			options.UseSqlite("Data Source=vertexbpmn.db"));
		// Register persistence-based services
		builder.Services.AddScoped<VertexBPMN.Persistence.Repositories.IProcessDefinitionRepository, VertexBPMN.Persistence.Repositories.Impl.ProcessDefinitionRepository>();
		builder.Services.AddScoped<VertexBPMN.Persistence.Repositories.IProcessInstanceRepository, VertexBPMN.Persistence.Repositories.Impl.ProcessInstanceRepository>();
		builder.Services.AddScoped<VertexBPMN.Persistence.Repositories.ITaskRepository, VertexBPMN.Persistence.Repositories.Impl.TaskRepository>();
		builder.Services.AddScoped<VertexBPMN.Persistence.Repositories.IHistoryEventRepository, VertexBPMN.Persistence.Repositories.Impl.HistoryEventRepository>();
		builder.Services.AddScoped<VertexBPMN.Persistence.Repositories.IJobRepository, VertexBPMN.Persistence.Repositories.Impl.JobRepository>();
		builder.Services.AddScoped<VertexBPMN.Core.Services.IRepositoryService, VertexBPMN.Persistence.Services.RepositoryService>();
		// Register Core IJobRepository abstraction to persistence implementation
		builder.Services.AddScoped<VertexBPMN.Core.Services.IJobRepository, VertexBPMN.Persistence.Repositories.Impl.JobRepository>();
		// Conditional registration for ProcessMiningEventDbContext (PostgreSQL or SQLite)
		var sqliteConn = builder.Configuration.GetConnectionString("ProcessMiningEventsSqlite");
		if (!string.IsNullOrWhiteSpace(sqliteConn))
		{
			builder.Services.AddDbContext<VertexBPMN.Persistence.Services.ProcessMiningEventDbContext>(options =>
				options.UseSqlite(sqliteConn));
		}
		else
		{
			builder.Services.AddDbContext<VertexBPMN.Persistence.Services.ProcessMiningEventDbContext>(options =>
				options.UseSqlite(builder.Configuration.GetConnectionString("ProcessMiningEvents")));
		}
		builder.Services.AddScoped<VertexBPMN.Core.Services.IProcessMiningEventSink, VertexBPMN.Persistence.Services.PersistentProcessMiningEventSink>();
		builder.Services.AddScoped<VertexBPMN.Core.Services.IRuntimeService, VertexBPMN.Persistence.Services.RuntimeService>();
		builder.Services.AddScoped<VertexBPMN.Core.Services.ITaskService, VertexBPMN.Persistence.Services.TaskService>();
		builder.Services.AddScoped<VertexBPMN.Core.Services.IHistoryService, VertexBPMN.Persistence.Services.HistoryService>();
		builder.Services.AddScoped<VertexBPMN.Core.Services.IIncidentService, VertexBPMN.Persistence.Services.IncidentService>();
		// Register JobExecutor as background service
		builder.Services.AddHostedService<VertexBPMN.Core.JobExecutor.JobExecutorService>();
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddScoped<VertexBPMN.Core.Services.IManagementService, VertexBPMN.Core.Services.ManagementService>();
		builder.Services.AddSingleton<VertexBPMN.Core.Services.IIdentityService, VertexBPMN.Core.Services.IdentityService>();
		builder.Services.AddSingleton<VertexBPMN.Core.Services.IDecisionService, VertexBPMN.Core.Services.DecisionService>();

	// Register SimulationService
	builder.Services.AddScoped<VertexBPMN.Core.Services.ISimulationService, VertexBPMN.Core.Services.SimulationService>();

	// Olympic-level Innovation Differentiators - The Future of BPMN
	builder.Services.AddScoped<IPredictiveAnalyticsService, MLPredictiveAnalyticsService>();
	builder.Services.AddScoped<ILiveProcessMigrationService, LiveProcessMigrationService>();
	builder.Services.AddScoped<IVisualDebuggingService, VisualDebuggingService>();
	builder.Services.AddSingleton<IPluginManager, PluginManager>();

		// Olympic-level Enterprise Scalability: SignalR real-time monitoring
		builder.Services.AddSignalR();

		// Olympic-level Enterprise Scalability: Distributed processing services
		builder.Services.AddSingleton<VertexBPMN.Core.Engine.IDistributedTokenEngine, VertexBPMN.Core.Engine.DistributedTokenEngine>();
		builder.Services.AddSingleton<VertexBPMN.Api.Controllers.ILoadBalancingService, VertexBPMN.Api.Controllers.LoadBalancingService>();
		builder.Services.AddSingleton<VertexBPMN.Core.Engine.IWorkerNodeManager, VertexBPMN.Core.Engine.WorkerNodeManager>();

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
		var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
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
				
				var tenantContext = services.GetRequiredService<VertexBPMN.Persistence.Services.TenantDbContext>();
				await tenantContext.Database.EnsureCreatedAsync();
				
				var simulationContext = services.GetRequiredService<VertexBPMN.Persistence.Services.SimulationScenarioDbContext>();
				await simulationContext.Database.EnsureCreatedAsync();
				
				var processMiningContext = services.GetRequiredService<VertexBPMN.Persistence.Services.ProcessMiningEventDbContext>();
				await processMiningContext.Database.EnsureCreatedAsync();
			}
			catch (Exception ex)
			{
				var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
				logger.LogError(ex, "An error occurred while creating the database");
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
		app.MapHub<VertexBPMN.Api.Debugging.DebugHub>("/api/debug-hub");

		app.MapControllers();

		// Map Prometheus metrics endpoint
	// Prometheus-Scraping-Endpoint entfernt

		app.Run();
public partial class Program { }
