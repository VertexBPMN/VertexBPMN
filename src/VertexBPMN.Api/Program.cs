using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.RegularExpressions;
using VertexBPMN.Api;
using VertexBPMN.Api.Config;
using VertexBPMN.Api.Debug;
using VertexBPMN.Api.Health;
using VertexBPMN.Api.Hubs;
using VertexBPMN.Api.Mcp;
using VertexBPMN.Api.Plugins;
using VertexBPMN.Api.Security;
using VertexBPMN.Api.Services;
using VertexBPMN.Application;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Engine;
using VertexBPMN.Infrastructure;
using VertexBPMN.Infrastructure.Config;
using VertexBPMN.Infrastructure.Notifications;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Repositories;
using VertexBPMN.Infrastructure.Persistence.Services;


var builder = WebApplication.CreateBuilder(args);
// Bind module toggles
builder.Services.Configure<ModuleOptions>(builder.Configuration.GetSection("Modules"));
var moduleOptions = new ModuleOptions();
builder.Configuration.GetSection("Modules").Bind(moduleOptions);

// Resolve operational mode
var opMode = builder.Environment.ResolveOperationalMode(builder.Configuration);

builder.Services.AddScoped<IMultiInstanceExecutionRepository, MultiInstanceExecutionRepository>();
builder.Services.AddScoped<IProcessMigrationService>(sp =>
	new ProcessMigrationService(
		sp.GetRequiredService<IRuntimeService>(),
		sp.GetRequiredService<IHistoryService>()
	)
);

builder.Services.AddLogging();
builder.Logging.AddFile("Logs/api-log.txt"); // mit z. B. Serilog oder Drittanbieter

// Telemetry conditional
if (moduleOptions.Telemetry && opMode is not OperationalMode.Test)
{
	builder.AddVertexBPMNTelemetry();
}

// Core service registrations(grouped)
if (moduleOptions.Persistence)
{
	builder.Services.AddAllEngineDbContexts(builder.Configuration);
}

if (moduleOptions.Engine)
{
	builder.Services.AddBpmnPersistenceServices(builder.Configuration);
	builder.Services.AddApplicationServices(builder.Configuration);
	builder.Services.AddEngineServices(builder.Configuration);
}

builder.Services.AddApiServices(builder.Configuration);

// Background hosted services control (example: disable job executor in Test)
if (opMode == OperationalMode.Test || !moduleOptions.BackgroundJobs)
{
	// Optionally remove specific hosted services after registration if API-level toggles needed.
	// Example pattern (pseudo):
	 builder.Services.Remove(new ServiceDescriptor(typeof(IHostedService), typeof(JobExecutorService), ServiceLifetime.Singleton));
}

// gRPC & SignalR
builder.Services.AddWhen(moduleOptions.Grpc, s => s.AddGrpc());
builder.Services.AddWhen(moduleOptions.SignalR, s => s.AddSignalR());


// Add services to the container.
builder.Services.AddControllers();

// Authentication (could be disabled for Test if desired)
if (opMode != OperationalMode.Test)
{
	OAuth2AuthenticationExtensions.AddOAuth2Authentication(
		builder.Services,
		options => { });
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks()
	.AddCheck<ServiceDependenciesHealthCheck>(
		"service_dependencies",
		failureStatus: HealthStatus.Unhealthy,
		tags: new[] { "ready" });

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (moduleOptions.SignalR)
{
	builder.Services.AddSignalR();
	builder.Services.AddScoped<INotificationService, SignalRNotificationService>();
}
// Swagger configuration - consolidated
if (moduleOptions.Swagger || opMode is OperationalMode.Development or OperationalMode.Stage)
{
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
				Array.Empty<string>()
			}
		});
		
		// Add Simulation API tag
		options.DocumentFilter<VertexBPMN.Api.SimulationTagDocumentFilter>();
		
		// Include XML comments if available
		var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
		var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
		if (File.Exists(xmlPath))
		{
			options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
		}
	});
}

if (opMode == OperationalMode.Production && moduleOptions.Emails)
{
	builder.Services.AddOptionalEmailNotifications(builder.Configuration);
}

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

		var decisionContext = services.GetRequiredService<DecisionDbContext>();
		await decisionContext.Database.EnsureCreatedAsync();
	}
	catch (Exception ex)
	{
		var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
		logger.LogError(ex, "An error occurred while creating the database");
	}
}

// Plugin system gating
var enablePlugins = moduleOptions.Plugins && opMode != OperationalMode.Test;
if (enablePlugins)
{
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
}

// Configure the HTTP request pipeline.
var pathBase = builder.Configuration["PathBase"]
			  ?? builder.Configuration["ASPNETCORE_PATHBASE"]
			  ?? Environment.GetEnvironmentVariable("ASPNETCORE_PATHBASE");

if (!string.IsNullOrWhiteSpace(pathBase))
{
	if (!pathBase.StartsWith('/'))
		pathBase = "/" + pathBase.Trim();
	pathBase = pathBase.TrimEnd('/');

	app.UsePathBase(pathBase);

	app.Use(async (ctx, next) =>
	{
		if (ctx.Request.Path == "/" && !string.IsNullOrEmpty(pathBase))
		{
			ctx.Response.Redirect(pathBase + "/");
			return;
		}
		await next();
	});
}

// Conditional Swagger for easier API exploration in all environments
if (moduleOptions.Swagger || opMode is OperationalMode.Development or OperationalMode.Stage)
{
	app.UseSwagger();
	app.UseSwaggerUI(c => c.RoutePrefix = "swagger");
}

// Health endpoint
app.MapHealthChecks("/api/health");
// Readiness – only include checks tagged "ready"
app.MapHealthChecks("/api/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
	Predicate = r => r.Tags.Contains("ready"),
	ResponseWriter = async (ctx, report) =>
	{
		ctx.Response.ContentType = "application/json; charset=utf-8";
		var payload = new
		{
			status = report.Status.ToString(),
			results = report.Entries.ToDictionary(
				e => e.Key,
				e => new
				{
					status = e.Value.Status.ToString(),
					description = e.Value.Description,
					data = e.Value.Data
				})
		};
		await System.Text.Json.JsonSerializer.SerializeAsync(
			ctx.Response.Body,
			payload,
			new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
	}
});

if (opMode != OperationalMode.Test)
{
	app.UseAuthentication();
}

// Request/Response Logging Middleware
app.Use(async (context, next) =>
{
	var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RequestLogger");
	logger.LogInformation("HTTP {Method} {Path}", context.Request.Method, context.Request.Path);
	await next();
	logger.LogInformation("HTTP {Method} {Path} responded {StatusCode}", context.Request.Method, context.Request.Path, context.Response.StatusCode);
});

app.UseAuthorization();

//Enterprise Scalability: SignalR Hub mapping,Only map hubs when enabled
if (moduleOptions.SignalR)
{
	app.MapHub<ProcessMonitoringHub>("/api/monitoring-hub");
	app.MapHub<DebugHub>("/api/debug-hub");
}

app.MapControllers();

// gRPC when enabled
if (moduleOptions.Grpc)
{
	app.MapGrpcService<VertexBpmnServiceImpl>();
	app.MapGrpcService<VertexBpmnMcpServiceImpl>();
}
app.MapGet("/", () => $"VertexBPMN API ({opMode})");

app.Run();

namespace VertexBPMN.Api
{
public partial class Program { }
}
