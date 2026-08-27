using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using VertexBPMN.Api;
using VertexBPMN.Api.Config;
using VertexBPMN.Api.Debug;
using VertexBPMN.Api.Features;
using VertexBPMN.Api.Health;
using VertexBPMN.Api.Hubs;
using VertexBPMN.Api.Mcp;
using VertexBPMN.Api.Middleware;
using VertexBPMN.Api.Operational;
using VertexBPMN.Api.Plugins;
using VertexBPMN.Api.Security;
using VertexBPMN.Api.Services;
using VertexBPMN.Application;
using VertexBPMN.Application.Configuration;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Engine;
using VertexBPMN.Infrastructure;
using VertexBPMN.Infrastructure.Config;
using VertexBPMN.Infrastructure.Notifications;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Repositories;
using VertexBPMN.Infrastructure.Persistence.Services;
using System.Security.Cryptography;
using SendGrid;


var builder = WebApplication.CreateBuilder(args);
// Bind module toggles
builder.Services.Configure<ModuleOptions>(builder.Configuration.GetSection("Modules"));
var moduleOptions = new ModuleOptions();
builder.Configuration.GetSection("Modules").Bind(moduleOptions);
var advancedFeatureOptions = new AdvancedFeatureOptions();
builder.Configuration.GetSection(AdvancedFeatureOptions.SectionName).Bind(advancedFeatureOptions);
var dependencyOptions = new DependencyOptions();
builder.Configuration.GetSection("Dependencies").Bind(dependencyOptions);

// Resolve operational mode
var opMode = builder.Environment.ResolveOperationalMode(builder.Configuration);

if (opMode is OperationalMode.Production or OperationalMode.Stage &&
	advancedFeatureOptions.LiveProcessMigration)
{
	throw new InvalidOperationException(
		"AdvancedFeatures execution flags cannot be enabled in Production or Stage until the corresponding feature has passed its qualification gate.");
}

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
	builder.Services.AddScoped<IProcessMiningEventSink>(sp => new VertexBPMN.Application.Messaging.WebhookEventSink(
		sp.GetRequiredService<PersistentProcessMiningEventSink>(),
		sp.GetRequiredService<IHttpClientFactory>(),
		builder.Configuration,
		sp.GetRequiredService<ILogger<VertexBPMN.Application.Messaging.WebhookEventSink>>()));
	builder.Services.AddScoped<VertexBPMN.Domain.Interfaces.IIdentityService, VertexBPMN.Infrastructure.Persistence.Services.PersistentIdentityService>();
	builder.Services.AddEngineServices(builder.Configuration);
}

builder.Services.AddApiServices(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
	var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 120);
	var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);
	var queueLimit = builder.Configuration.GetValue("RateLimiting:QueueLimit", 0);

	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
		RateLimitPartition.GetFixedWindowLimiter(
			context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
			_ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = permitLimit,
				Window = TimeSpan.FromSeconds(windowSeconds),
				QueueLimit = queueLimit,
				AutoReplenishment = true
			}));
});

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
builder.Services.AddHttpContextAccessor();

