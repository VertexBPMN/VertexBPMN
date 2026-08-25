using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VertexBPMN.Application;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Tests.Infrastructure.Seeding;

namespace VertexBPMN.Tests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<VertexBPMN.Api.Program>, IAsyncLifetime
{
    private bool _initialized;
    private string _engineType = "Simple";
    private bool _backgroundJobsEnabled;
    private readonly List<SqliteConnection> _ownedConnections = new();
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
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OperationalMode"] = "Test",
                    ["ProcessEngine:Type"] = _engineType,
                    ["Modules:Swagger"] = "false",
                    ["PathBase"] = "/api"
                });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
                options.AddPolicy("ProcessManager", policy => policy.RequireRole("Admin", "ProcessManager"));
                options.AddPolicy("ReadOnly", policy => policy.RequireRole("Admin", "ProcessManager", "ReadOnly"));
            });

            // Remove existing DbContext options so we can inject unified connection
            services.RemoveAll<DbContextOptions<BpmnDbContext>>();
            services.RemoveAll<DbContextOptions<TenantDbContext>>();
            services.RemoveAll<DbContextOptions<SimulationScenarioDbContext>>();
            services.RemoveAll<DbContextOptions<ProcessMiningEventDbContext>>();
            services.RemoveAll<DbContextOptions<DecisionDbContext>>();

            // Decide which connection to use:
            if (_persistentBpmnConnectionString is not null)
            {
                services.AddDbContext<BpmnDbContext>(o => o.UseSqlite(_persistentBpmnConnectionString)
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<TenantDbContext>(o => o.UseSqlite(CreateAndTrack(":memory:", "tenants"))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<SimulationScenarioDbContext>(o => o.UseSqlite(CreateAndTrack(":memory:", "simulation"))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<ProcessMiningEventDbContext>(o => o.UseSqlite(CreateAndTrack(":memory:", "procmining"))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<DecisionDbContext>(o => o.UseSqlite(CreateAndTrack(":memory:", "decision"))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
            }
            else if (_persistentBpmnConnection is not null)
            {
                services.AddDbContext<BpmnDbContext>(o => o.UseSqlite(_persistentBpmnConnection)
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<TenantDbContext>(o => o.UseSqlite(CreateAndTrack(":memory:", "tenants"))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<SimulationScenarioDbContext>(o => o.UseSqlite(CreateAndTrack(":memory:", "simulation"))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<ProcessMiningEventDbContext>(o => o.UseSqlite(CreateAndTrack(":memory:", "procmining"))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<DecisionDbContext>(o => o.UseSqlite(CreateAndTrack(":memory:", "decision"))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
            }
            else if (_sharedFixture is not null)
            {
                services.AddDbContext<BpmnDbContext>(o => o.UseSqlite(_sharedFixture.Connection)
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<TenantDbContext>(o => o.UseSqlite(_sharedFixture.Connection)
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<SimulationScenarioDbContext>(o => o.UseSqlite(_sharedFixture.Connection)
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<ProcessMiningEventDbContext>(o => o.UseSqlite(_sharedFixture.Connection)
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
                services.AddDbContext<DecisionDbContext>(o => o.UseSqlite(_sharedFixture.Connection)
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
            }
            else
            {
                services.AddDbContext<BpmnDbContext>(o =>
                {
                    var c = CreateAndTrack(":memory:", "bpmn");
                    o.UseSqlite(c).EnableSensitiveDataLogging().EnableDetailedErrors();
                });
                services.AddDbContext<TenantDbContext>(o =>
                {
                    var c = CreateAndTrack(":memory:", "tenants");
                    o.UseSqlite(c).EnableSensitiveDataLogging().EnableDetailedErrors();
                });
                services.AddDbContext<SimulationScenarioDbContext>(o =>
                {
                    var c = CreateAndTrack(":memory:", "simulation");
                    o.UseSqlite(c).EnableSensitiveDataLogging().EnableDetailedErrors();
                });
                services.AddDbContext<ProcessMiningEventDbContext>(o =>
                {
                    var c = CreateAndTrack(":memory:", "procmining");
                    o.UseSqlite(c).EnableSensitiveDataLogging().EnableDetailedErrors();
                });
                services.AddDbContext<DecisionDbContext>(o =>
                {
                    var c = CreateAndTrack(":memory:", "decision");
                    o.UseSqlite(c).EnableSensitiveDataLogging().EnableDetailedErrors();
                });
            }

            //void Configure(DbContextOptionsBuilder o, SqliteConnection c) =>
            //    o.UseSqlite(c)
            //     .EnableSensitiveDataLogging()
            //     .EnableDetailedErrors();

            //services.AddDbContext<BpmnDbContext>((sp, o) => Configure(o, sp.GetRequiredService<SqliteConnection>()));
            //services.AddDbContext<TenantDbContext>((sp, o) => Configure(o, sp.GetRequiredService<SqliteConnection>()));
            //services.AddDbContext<SimulationScenarioDbContext>((sp, o) => Configure(o, sp.GetRequiredService<SqliteConnection>()));
            //services.AddDbContext<ProcessMiningEventDbContext>((sp, o) => Configure(o, sp.GetRequiredService<SqliteConnection>()));
            //services.AddDbContext<DecisionDbContext>((sp, o) => Configure(o, sp.GetRequiredService<SqliteConnection>()));

            services.AddSingleton<ITestDataSeeder, TenantAndUserSeeder>();
            services.AddSingleton<ITestDataSeeder, ProcessDefinitionSeeder>();
            services.AddSingleton<ITestDataSeeder, DecisionSeeder>();

            if (_backgroundJobsEnabled)
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, JobExecutorService>());
            }

            _configureTestServices?.Invoke(services);
        });
    }

    private SqliteConnection CreateAndTrack(string dataSource, string label)
    {
        // Separate In-Memory-DB: Data Source=:memory:
        // Wenn du mehrere offene Verbindungen brauchst, aber dieselbe DB teilen willst: Data Source=file:vertex_{label}?mode=memory&cache=shared
        var connString = $"Data Source=file:vertex_{_databaseId}_{label}?mode=memory&cache=shared";
        var conn = new SqliteConnection(connString);
        conn.Open();
        _ownedConnections.Add(conn);
        return conn;
    }

    public async ValueTask InitializeAsync()
    {
        if (_initialized) return;

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
                    throw new InvalidOperationException($"Required table '{table}' not found in manual schema (check Sqlite.sql).");
            }
        }
        await RunSeedersAsync(scope, logger);

        _initialized = true;
    }

    public async Task DisposeAsync()
    {
        foreach (var c in _ownedConnections)
        {
            try
            {
                await c.CloseAsync();
                await c.DisposeAsync();
            }
            catch { /* ignore */ }
        }
        _ownedConnections.Clear();
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
            try { await ctx.Database.MigrateAsync(); } catch { /* ignore for pure EnsureCreated flow */ }
        }
        logger.LogDebug("{DbContext} ready (Created={Created})", typeof(T).Name, created);
    }

    private async Task RunSeedersAsync(IServiceScope scope, ILogger logger, CancellationToken ct = default)
    {
        var seeders = scope.ServiceProvider
            .GetServices<ITestDataSeeder>()
            .OrderBy(s => s.Order)
            .ToList();

        foreach (var seeder in seeders)
        {
            try
            {
                logger.LogInformation("Running seeder {Seeder} (Order={Order})", seeder.GetType().Name, seeder.Order);
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
        InitializeAsync().GetAwaiter().GetResult();
        return base.CreateClient();
    }

    public new HttpClient CreateClient(WebApplicationFactoryClientOptions options)
    {
        InitializeAsync().GetAwaiter().GetResult();
        var client = base.CreateClient(options);
        return client;
    }

    public new HttpClient CreateClient(ITestOutputHelper output)
    {
        InitializeAsync().GetAwaiter().GetResult();
        var options = new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        };

        var client = base.CreateClient(options);
        client.Timeout = TimeSpan.FromSeconds(30);

        Services.GetRequiredService<ILoggerFactory>()
            .AddProvider(new XunitLoggerProvider(output));
        InitializeAsync().GetAwaiter().GetResult();
        return client;
    }
}
