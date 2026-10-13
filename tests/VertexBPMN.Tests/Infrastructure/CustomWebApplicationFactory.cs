using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VertexBPMN.Api.Features;
using VertexBPMN.Application;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Tests.Infrastructure.Seeding;

namespace VertexBPMN.Tests.Infrastructure;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public xUnit collection or fixture contract retained for framework activation and injection into public test constructors.")]
public class CustomWebApplicationFactory : WebApplicationFactory<VertexBPMN.Api.Program>, IAsyncLifetime
{
	private bool _initialized;
	private string _engineType = "Simple";
	private bool _backgroundJobsEnabled;
	private bool _cmmnExecutionEnabled;
	private bool _liveProcessMigrationEnabled;
	private readonly Lock _ownedConnectionsLock = new();
	private readonly List<SqliteConnection> _ownedConnections = [];
	private bool _ownedConnectionsDisposed;
	private readonly string _databaseId = Guid.NewGuid().ToString("N");
	private SharedSqliteDbFixture? _sharedFixture;
	private SqliteConnection? _persistentBpmnConnection;
	private string? _persistentBpmnConnectionString;
	private Action<IServiceCollection>? _configureTestServices;

	// Allow chaining in test constructors
	public CustomWebApplicationFactory WithSharedFixture(SharedSqliteDbFixture fixture)
	{
		_sharedFixture = fixture;
		return this;
	}

	public CustomWebApplicationFactory WithEngineType(string engineType)
	{
		_engineType = engineType;
		return this;
	}

	public CustomWebApplicationFactory WithBackgroundJobsEnabled()
	{
		_backgroundJobsEnabled = true;
		return this;
	}

	public CustomWebApplicationFactory WithCmmnExecutionEnabled()
	{
		_cmmnExecutionEnabled = true;
		_configureTestServices += services =>
			services.PostConfigure<AdvancedFeatureOptions>(options => options.CmmnExecution = true);
		return this;
	}

	public CustomWebApplicationFactory WithLiveProcessMigrationEnabled()
	{
		_liveProcessMigrationEnabled = true;
		_configureTestServices += services =>
			services.PostConfigure<AdvancedFeatureOptions>(options => options.LiveProcessMigration = true);
		return this;
	}

	public CustomWebApplicationFactory WithPersistentBpmnDatabase(SqliteConnection connection)
	{
		_persistentBpmnConnection = connection;
		return this;
	}

	public CustomWebApplicationFactory WithPersistentBpmnDatabase(string connectionString)
	{
		_persistentBpmnConnectionString = string.IsNullOrWhiteSpace(connectionString)
			? throw new ArgumentException("A SQLite connection string is required.", nameof(connectionString))
			: connectionString;
		return this;
	}

	public CustomWebApplicationFactory WithTestServices(Action<IServiceCollection> configure)
	{
		_configureTestServices += configure ?? throw new ArgumentNullException(nameof(configure));
		return this;
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Test");
		builder.ConfigureAppConfiguration((ctx, cfg) =>
		{
			var env = ctx.HostingEnvironment;
			cfg
				.AddJsonFile("appsettings.json", optional: true)
				.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
				.AddJsonFile("appsettings.Test.json", optional: true)
				.AddEnvironmentVariables()
				.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
				{
					["OperationalMode"] = "Test",
					["Database:ApplyMigrationsOnStartup"] = "true",
					["ProcessEngine:Type"] = _engineType,
					["Modules:Swagger"] = "false",
					["AdvancedFeatures:CmmnExecution"] = _cmmnExecutionEnabled.ToString(),
					["AdvancedFeatures:LiveProcessMigration"] = _liveProcessMigrationEnabled.ToString(),
					["PathBase"] = "/api"
				});
		});

