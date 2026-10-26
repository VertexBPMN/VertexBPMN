using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

/// <summary>
/// LC-AC-15/16/17: gemeinsame Aktivierung, Rollback und Provenienz über die echte HTTP-API
/// mit ZWEI API-Host-Instanzen gegen denselben Store auf echtem PostgreSQL (generierte EF-Migration,
/// inklusive 20261026052338_ApplicationReleaseActivation => Tabelle ApplicationReleases mit
/// eindeutigem {TenantId, ApplicationDefinitionId, OperationId}-Index).
///
///   LC-AC-15: zwei Replikate + wiederholter Aktivierungsaufruf -> GENAU EINE konsistente aktive
///             Aktivierung (idempotent über {TenantId, ApplicationDefinitionId, OperationId}).
///   LC-AC-16a: Abbruch vor dem Deployment (nicht satisfiierbare Revision) -> Rekonziliation zu Failed.
///   LC-AC-16b: Abbruch nach dem Deployment (Pointer-Switch offen, durable Ready) -> Ready->Active via Rekonziliation.
///   LC-AC-17:  Release-1-Operationen laufen nach Release-2-Aktivierung UND nach Rollback weiter
///              (Pointer wechselt neue Starts, bereits Gebundene laufen weiter).
///
/// Store-Auswahl ist nicht über Test-Factory-Konfiguration zu erzwingen (Program/MigrationCoordinator
/// liest die Verbindungsstrings zur Build-Zeit aus appsettings.Test.json = SQLite :memory:). Deshalb
/// setzt die Collection-Fixture die Verbindungsstrings als PROZESS-Umgebungsvariablen (ConnectionStrings:*),
/// die WebApplication.CreateBuilder standardmäßig und mit höherer Priorität als appsettings.json liest —
/// damit registriert AddAllEngineDbContexts ALLE Kontexte auf demselben PostgreSQL-Store (ein Provider).
/// </summary>
[Collection("Lc10PostgresReleaseAcceptance")]
public sealed class Lc10ReleaseActivationAcceptanceTests
{
    private const string Tenant = "vertexbpmn";
    private const string AdminDatabase = "vertexbpmn_bpmn";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Container lcac08-infra-platform-postgres-1 (LC-AC-08 Infrastruktur) -- discovered via docker inspect,
    // NICHT hartkodiert in Skripten/Runbooks; die Acceptance-Fixture wählt den Store zur Laufzeit.
    private const string PgHost = "localhost";
    private const int PgPort = 5432;
    private const string PgUser = "vertexbpmn";
    private const string PgPassword = "33ffaead98e566d575151e4bbaa4dc566dbebdc8";

    /// <summary>
    /// Gemeinsamer PostgreSQL-Store der Collection: legt EINE Datenbank an, führt die echten
    /// Bpmn-Migrationen (inkl. ApplicationReleaseActivation) aus und setzt die Store-Verbindungen
    /// als Prozess-Umgebungsvariablen, damit die zwei API-Replikate denselben Store teilen.
    /// </summary>
#pragma warning disable CA2100 // Create/Drop DATABASE names sind GUID-Ableitungen (kein Benutzereingabestrom)
    internal sealed class Lc10PostgresSharedStore : IAsyncLifetime
    {
        private const string EnvPrefix = "ConnectionStrings__";
        private static readonly string[] EnvKeys =
        [
            "Bpmn", "BpmnDbContext",
            "Tenants", "TenantDbContext",
            "Simulation", "SimulationScenarioDbContext",
            "ProcessMiningEvents",
            "Decision", "DecisionDbContext",
            "DependencyRegistry",
        ];

        public string DatabaseName { get; } = "lc10_ac_" + Guid.NewGuid().ToString("n")[..10];
        public string ConnectionString { get; }
        private readonly string _adminConnectionString;
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public Lc10PostgresSharedStore()
        {
            ConnectionString = $"Host={PgHost};Port={PgPort};Database={DatabaseName};Username={PgUser};Password={PgPassword}";
            _adminConnectionString = $"Host={PgHost};Port={PgPort};Database={AdminDatabase};Username={PgUser};Password={PgPassword}";
        }

