using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<VertexBPMN.Api.Program>
{
    private readonly object _initializationLock = new();
    private bool _isInitialized = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            var env = ctx.HostingEnvironment;

            cfg
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddJsonFile("appsettings.Test.json", optional: true)
                .AddEnvironmentVariables()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OperationalMode"] = "Test",
                    ["Modules:Swagger"] = "true",
                    ["PathBase"] = "/api",
                    // Use proper SQLite in-memory connection strings - each database gets its own memory space
                    ["ConnectionStrings:Bpmn_Test"] = "Data Source=:memory:",
                    ["ConnectionStrings:Tenants_Test"] = "Data Source=:memory:",
                    ["ConnectionStrings:Simulation_Test"] = "Data Source=:memory:", 
                    ["ConnectionStrings:ProcessMiningEvents_Test"] = "Data Source=:memory:",
                    ["ConnectionStrings:Decision_Test"] = "Data Source=:memory:",
                });
        });

        builder.ConfigureServices(services =>
        {
            // Override DbContext configurations to ensure proper in-memory setup
            services.RemoveAll<DbContextOptions<BpmnDbContext>>();
            services.RemoveAll<DbContextOptions<TenantDbContext>>();
            services.RemoveAll<DbContextOptions<SimulationScenarioDbContext>>();
            services.RemoveAll<DbContextOptions<ProcessMiningEventDbContext>>();
            services.RemoveAll<DbContextOptions<DecisionDbContext>>();
            // Erstelle EINE einzige Verbindung, die von allen Contexts geteilt wird.
            // Registriere sie als Singleton, damit sie während des gesamten Tests am Leben bleibt.
            services.AddSingleton(sp =>
            {
                // Wichtig: Die Verbindung muss manuell geöffnet werden!
                var connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                return connection;
            });
            services.AddDbContext<BpmnDbContext>((sp,options) =>
            {
                var connection = sp.GetRequiredService<SqliteConnection>();
                options.UseSqlite(connection)
                               .EnableSensitiveDataLogging()
                       .EnableDetailedErrors();
            });

            services.AddDbContext<TenantDbContext>((sp, options) =>
            {
                var connection = sp.GetRequiredService<SqliteConnection>();
                options.UseSqlite(connection)
                       .EnableSensitiveDataLogging()
                       .EnableDetailedErrors();
            });

            services.AddDbContext<SimulationScenarioDbContext>((sp, options) =>
            {
                var connection = sp.GetRequiredService<SqliteConnection>();
                options.UseSqlite(connection)
                       .EnableSensitiveDataLogging()
                       .EnableDetailedErrors();
            });

            services.AddDbContext<ProcessMiningEventDbContext>((sp, options) =>
            {
                var connection = sp.GetRequiredService<SqliteConnection>();
                options.UseSqlite(connection)
                       .EnableSensitiveDataLogging()
                       .EnableDetailedErrors();
            });

            services.AddDbContext<DecisionDbContext>((sp, options) =>
            {
                var connection = sp.GetRequiredService<SqliteConnection>();
                options.UseSqlite(connection)
                       .EnableSensitiveDataLogging()
                       .EnableDetailedErrors();
            });

        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Any cleanup if needed
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Ensures the test databases are initialized. This method is thread-safe and will only initialize once.
    /// </summary>
    public async Task EnsureDatabasesInitializedAsync()
    {
        if (_isInitialized)
            return;

        lock (_initializationLock)
        {
            if (_isInitialized)
                return;

            // Use Task.Run to avoid deadlock issues with async initialization
            Task.Run(async () =>
            {
                await InitializeDatabasesAsync();
                _isInitialized = true;
            }).GetAwaiter().GetResult();
        }
    }

    private async Task InitializeDatabasesAsync()
    {
        using var scope = Services.CreateScope();
        var logger = scope.ServiceProvider.GetService<ILogger<CustomWebApplicationFactory>>() 
                     ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CustomWebApplicationFactory>.Instance;

        try
        {
            await EnsureDbCreatedAsync<BpmnDbContext>(scope, logger);
            await EnsureDbCreatedAsync<TenantDbContext>(scope, logger);
            await EnsureDbCreatedAsync<SimulationScenarioDbContext>(scope, logger);
            await EnsureDbCreatedAsync<ProcessMiningEventDbContext>(scope, logger);
            await EnsureDbCreatedAsync<DecisionDbContext>(scope, logger);

            logger.LogInformation("All test databases initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing test databases");
            throw;
        }
    }

    private static async Task EnsureDbCreatedAsync<T>(IServiceScope scope, ILogger logger) where T : DbContext
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<T>();
            
            // Log the connection string for debugging
            logger.LogInformation("Initializing {DbContextType} with connection: {ConnectionString}", 
                typeof(T).Name, db.Database.GetConnectionString());


            var created = await db.Database.EnsureCreatedAsync();
            if (!created)
            {
                await db.Database.MigrateAsync();
            }
            logger.LogInformation("Database in im memory {DbContextType} = {0} initialized successfully", typeof(T).Name, created);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize database {DbContextType}", typeof(T).Name);
            throw;
        }
    }


    public HttpClient CreateClient()
    {
        EnsureDatabasesInitializedAsync().GetAwaiter().GetResult();
        return base.CreateClient();
    }

    public  HttpClient CreateClient(WebApplicationFactoryClientOptions options)
    {
        EnsureDatabasesInitializedAsync().GetAwaiter().GetResult();
        return base.CreateClient(options);
    }
}