		builder.ConfigureServices(ConfigureTestServices);
	}

	private void ConfigureTestServices(IServiceCollection services)
	{
		ConfigureTestAuthentication(services);
		services.RemoveAll<DbContextOptions<BpmnDbContext>>();
		services.RemoveAll<DbContextOptions<TenantDbContext>>();
		services.RemoveAll<DbContextOptions<SimulationScenarioDbContext>>();
		services.RemoveAll<DbContextOptions<ProcessMiningEventDbContext>>();
		services.RemoveAll<DbContextOptions<DecisionDbContext>>();
		services.AddDbContext<BpmnDbContext>(ConfigureBpmnTestDatabase);
		services.AddDbContext<TenantDbContext>(options => ConfigureAuxiliaryTestDatabase(options, "tenants"));
		services.AddDbContext<SimulationScenarioDbContext>(options => ConfigureAuxiliaryTestDatabase(options, "simulation"));
		services.AddDbContext<ProcessMiningEventDbContext>(options => ConfigureAuxiliaryTestDatabase(options, "procmining"));
		services.AddDbContext<DecisionDbContext>(options => ConfigureAuxiliaryTestDatabase(options, "decision"));
		services.AddSingleton<ITestDataSeeder, TenantAndUserSeeder>();
		services.AddSingleton<ITestDataSeeder, ProcessDefinitionSeeder>();
		services.AddSingleton<ITestDataSeeder, DecisionSeeder>();
		if (_backgroundJobsEnabled)
		{
			services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, JobExecutorService>());
		}
		_configureTestServices?.Invoke(services);
	}

	private static void ConfigureTestAuthentication(IServiceCollection services)
	{
		services.AddAuthentication("Test")
			.AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
		services.AddAuthorization(options =>
		{
			options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
			options.AddPolicy("ProcessManager", policy => policy.RequireRole("Admin", "ProcessManager"));
			options.AddPolicy("ReadOnly", policy => policy.RequireRole("Admin", "ProcessManager", "ReadOnly"));
			VertexBPMN.Api.Security.SecurityConfiguration.AddTenantReadOnlyPolicy(options);
		});
	}

	private void ConfigureBpmnTestDatabase(DbContextOptionsBuilder options)
	{
		if (_persistentBpmnConnectionString is not null)
		{
			options.UseSqlite(_persistentBpmnConnectionString);
		}
		else if (_persistentBpmnConnection is null && _sharedFixture is not null)
		{
			options.UseSqlite(_sharedFixture.Connection.ConnectionString);
		}
		else
		{
			options.UseSqlite(_persistentBpmnConnection ?? CreateAndTrack("bpmn"));
		}
		options.EnableSensitiveDataLogging().EnableDetailedErrors();
	}

	private void ConfigureAuxiliaryTestDatabase(DbContextOptionsBuilder options, string label)
	{
		if (_persistentBpmnConnectionString is null && _persistentBpmnConnection is null && _sharedFixture is not null)
		{
			options.UseSqlite(_sharedFixture.GetConnection(label).ConnectionString).EnableSensitiveDataLogging().EnableDetailedErrors();
			return;
		}

		// Persistent-BPMN profiles deliberately keep the auxiliary databases isolated,
		// even if a shared fixture was also supplied.
		var connection = CreateAndTrack(label);
		options.UseSqlite(connection).EnableSensitiveDataLogging().EnableDetailedErrors();
	}

	private SqliteConnection CreateAndTrack(string label)
	{
		// Separate In-Memory-DB: Data Source=:memory:
		// Wenn du mehrere offene Verbindungen brauchst, aber dieselbe DB teilen willst: Data Source=file:vertex_{label}?mode=memory&cache=shared
		var connString = $"Data Source=file:vertex_{_databaseId}_{label}?mode=memory&cache=shared";
		var conn = new SqliteConnection(connString);
		conn.Open();

		lock (_ownedConnectionsLock)
		{
			if (_ownedConnectionsDisposed)
			{
				conn.Dispose();
				throw new ObjectDisposedException(nameof(CustomWebApplicationFactory));
			}

			_ownedConnections.Add(conn);
		}

		return conn;
	}

	public async ValueTask InitializeAsync()
	{
		if (_initialized)
		{
			return;
		}

		// Force host creation
		_ = Services;

		using var scope = Services.CreateScope();
		var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomWebApplicationFactory>>();

		if (_sharedFixture is null)
		{
			// Only create schema if we own the connection
			await EnsureDbAsync<BpmnDbContext>(scope, logger);
			await EnsureDbAsync<TenantDbContext>(scope, logger);
			await EnsureDbAsync<SimulationScenarioDbContext>(scope, logger);
			await EnsureDbAsync<ProcessMiningEventDbContext>(scope, logger);
			await EnsureDbAsync<DecisionDbContext>(scope, logger);
		}
		else
		{
			// Validate required tables exist (fast fail)
			var required = new[] { "Tenants", "Users", "ProcessDefinitions" };
			foreach (var table in required)
			{
				if (!await TableExistsAsync(scope.ServiceProvider, table))
				{
					throw new InvalidOperationException($"Required table '{table}' not found in manual schema (check Sqlite.sql).");
				}
			}
		}
		await RunSeedersAsync(scope, logger);

		_initialized = true;
	}

	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();

		var ownedConnections = TakeOwnedConnections();
		await Task.WhenAll(ownedConnections.Select(connection => connection.DisposeAsync().AsTask()));
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (!disposing)
		{
			return;
		}

		foreach (var connection in TakeOwnedConnections())
		{
			connection.Dispose();
		}
	}

	private SqliteConnection[] TakeOwnedConnections()
	{
		SqliteConnection[] ownedConnections;
		lock (_ownedConnectionsLock)
		{
			if (_ownedConnectionsDisposed)
			{
				return [];
			}

			_ownedConnectionsDisposed = true;
			ownedConnections = [.. _ownedConnections];
			_ownedConnections.Clear();
		}

		return ownedConnections;
	}
	private static async Task<bool> TableExistsAsync(IServiceProvider sp, string table)
	{
		var conn = sp.GetRequiredService<SqliteConnection>();
		await using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name = $n";
		cmd.Parameters.AddWithValue("$n", table);
		var result = await cmd.ExecuteScalarAsync();
		return result is not null;
	}

	private static async Task EnsureDbAsync<T>(IServiceScope scope, ILogger logger) where T : DbContext
	{
		var ctx = scope.ServiceProvider.GetRequiredService<T>();
		var created = await ctx.Database.EnsureCreatedAsync();
		if (!created)
		{
			// Optional migration step if you use migrations
			try
			{ await ctx.Database.MigrateAsync(); }
			catch { /* ignore for pure EnsureCreated flow */ }
		}
		if (logger.IsEnabled(LogLevel.Debug))
		{
			logger.LogDebug("{DbContext} ready (Created={Created})", typeof(T).Name, created);
		}
	}

	private static async Task RunSeedersAsync(IServiceScope scope, ILogger logger, CancellationToken ct = default)
	{
		var seeders = scope.ServiceProvider
			.GetServices<ITestDataSeeder>()
			.OrderBy(s => s.Order)
			.ToList();

		foreach (var seeder in seeders)
		{
			try
			{
				if (logger.IsEnabled(LogLevel.Information))
				{
					logger.LogInformation("Running seeder {Seeder} (Order={Order})", seeder.GetType().Name, seeder.Order);
				}
				await seeder.SeedAsync(scope, ct);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Seeder {Seeder} failed", seeder.GetType().Name);
				throw;
			}
		}
	}

	public new HttpClient CreateClient()
	{
		InitializeAsync().AsTask().GetAwaiter().GetResult();
		return base.CreateClient();
	}

	public new HttpClient CreateClient(WebApplicationFactoryClientOptions options)
	{
		InitializeAsync().AsTask().GetAwaiter().GetResult();
		var client = base.CreateClient(options);
		return client;
	}

	public HttpClient CreateClient(ITestOutputHelper output)
	{
		InitializeAsync().AsTask().GetAwaiter().GetResult();
		var options = new WebApplicationFactoryClientOptions
		{
			BaseAddress = new Uri("http://localhost")
		};

		var client = base.CreateClient(options);
		client.Timeout = TimeSpan.FromSeconds(30);

		try
		{
			XunitLoggerProvider.Register(Services.GetRequiredService<ILoggerFactory>(), output);
		}
		catch
		{
			client.Dispose();
			throw;
		}
		return client;
	}
}