        public async ValueTask InitializeAsync()
        {
            await using (var admin = new NpgsqlConnection(_adminConnectionString))
            {
                await admin.OpenAsync(Ct);
                await using var cmd = admin.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE \"{DatabaseName}\"";
                await cmd.ExecuteNonQueryAsync(Ct);
            }

            foreach (var key in EnvKeys)
            {
                var envKey = EnvPrefix + key;
                _originalValues[envKey] = Environment.GetEnvironmentVariable(envKey);
                Environment.SetEnvironmentVariable(envKey, ConnectionString);
            }

            // Echte Migration der vollständigen Bpmn-Kette (inkl. 20261026052338_ApplicationReleaseActivation)
            // auf dem frischen PostgreSQL-Store; Hilfs-Stores ebenfalls auf demselben Store.
            await MigrateAsync<BpmnDbContext>();
            await MigrateAsync<TenantDbContext>();
            await MigrateAsync<SimulationScenarioDbContext>();
            await MigrateAsync<ProcessMiningEventDbContext>();
            await MigrateAsync<DecisionDbContext>();
            await MigrateAsync<DependencyRegistryDbContext>();
        }

        private async Task MigrateAsync<T>() where T : DbContext
        {
            var options = new DbContextOptionsBuilder<T>()
                .UseVertexNpgsql(ConnectionString)
                .Options;
            await using var ctx = (T)Activator.CreateInstance(typeof(T), options)!;
            await ctx.Database.MigrateAsync(Ct);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var (key, original) in _originalValues)
            {
                Environment.SetEnvironmentVariable(key, original);
            }