// Authentication is disabled only in the dedicated test host, where tests install
// an explicit test scheme. All other modes use the single production security setup.
if (opMode != OperationalMode.Test)
{
	builder.Services.AddProductionSecurity(builder.Configuration);
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks()
	.AddCheck("liveness", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
	.AddCheck<ServiceDependenciesHealthCheck>(
		"service_dependencies",
		failureStatus: HealthStatus.Unhealthy,
		tags: new[] { "ready" })
	.AddCheck<OperationalReadinessHealthCheck>(
		"operational_readiness",
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
		// Nested request records in different controllers may share a short name
		// (for example DeployRequest). Full names keep the generated OpenAPI
		// component identifiers unique and therefore make snapshot generation stable.
		options.CustomSchemaIds(type => type.FullName!.Replace('+', '.'));

		// Add JWT Bearer security definition
		var securityScheme = new Microsoft.OpenApi.OpenApiSecurityScheme
		{
			Description = "JWT Authorization header using the Bearer scheme. Example: 'Authorization: Bearer {token}'",
			Name = "Authorization",
			In = Microsoft.OpenApi.ParameterLocation.Header,
			Type = Microsoft.OpenApi.SecuritySchemeType.Http,
			Scheme = "bearer",
			BearerFormat = "JWT"
		};
		options.AddSecurityDefinition("Bearer", securityScheme);
		var securityRef = new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", null);
		options.AddSecurityRequirement((_) => new Microsoft.OpenApi.OpenApiSecurityRequirement
		{
			{ securityRef, new List<string>() }
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

var migrateOnly = args.Any(argument =>
	string.Equals(argument, "--migrate-only", StringComparison.OrdinalIgnoreCase));
if (migrateOnly)
{
	await DatabaseMigrationCoordinator.ApplyAsync(app.Services);
	return;
}

if (opMode is OperationalMode.Production or OperationalMode.Stage)
{
	if (builder.Configuration.GetValue<bool>("Runtime:Scripts:Enabled"))
		throw new InvalidOperationException("In-process BPMN scripts are forbidden in Production and Stage.");

	using var validationScope = app.Services.CreateScope();
	var dispatcher = validationScope.ServiceProvider.GetRequiredService<IMessageDispatcher>();
	var workerManager = validationScope.ServiceProvider.GetRequiredService<IWorkerNodeManager>();
	var miningSink = validationScope.ServiceProvider.GetRequiredService<IProcessMiningEventSink>();
	var productionDependencies = new object?[]
	{
		dispatcher,
		workerManager,
		miningSink,
		validationScope.ServiceProvider.GetService<IAiDecisionService>(),
		validationScope.ServiceProvider.GetService<ISendGridClient>()
	};
	var forbiddenImplementations = productionDependencies
		.Where(dependency => dependency is not null)
		.Select(dependency => dependency!.GetType())
		.Where(type => type.Name.Contains("Fake", StringComparison.OrdinalIgnoreCase)
	                || type.Name.Contains("InMemory", StringComparison.OrdinalIgnoreCase)
	                || type.Name.Contains("NoOp", StringComparison.OrdinalIgnoreCase))
	 .Select(type => type.FullName)
	 .ToArray();
	if (forbiddenImplementations.Length > 0)
		throw new InvalidOperationException(
			$"Production dependency validation rejected: {string.Join(", ", forbiddenImplementations)}.");
}

// Production pods never race each other while applying schemas. A dedicated
// migration job invokes --migrate-only; regular pods only verify schema state.
var applyMigrationsOnStartup = builder.Configuration.GetValue<bool?>("Database:ApplyMigrationsOnStartup")
	?? opMode is not (OperationalMode.Production or OperationalMode.Stage);
if (applyMigrationsOnStartup)
	await DatabaseMigrationCoordinator.ApplyAsync(app.Services);
else
	await DatabaseMigrationCoordinator.EnsureCurrentAsync(app.Services);

// Plugin system gating
var enablePlugins = moduleOptions.Plugins && dependencyOptions.Plugins.Enabled && opMode != OperationalMode.Test;
if (enablePlugins)
{
	if (opMode is OperationalMode.Production or OperationalMode.Stage
	    && dependencyOptions.Plugins.Files.Count == 0)
		throw new InvalidOperationException("Production plug-ins require an explicit Dependencies:Plugins:Files allowlist.");

	var pluginsDir = Path.IsPathRooted(dependencyOptions.Plugins.Directory)
		? dependencyOptions.Plugins.Directory
		: Path.Combine(AppContext.BaseDirectory, dependencyOptions.Plugins.Directory);
	if (!Directory.Exists(pluginsDir))
	{
		Directory.CreateDirectory(pluginsDir);
	}
	// Load plugins from the "plugins" directory
	using (var pluginScope = app.Services.CreateScope())
	{
		var pluginManager = pluginScope.ServiceProvider.GetRequiredService<IPluginManager>();
		var logger = pluginScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PluginLoader");
		var pluginFiles = dependencyOptions.Plugins.Files.Count == 0
			? Directory.GetFiles(pluginsDir, "*.dll", SearchOption.TopDirectoryOnly)
			: dependencyOptions.Plugins.Files.Select(file => Path.IsPathRooted(file) ? file : Path.Combine(pluginsDir, file));
		foreach (var pluginPath in pluginFiles)
		{
			if (opMode is OperationalMode.Production or OperationalMode.Stage)
			{
				var fileName = Path.GetFileName(pluginPath);
				if (!dependencyOptions.Plugins.Sha256.TryGetValue(fileName, out var expectedHash)
				    || string.IsNullOrWhiteSpace(expectedHash))
					throw new InvalidOperationException($"No SHA-256 allowlist entry exists for plug-in '{fileName}'.");
				await using var pluginStream = File.OpenRead(pluginPath);
				var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(pluginStream)).ToLowerInvariant();
				if (!CryptographicOperations.FixedTimeEquals(
						Convert.FromHexString(expectedHash), Convert.FromHexString(actualHash)))
					throw new InvalidOperationException($"SHA-256 verification failed for plug-in '{fileName}'.");
			}
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
if (opMode is OperationalMode.Production or OperationalMode.Stage)
{
	// Orchestrator probes originate inside the cluster and use the container's HTTP port.
	// Application traffic still redirects unless the trusted reverse proxy supplied HTTPS.
	app.UseWhen(
		context => !context.Request.Path.StartsWithSegments("/api/health")
		           && !context.Request.Path.StartsWithSegments("/api/ready"),
		branch => branch.UseHttpsRedirection());
}

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

// Health endpoints remain anonymous so orchestrators can probe a pod before authentication is available.
app.MapHealthChecks("/api/health").AllowAnonymous();
app.MapHealthChecks("/api/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
	Predicate = registration => registration.Tags.Contains("live")
}).AllowAnonymous();
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
}).AllowAnonymous();

if (opMode != OperationalMode.Test)
{
	app.UseCors("Production");
	app.UseRateLimiter();
	app.UseAuthentication();
}

app.UseMiddleware<CorrelationIdMiddleware>();

// Request/Response Logging Middleware
app.Use(async (context, next) =>
{
	var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RequestLogger");
	logger.LogInformation("HTTP {Method} {Path}", context.Request.Method, context.Request.Path);
	await next();
	logger.LogInformation("HTTP {Method} {Path} responded {StatusCode}", context.Request.Method, context.Request.Path, context.Response.StatusCode);
});

app.UseAuthorization();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<AuditLoggingMiddleware>();

//Enterprise Scalability: SignalR Hub mapping,Only map hubs when enabled
if (moduleOptions.SignalR)
{
	app.MapHub<ProcessMonitoringHub>("/api/monitoring-hub");
	app.MapHub<DebugHub>("/api/debug-hub");
}

app.MapControllers();

// The CMMN-only gRPC contracts remain discoverable when gRPC is enabled, but
// each operation enforces the advanced-feature gate before invoking the engine.
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