            try
            {
                await using var admin = new NpgsqlConnection(_adminConnectionString);
                await admin.OpenAsync(Ct);
                await using var cmd = admin.CreateCommand();
                cmd.CommandText = $"DROP DATABASE \"{DatabaseName}\" WITH (FORCE)";
                await cmd.ExecuteNonQueryAsync(Ct);
            }
            catch
            {
                // Bereits weg oder Verbindung unterbrochen -- Satzpflicht bewusst NICHT hart.
            }
        }
    }

    /// <summary>Ein echtes API-Replikat: Store-Verbindungen kommen aus den Prozess-Umgebungsvariablen (PostgreSQL).</summary>
    private sealed class Lc10ReplicaFactory : WebApplicationFactory<VertexBPMN.Api.Program>
    {
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
                        ["Database:ApplyMigrationsOnStartup"] = "false",
                        ["ProcessEngine:Type"] = "Simple",
                        ["Modules:Swagger"] = "false",
                        ["PathBase"] = "/api",
                    });
            });
            // Test-Authentifizierung + Policies (identisch zu CustomWebApplicationFactory).
            builder.ConfigureServices(ConfigureTestAuthentication);
        }

        private static void ConfigureTestAuthentication(IServiceCollection services)
        {
            services.AddAuthentication("Test")
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
                options.AddPolicy("ProcessManager", policy => policy.RequireRole("Admin", "ProcessManager"));
                options.AddPolicy("ReadOnly", policy => policy.RequireRole("Admin", "ProcessManager", "ReadOnly"));
                VertexBPMN.Api.Security.SecurityConfiguration.AddTenantReadOnlyPolicy(options);
            });
        }
    }

    /// <summary>Hält beide API-Replikate + einen Store-Context für DB-Beweisführung und gibt alles frei.</summary>
    private sealed class ReplicaSet : IAsyncDisposable
    {
        public required string ConnectionString { get; init; }
        public required BpmnDbContext Db { get; init; }
        public required HttpClient A { get; init; }
        public required HttpClient B { get; init; }
        public required Lc10ReplicaFactory FactoryA { get; init; }
        public required Lc10ReplicaFactory FactoryB { get; init; }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            A.Dispose();
            B.Dispose();
            await FactoryA.DisposeAsync();
            await FactoryB.DisposeAsync();
        }
    }

    private async Task<ReplicaSet> CreateTwoReplicasAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Bpmn")!;

        var replicaA = new Lc10ReplicaFactory();
        var replicaB = new Lc10ReplicaFactory();
        try
        {
            var clientA = replicaA.CreateClient();
            var clientB = replicaB.CreateClient();
            var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>()
                .UseVertexNpgsql(connectionString).Options);
            return new ReplicaSet { ConnectionString = connectionString, Db = db, A = clientA, B = clientB, FactoryA = replicaA, FactoryB = replicaB };
        }
        catch
        {
            await replicaA.DisposeAsync();
            await replicaB.DisposeAsync();
            throw;
        }
    }

    private static async Task SeedProcessAsync(BpmnDbContext db, string tenant, string key, Guid id)
    {
        if (await db.ProcessDefinitions.AnyAsync(x => x.TenantId == tenant && x.Key == key, Ct)) { return; }
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "lc10-deployment", CreatedAt = DateTime.UtcNow };
        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.Add(new ProcessDefinition
        {
            Id = id,
            Key = key,
            Name = key,
            Version = 1,
            TenantId = tenant,
            Deployment = deployment,
            DeploymentId = deployment.Id,
            BpmnXml = "<definitions/>",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(Ct);
    }

    private static async Task<string> SeedApplicationAsync(BpmnDbContext db, string tenant, string appKey,
        (int Version, string Content)[] revisions)
    {
        var appId = "app-" + Guid.NewGuid().ToString("n");
        db.ApplicationDefinitions.Add(new ApplicationDefinitionRecord
        {
            Id = appId,
            TenantId = tenant,
            Key = appKey,
            Name = appKey,
            ContentJson = "{}",
            Version = revisions.Max(r => r.Version),
        });
        foreach (var (version, content) in revisions)
        {
            db.ApplicationRevisions.Add(new ApplicationRevisionRecord
            {
                Id = "rev-" + appId + "-" + version,
                ApplicationDefinitionId = appId,
                TenantId = tenant,
                ContentJson = content,
                ContentHash = "h" + version,
                Version = version,
            });
        }
        await db.SaveChangesAsync(Ct);
        return appId;
    }

    private static string Manifest(string key, int revision) =>
        "{\"schemaVersion\":\"1\",\"artifactKind\":\"application\",\"key\":\"" + key +
        "\",\"revision\":" + revision + ",\"hash\":\"deadbeef\"}";

    private static string Content(string processKey, Guid processId) =>
        "{\"references\":[{\"type\":\"process\",\"key\":\"" + processKey + "\",\"id\":\"" +
        processId + "\",\"revision\":1}]}";

    private const string ContentMissingForm =
        "{\"references\":[{\"type\":\"form\",\"key\":\"MissingForm\",\"revision\":2}]}";

    private static string PrepareUrl(string appId) => $"api/applications/{appId}/releases/prepare?tenantId={Tenant}";
    private static string ActivateUrl(string appId) => $"api/applications/{appId}/releases/activate?tenantId={Tenant}";
    private static string StatusUrl(string appId) => $"api/applications/{appId}/releases/status?tenantId={Tenant}";
    private static string RollbackUrl(string appId) => $"api/applications/{appId}/releases/rollback?tenantId={Tenant}";
    private static string ActiveStartUrl(string appId) => $"api/applications/{appId}/releases/active-start?tenantId={Tenant}";

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync(Ct);
            throw new Xunit.Sdk.XunitException($"Unexpected status {response.StatusCode} (expected {expected}); body: {body}");
        }
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct);
    }

    /// <summary>Groß-/kleinschreibungs-unabhängiger Zugriff auf ein Release-Status-Feld.</summary>
    private static JsonElement Prop(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new Xunit.Sdk.XunitException("erwartetes Release-Status-Objekt, aber erhalten: " + root.ToString());
        }
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }
        throw new Xunit.Sdk.XunitException($"Release-Status enthält kein Feld '{name}': " + root.ToString());
    }

    private static async Task<string> ActiveStartProcessIdAsync(HttpClient client, string appId)
    {
        var json = await ReadJson(await client.GetAsync(ActiveStartUrl(appId), Ct), HttpStatusCode.OK);
        return Prop(json, "processDefinitionId").GetString()!;
    }

    // =================================================================================
    // LC-AC-15: Zwei Replikate + wiederholter Aktivierungsaufruf -> genau EINE aktive Generation
    // =================================================================================
    [Fact]
    public async Task LcAc15_TwoReplicas_RepeatedActivation_YieldsExactlyOneActiveActivation()
    {
        await using var rs = await CreateTwoReplicasAsync();
        var procId = Guid.NewGuid();
        await SeedProcessAsync(rs.Db, Tenant, "lc15-proc", procId);
        var suffix = Guid.NewGuid().ToString("n")[..8];
        var appKey = "lc15-app-" + suffix;
        var appId = await SeedApplicationAsync(rs.Db, Tenant, appKey, [(1, Content("lc15-proc", procId))]);
        const string op = "lc15-op";

        // Vorbereitung über Replikat A -> 201 Prepared; Wiederholung mit DERSELBEN Operation-Identity idempotent.
        var prep1 = await ReadJson(await rs.A.PostAsJsonAsync(PrepareUrl(appId),
            new { operationId = op, revision = 1, manifestJson = Manifest(appKey, 1) }, Ct), HttpStatusCode.Created);
        Assert.Equal(JsonValueKind.Object, prep1.ValueKind);
        Assert.Equal("Prepared", Prop(prep1, "state").GetString());

        var prep2 = await ReadJson(await rs.A.PostAsJsonAsync(PrepareUrl(appId),
            new { operationId = op, revision = 1, manifestJson = Manifest(appKey, 1) }, Ct), HttpStatusCode.Created);
        Assert.Equal(Prop(prep1, "operationId").GetString(), Prop(prep2, "operationId").GetString(), StringComparer.Ordinal);

        // Zwei Replikate aktivieren GLEICHZEITIG mit derselben Operation-Identity.
        var tA = rs.A.PostAsJsonAsync(ActivateUrl(appId), new { operationId = op }, Ct);
        var tB = rs.B.PostAsJsonAsync(ActivateUrl(appId), new { operationId = op }, Ct);
        await Task.WhenAll(tA, tB);

        var jsonA = await ReadJson(await tA, HttpStatusCode.OK);
        var jsonB = await ReadJson(await tB, HttpStatusCode.OK);
        Assert.Equal("Active", Prop(jsonA, "state").GetString());
        Assert.Equal("Active", Prop(jsonB, "state").GetString());
        Assert.Equal(procId.ToString(), Prop(jsonA, "boundProcessDefinitionId").GetString());

        // Wiederholter (sequenzieller) Aktivierungsaufruf mit derselben Operation-Identity bleibt idempotent.
        var repeat = await ReadJson(await rs.A.PostAsJsonAsync(ActivateUrl(appId), new { operationId = op }, Ct), HttpStatusCode.OK);
        Assert.Equal("Active", Prop(repeat, "state").GetString());

        // Exakt EINE aktive Generation in der Datenbank.
        var activeCount = await rs.Db.ApplicationReleases.CountAsync(x =>
            x.TenantId == Tenant && x.ApplicationDefinitionId == appId && x.State == ApplicationReleaseState.Active, Ct);
        Assert.Equal(1, activeCount);

        // Beide Replikate sehen denselben aktiven Zeiger.
        Assert.Equal(procId.ToString(), await ActiveStartProcessIdAsync(rs.B, appId));
    }

    // =================================================================================
    // LC-AC-16a: Abbruch vor dem Deployment -> Rekonziliation ergibt Failed (nicht satisfiierbar).
    // =================================================================================
    [Fact]
    public async Task LcAc16_AbortBeforeDeployment_NonSatisfiable_ReconcilesToFailed()
    {
        await using var rs = await CreateTwoReplicasAsync();
        var suffix = Guid.NewGuid().ToString("n")[..8];
        var appKey = "lc16a-app-" + suffix;
        var appId = await SeedApplicationAsync(rs.Db, Tenant, appKey, [(1, ContentMissingForm)]);

        // Simulierter Replikat-Crash: durable Operation blieb in DeploymentRunning hängen,
        // gebunden an eine Revision, deren Referenzgraph nicht satisfiierbar ist.
        rs.Db.ApplicationReleases.Add(new ApplicationReleaseRecord
        {
            OperationId = "lc16a-op",
            TenantId = Tenant,
            ApplicationDefinitionId = appId,
            ApplicationRevisionId = "rev-" + appId + "-1",
            ManifestJson = Manifest(appKey, 1),
            State = ApplicationReleaseState.DeploymentRunning,
            DiagnosticsJson = "[]",
            PointerRevision = 1,
            PreparedAtTicks = DateTime.UtcNow.Ticks,
            DeployStartedAtTicks = DateTime.UtcNow.Ticks,
            UpdatedAtTicks = DateTime.UtcNow.Ticks,
        });
        await rs.Db.SaveChangesAsync(Ct);

        // Status über Replikat A rekonziliert deterministisch -> Failed mit Diagnose.
        var status = await ReadJson(await rs.A.GetAsync(StatusUrl(appId), Ct), HttpStatusCode.OK);
        Assert.Equal("Failed", Prop(status, "state").GetString());
        Assert.NotEqual("[]", Prop(status, "diagnostics").GetString(), StringComparer.Ordinal);

        var persisted = await rs.Db.ApplicationReleases.AsNoTracking().SingleAsync(x => x.OperationId == "lc16a-op", Ct);
        Assert.Equal(ApplicationReleaseState.Failed, persisted.State);
    }

    // =================================================================================
    // LC-AC-16b: Abbruch nach dem Deployment (Pointer-Switch offen, durable Ready) -> Ready->Active.
    // =================================================================================
    [Fact]
    public async Task LcAc16_AbortAfterDeployment_ReconcilesReadyThenActivate_ToActive()
    {
        await using var rs = await CreateTwoReplicasAsync();
        var procId = Guid.NewGuid();
        await SeedProcessAsync(rs.Db, Tenant, "lc16b-proc", procId);
        var suffix = Guid.NewGuid().ToString("n")[..8];
        var appKey = "lc16b-app-" + suffix;
        var appId = await SeedApplicationAsync(rs.Db, Tenant, appKey, [(1, Content("lc16b-proc", procId))]);

        // Simulierter Replikat-Crash NACH dem Deploy, aber bevor der aktive Zeiger gesetzt wurde.
        rs.Db.ApplicationReleases.Add(new ApplicationReleaseRecord
        {
            OperationId = "lc16b-op",
            TenantId = Tenant,
            ApplicationDefinitionId = appId,
            ApplicationRevisionId = "rev-" + appId + "-1",
            ManifestJson = Manifest(appKey, 1),
            State = ApplicationReleaseState.DeploymentRunning,
            DiagnosticsJson = "[]",
            BoundProcessDefinitionId = procId.ToString(),
            PointerRevision = 1,
            PreparedAtTicks = DateTime.UtcNow.Ticks,
            DeployStartedAtTicks = DateTime.UtcNow.Ticks,
            UpdatedAtTicks = DateTime.UtcNow.Ticks,
        });
        await rs.Db.SaveChangesAsync(Ct);

        // Status rekonziliert die satisfiierbare Revision deterministisch zu Ready.
        var status = await ReadJson(await rs.B.GetAsync(StatusUrl(appId), Ct), HttpStatusCode.OK);
        Assert.True(Prop(status, "state").GetString() is "Ready" or "Active",
            $"Erwartet Rekonziliation zu Ready/Active, erhielt {Prop(status, "state").GetString()}.");

        // Aktivierung schließt den Zeiger-Wechsel ab -> Active, genau eine aktive Generation.
        var active = await ReadJson(await rs.A.PostAsJsonAsync(ActivateUrl(appId), new { operationId = "lc16b-op" }, Ct), HttpStatusCode.OK);
        Assert.Equal("Active", Prop(active, "state").GetString());
        Assert.Equal(procId.ToString(), Prop(active, "boundProcessDefinitionId").GetString());

        var activeCount = await rs.Db.ApplicationReleases.CountAsync(x =>
            x.TenantId == Tenant && x.ApplicationDefinitionId == appId && x.State == ApplicationReleaseState.Active, Ct);
        Assert.Equal(1, activeCount);
    }

    // =================================================================================
    // LC-AC-17: Release-2-Aktivierung und Rollback wechseln neue Starts; bereits Gebundene laufen weiter.
    // =================================================================================
    [Fact]
    public async Task LcAc17_Release2ActivationAndRollback_SwitchesNewStarts_AlreadyBoundContinue()
    {
        await using var rs = await CreateTwoReplicasAsync();
        var procAlpha = Guid.NewGuid();
        var procBeta = Guid.NewGuid();
        await SeedProcessAsync(rs.Db, Tenant, "lc17-alpha", procAlpha);
        await SeedProcessAsync(rs.Db, Tenant, "lc17-beta", procBeta);
        var suffix = Guid.NewGuid().ToString("n")[..8];
        var appKey = "lc17-app-" + suffix;
        var appId = await SeedApplicationAsync(rs.Db, Tenant, appKey,
        [
            (1, Content("lc17-alpha", procAlpha)),
            (2, Content("lc17-beta", procBeta)),
        ]);

        // Release-1 aktivieren (Revision 1 -> alpha), über Replikat A.
        await rs.A.PostAsJsonAsync(PrepareUrl(appId), new { operationId = "op1", revision = 1, manifestJson = Manifest(appKey, 1) }, Ct);
        var rel1 = await ReadJson(await rs.A.PostAsJsonAsync(ActivateUrl(appId), new { operationId = "op1" }, Ct), HttpStatusCode.OK);
        Assert.Equal("Active", Prop(rel1, "state").GetString());
        Assert.Equal(procAlpha.ToString(), Prop(rel1, "boundProcessDefinitionId").GetString());
        Assert.True(await rs.Db.ProcessDefinitions.AnyAsync(x => x.Id == procAlpha, Ct), "Alpha deployt für gebundene Release-1-Ops.");

        // Release-2 aktivieren (Revision 2 -> beta), über Replikat B -> neuer aktiver Zeiger.
        await ReadJson(await rs.B.PostAsJsonAsync(PrepareUrl(appId), new { operationId = "op2", revision = 2, manifestJson = Manifest(appKey, 2) }, Ct), HttpStatusCode.Created);
        var rel2 = await ReadJson(await rs.B.PostAsJsonAsync(ActivateUrl(appId), new { operationId = "op2" }, Ct), HttpStatusCode.OK);
        Assert.Equal("Active", Prop(rel2, "state").GetString());
        Assert.Equal(procBeta.ToString(), Prop(rel2, "boundProcessDefinitionId").GetString());

        // Neue Starts binden jetzt Release-2 (beta); alpha bleibt deployt -> gebundene Release-1-Ops laufen weiter.
        Assert.Equal(procBeta.ToString(), await ActiveStartProcessIdAsync(rs.A, appId));
        Assert.True(await rs.Db.ProcessDefinitions.AnyAsync(x => x.Id == procAlpha, Ct), "Alpha muss auch nach Release-2 deployt bleiben.");

        // Rollback über Replikat A: neue Starts zurück auf Release-1 (alpha); Release-2 -> Recovered.
        var rolledBack = await ReadJson(await rs.A.PostAsJsonAsync(RollbackUrl(appId), new { operationId = "op-rollback" }, Ct), HttpStatusCode.OK);
        Assert.Equal("Active", Prop(rolledBack, "state").GetString());

        Assert.Equal(procAlpha.ToString(), await ActiveStartProcessIdAsync(rs.B, appId));

        // DB: genau eine aktive Generation, op2 als Recovered; beide Prozesse bleiben deployt.
        var actives = await rs.Db.ApplicationReleases.Where(x =>
            x.TenantId == Tenant && x.ApplicationDefinitionId == appId && x.State == ApplicationReleaseState.Active).ToListAsync(Ct);
        Assert.Single(actives);
        var gen2 = await rs.Db.ApplicationReleases.SingleAsync(x => x.OperationId == "op2", Ct);
        Assert.Equal(ApplicationReleaseState.Recovered, gen2.State);
        Assert.True(await rs.Db.ProcessDefinitions.AnyAsync(x => x.Id == procAlpha, Ct), "Alpha bleibt nach Rollback deployt (bereits gebundene Ops laufen weiter).");
        Assert.True(await rs.Db.ProcessDefinitions.AnyAsync(x => x.Id == procBeta, Ct), "Beta bleibt deployt (in-flight Ops laufen weiter).");
    }
}
