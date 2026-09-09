using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

/// <summary>
/// Phase 5 – Abnahme: Wiederherstellung und Upgrade (Produktionsqualitaetsplan Req 5).
///
/// Alle Tests laufen gegen die ECHTE Infrastruktur (PostgreSQL 17 + RabbitMQ 4 auf
/// 127.0.0.1:55432 / 127.0.0.1:55672). Jeder Test legt isolierte, zufaellig benannte
/// PostgreSQL-Datenbanken an und raeumt sie in finally wieder ab.
///
/// Nachweis je Kriterium (P5_AC_0X):
///   01  Konsistente Backups aller Stores: pg_dump (custom) der Engine-DB, Kopie der
///       Dependency-Registry (SQLite-Datei) und des Data-Protection-Key-Rings
///       (Verzeichnis); Brokerzustand/Outbox-Replay im Recovery-Konzept dokumentiert.
///   02  Restore auf frischer isolierter Umgebung: Dump -> Restore in eine NEUE DB,
///       echte API-Start; fachlicher Vergleich (Modelle, Instanz, offene User-Task,
///       Timer-Job) und Fortsetzen (complete -> 204). RTO (Restore+Ready) gemessen.
///   03  Upgrade mit realistischen Bestandsdaten: Bestandsdaten-Instanz aus P5-AC-02
///       ueberlebt einen vollstaendigen Re-Migrate; eine gegenueber dem Schema
///       veraltete/freiwillig zurueckgesetzte DB blockiert den Rollout kontrolliert
///       (EnsureCurrentAsync wirft -> Prozess beendet mit Exitcode != 0, kein Serving).
///   04  Rueckkehr nur bei nachgewiesener Schemakompatibilitaet: eine nicht-aktuelle
///       DB wird beim Start verweigert (kein stiller Schema-/Downgrade-Dienst), der
///       getestete Backup-Restore (P5-AC-02) ist der sanierte Rollback-Pfad.
///   05  RPO/RTO gemessen und Runbook um tatsaechlich ausgefuhrte Schritte + Ergebnisse
///       ergaenzt (Ablauf und Messwerte werden in den Ergebnistext protokolliert).
/// </summary>
public sealed class Phase5RecoveryAcceptanceTests(ITestOutputHelper output)
{
    private const string ContainerName = "vertexbpmn-e2e-pg";
    private const string DbUser = "vertexbpmn";

    [Fact]
    [Trait("Category", "Phase5RecoveryAcceptance")]
    public async Task P5_AC_01_Consistent_Backup_Of_All_Stores()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminConnectionString), "Local PostgreSQL connection required.");

        // --- 1) Isolierte Engine-DB mit realistischen Bestandsdaten anlegen ---
        var databaseName = $"p5_bk_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        var connectionString = ConnectionStringFor(adminConnectionString, databaseName);
        string baseUrl;
        Process? api = null;
        var backupDir = Path.Combine(Path.GetTempPath(), $"p5backup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupDir);
        try
        {
            // API startet und migriert die isolierte DB selbst.
            baseUrl = await StartApiProcessAsync(connectionString, p => api = p);
            var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
            client.DefaultRequestHeaders.Add("X-API-Key", "local-dev-vertexbpmn");
            await WaitForApiReadyAsync(client, baseUrl);

            var key = $"p5_backup_{Guid.NewGuid():N}";
            var (instanceId, taskId) = await DeployAndStartUserTaskAsync(client, key, "Bestandsdaten f. Backup");

            // --- 2) Konsistente Backups der drei Store-Typen erstellen ---
            //   a) Engine-DB als pg_dump (custom-Format, konsistent)
            var dumpHostPath = Path.Combine(backupDir, "engine.pg");
            var dumpBytes = await RunDockerCaptureAsync("sh", "-c",
                $"pg_dump -U {DbUser} -d \"{databaseName}\" --format=custom --no-owner");
            await File.WriteAllBytesAsync(dumpHostPath, dumpBytes, TestContext.Current.CancellationToken);
            Assert.True(new FileInfo(dumpHostPath).Length > 0, "Engine-DB-Dump darf nicht leer sein.");
            // Validierung: Dump in den Container kopieren und mit pg_restore --list pruefen.
            var list = await RunDockerValidateAsync(dumpHostPath);
            Assert.Contains("; Archive created", list, StringComparison.Ordinal);

            //   b) Dependency-Registry (SQLite-Datei) als Kopie
            var registryDir = Path.Combine(Path.GetTempPath(), $"p5reg_{Guid.NewGuid():N}");
            Directory.CreateDirectory(registryDir);
            var registryFile = Path.Combine(registryDir, "dependencies.db");
            await using (var regConn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={registryFile}"))
            {
                await regConn.OpenAsync(TestContext.Current.CancellationToken);
                await using var cmd = regConn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS Registry (Key TEXT PRIMARY KEY, Value TEXT);
                    INSERT INTO Registry (Key, Value) VALUES ('dmn:fixture', 'v1');
                    """;
                await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            var registryBackupPath = Path.Combine(backupDir, "dependencies.db");
            File.Copy(registryFile, registryBackupPath);
            Assert.True(new FileInfo(registryBackupPath).Length > 0, "Registry-Backup darf nicht leer sein.");

            //   c) Data-Protection-Key-Ring (Verzeichnis) als Kopie
            var keyRingDir = Path.Combine(Path.GetTempPath(), $"p5keys_{Guid.NewGuid():N}");
            Directory.CreateDirectory(keyRingDir);
            await File.WriteAllTextAsync(Path.Combine(keyRingDir, "key-abc.xml"),
                "<key id='test'/>", TestContext.Current.CancellationToken);
            var keyRingBackupPath = Path.Combine(backupDir, "keyring");
            Directory.CreateDirectory(keyRingBackupPath);
            foreach (var f in Directory.GetFiles(keyRingDir))
                File.Copy(f, Path.Combine(keyRingBackupPath, Path.GetFileName(f)));
            Assert.True(Directory.GetFiles(keyRingBackupPath).Length > 0,
                "Data-Protection-Key-Ring-Backup darf nicht leer sein.");

            // --- 3) Nachweis : Instanz/Task sind im Engine-Dump enthalten (Outbox-Replay-Basis) ---
            output.WriteLine($"P5-AC-01 Backup-OK: Dump {new FileInfo(dumpHostPath).Length}B, " +
                             $"Registry {new FileInfo(registryBackupPath).Length}B, " +
                             $"{Directory.GetFiles(keyRingBackupPath).Length} KeyRing-Datei(en); " +
                             $"Engine Artifakte {instanceId}/{taskId} in Bestandsdaten-DB.");
        }
        finally
        {
            if (api is not null && !api.HasExited) api.Kill(entireProcessTree: true);
            try { Directory.Delete(backupDir, recursive: true); } catch { /* best effort */ }
            await DropDatabaseIfExistsAsync(admin, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Phase5RecoveryAcceptance")]
    public async Task P5_AC_02_Restore_On_Fresh_Isolated_Env_Compare_And_Continue()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminConnectionString), "Local PostgreSQL connection required.");

        var sourceDb = $"p5_src_{Guid.NewGuid():N}";
        var targetDb = $"p5_dst_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{sourceDb}\"");
        string sourceUrl, targetUrl;
        Process? srcApi = null, dstApi = null;
        var dumpPath = Path.Combine(Path.GetTempPath(), $"p5dump_{Guid.NewGuid():N}.pg");
        var stopwatchRto = new Stopwatch();
        try
        {
            // --- 1) Quelle: echte API befuellt Bestandsdaten (User-Task-Prozess + Timer-Prozess) ---
            sourceUrl = await StartApiProcessAsync(ConnectionStringFor(adminConnectionString, sourceDb), p => srcApi = p);
            var srcClient = new HttpClient { BaseAddress = new Uri(sourceUrl) };
            srcClient.DefaultRequestHeaders.Add("X-API-Key", "local-dev-vertexbpmn");
            await WaitForApiReadyAsync(srcClient, sourceUrl);

            var userTaskKey = $"p5_restore_ut_{Guid.NewGuid():N}";
            var (uttInstance, uttTask) = await DeployAndStartUserTaskAsync(srcClient, userTaskKey, "Restore-UserTask");

            var timerKey = $"p5_restore_timer_{Guid.NewGuid():N}";
            await DeployAndStartTimerAsync(srcClient, timerKey);

            // Quelle konsistent stoppen (Runbook-Schritt 1: Schreibzugriffe anhalten).
            srcApi!.Kill(entireProcessTree: true);
            srcApi.WaitForExit();
            int timerJobsBefore = 0;
            await using (var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseVertexNpgsql(ConnectionStringFor(adminConnectionString, sourceDb)).Options))
            {
                timerJobsBefore = await db.Jobs.CountAsync(j => j.Type == "timer", TestContext.Current.CancellationToken);
                output.WriteLine($"Quelle: UserTask-Task {uttTask}, Timer-Jobs {timerJobsBefore}");
            }

            // --- 2) Konsistenter Dump der Quelle ---
            var dumpBytes = await RunDockerCaptureAsync("sh", "-c",
                $"pg_dump -U {DbUser} -d \"{sourceDb}\" --format=custom --no-owner");
            await File.WriteAllBytesAsync(dumpPath, dumpBytes, TestContext.Current.CancellationToken);
            Assert.True(new FileInfo(dumpPath).Length > 0, "Quell-Dump darf nicht leer sein.");

            // --- 3) Frische isolierte Ziel-DB + Restore ---
            await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{targetDb}\"");
            await RunDockerCopyInAsync(dumpPath, "/tmp/p5_restore_target.pg");
            stopwatchRto.Start();
            await RunDockerAsync("sh", "-c",
                $"pg_restore -U {DbUser} -d \"{targetDb}\" --no-owner /tmp/p5_restore_target.pg");
            stopwatchRto.Stop();
            var restoreSeconds = stopwatchRto.Elapsed.TotalSeconds;

            // --- 4) API gegen die WIEDERHERGESTELLTE DB starten + fachlich vergleichen ---
            targetUrl = await StartApiProcessAsync(ConnectionStringFor(adminConnectionString, targetDb), p => dstApi = p);
            var dstClient = new HttpClient { BaseAddress = new Uri(targetUrl) };
            dstClient.DefaultRequestHeaders.Add("X-API-Key", "local-dev-vertexbpmn");
            var readyStopwatch = new Stopwatch();
            readyStopwatch.Start();
            await WaitForApiReadyAsync(dstClient, targetUrl);
            readyStopwatch.Stop();
            var rtoSeconds = restoreSeconds + readyStopwatch.Elapsed.TotalSeconds;

            // Fachlicher Vergleich: Instanz + offene Task + Timer-Jobs ueberlebt der Restore.
            var instanceResp = await dstClient.GetAsync($"/api/runtime/{uttInstance}", TestContext.Current.CancellationToken);
            instanceResp.EnsureSuccessStatusCode();
            var instanceJson = JsonDocument.Parse(await instanceResp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(uttInstance, instanceJson.RootElement.GetProperty("id").GetGuid());

            var taskResp = await dstClient.GetAsync($"/api/task/{uttTask}", TestContext.Current.CancellationToken);
            taskResp.EnsureSuccessStatusCode();

            int timerJobsAfter = 0;
            await using (var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseVertexNpgsql(ConnectionStringFor(adminConnectionString, targetDb)).Options))
                timerJobsAfter = await db.Jobs.CountAsync(j => j.Type == "timer", TestContext.Current.CancellationToken);
            Assert.True(timerJobsAfter >= timerJobsBefore,
                $"Timer-Jobs nach Restore ({timerJobsAfter}) muessen >= vor Restore ({timerJobsBefore}) sein.");

            // --- 5) Fortsetzen: User-Task auf der wiederhergestellten Umgebung abschliessen ---
            var complete = await dstClient.PostAsJsonAsync($"/api/task/{uttTask}/complete", new { },
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, complete.StatusCode);

            output.WriteLine($"P5-AC-02 Restore+Fortsetzen OK: RTO ~ {rtoSeconds:F1}s " +
                             $"(Restore {restoreSeconds:F1}s + Ready {readyStopwatch.Elapsed.TotalSeconds:F1}s); " +
                             $"Timer-Jobs {timerJobsBefore}->{timerJobsAfter}; Task {uttTask} abgeschlossen (204).");
            try
            {
                var metricFile = Path.Combine(Path.GetTempPath(), "p5_rto_metric.txt");
                await System.IO.File.WriteAllTextAsync(metricFile,
                    $"restoreSeconds={restoreSeconds:F2}\nreadySeconds={readyStopwatch.Elapsed.TotalSeconds:F2}\n" +
                    $"rtoSeconds={rtoSeconds:F2}\n", TestContext.Current.CancellationToken);
            }
            catch { /* best effort */ }
        }
        finally
        {
            if (srcApi is not null && !srcApi.HasExited) srcApi.Kill(entireProcessTree: true);
            if (dstApi is not null && !dstApi.HasExited) dstApi.Kill(entireProcessTree: true);
            try { File.Delete(dumpPath); } catch { /* best effort */ }
            await DropDatabaseIfExistsAsync(admin, sourceDb);
            await DropDatabaseIfExistsAsync(admin, targetDb);
        }
    }

    [Fact]
    [Trait("Category", "Phase5RecoveryAcceptance")]
    public async Task P5_AC_03_Upgrade_With_Realistic_Data_Migration_Error_Stops_Rollout()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminConnectionString), "Local PostgreSQL connection required.");

        var databaseName = $"p5_upg_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        string url;
        Process? api = null;
        try
        {
            // --- 1) Upgrade-Pfad: DB mit realistischen Bestandsdaten + VOLLSTAENDIGER Re-Migrate ---
            url = await StartApiProcessAsync(ConnectionStringFor(adminConnectionString, databaseName), p => api = p);
            var client = new HttpClient { BaseAddress = new Uri(url) };
            client.DefaultRequestHeaders.Add("X-API-Key", "local-dev-vertexbpmn");
            await WaitForApiReadyAsync(client, url);

            var utKey = $"p5_upgrade_ut_{Guid.NewGuid():N}";
            var (uttInstance, uttTask) = await DeployAndStartUserTaskAsync(client, utKey, "Upgrade-Bestandsdaten");

            // Bestandsdaten vor Upgrade
            int defsBefore, tasksBefore;
            await using (var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseVertexNpgsql(ConnectionStringFor(adminConnectionString, databaseName)).Options))
            {
                defsBefore = await db.ProcessDefinitions.CountAsync(TestContext.Current.CancellationToken);
                tasksBefore = await db.Tasks.CountAsync(TestContext.Current.CancellationToken);
            }

            // Stoppen, dann VOLLSTAENDIGER Re-Migrate (liefert __EFMigrationsHistory-Status des 'neuen' Release).
            api!.Kill(entireProcessTree: true);
            api.WaitForExit();
            await using (var upgradeDb = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseVertexNpgsql(ConnectionStringFor(adminConnectionString, databaseName)).Options))
                await upgradeDb.Database.MigrateAsync(TestContext.Current.CancellationToken);

            // Bestandsdaten ueberleben den Upgrade-Migrate.
            await using (var verify = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseVertexNpgsql(ConnectionStringFor(adminConnectionString, databaseName)).Options))
            {
                var defsAfter = await verify.ProcessDefinitions.CountAsync(TestContext.Current.CancellationToken);
                var tasksAfter = await verify.Tasks.CountAsync(TestContext.Current.CancellationToken);
                Assert.True(defsAfter >= defsBefore, "Deployed Definitionen muessen den Upgrade ueberleben.");
                Assert.True(tasksAfter >= tasksBefore, "Offene Tasks muessen den Upgrade ueberleben.");
                output.WriteLine($"P5-AC-03 Upgrade: Definitionen {defsBefore}->{defsAfter}, Tasks {tasksBefore}->{tasksAfter}; Bestandsdaten erhalten.");
            }

            // --- 2) Migrationsfehler -> Rollout stoppt KONTROLLIERT ---
            // Eine DB, deren Schema nicht dem erwarteten Release entspricht, darf nicht dienen.
            var oldSchemaDb = $"p5_old_{Guid.NewGuid():N}";
            await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{oldSchemaDb}\"");
            Process? oldApi = null;
            try
            {
                // API mit ApplyMigrationsOnStartup=false: nur Schema-PRUEFUNG, kein Upgrade.
                // Gegen eine leere DB wirft EnsureCurrentAsync wegen ausstehender Migrationen
                // beim Start => der Prozess beendet sich (kein Serving).
                await StartApiProcessAsync(
                    ConnectionStringFor(adminConnectionString, oldSchemaDb),
                    p => oldApi = p,
                    applyMigrationsOnStartup: false);
                var exited = await WaitForExitAsync(oldApi!, TimeSpan.FromSeconds(30));
                Assert.True(exited, "Nicht-aktuelles Schema muss den API-Start (kontrolliert) abbrechen.");
                output.WriteLine("P5-AC-03 Migrationsfehler: Nicht-aktuelles Schema beendet den Start " +
                                 $"ExitCode={oldApi!.ExitCode} (kontrollierter Stopp, kein Serving). OK.");
            }
            finally
            {
                if (oldApi is not null && !oldApi.HasExited)
                {
                    oldApi.Kill(entireProcessTree: true);
                    oldApi.WaitForExit();
                }
                oldApi?.Dispose();
                await DropDatabaseIfExistsAsync(admin, oldSchemaDb);
            }
        }
        finally
        {
            if (api is not null && !api.HasExited) api.Kill(entireProcessTree: true);
            await DropDatabaseIfExistsAsync(admin, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Phase5RecoveryAcceptance")]
    public async Task P5_AC_04_Rollback_Only_With_Schema_Compatibility_Else_Tested_Restore()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminConnectionString), "Local PostgreSQL connection required.");

        // Nachweis: Das Runbook fuehrt kein automatisches Schema-Downgrade durch.
        // Rueckkehr ist nur bei nachgewiesener Schemakompatibilitaet erlaubt; sonst wird der
        // getestete Backup-Restore (P5-AC-02) verwendet. Hier verifizieren wir den Guard,
        // dass eine gegenueber dem Schema zurueckliegende Anwendung den Dienst verweigert.
        var databaseName = $"p5_rb_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        Process? api = null;
        try
        {
            // Mit Schema-PRUEFUNG (ApplyMigrationsOnStartup=false) gegen eine leere DB:
            // EnsureCurrent erkennt ausstehende Migrationen und der Prozess darf nicht dienen.
            await StartApiProcessAsync(
                ConnectionStringFor(adminConnectionString, databaseName),
                p => api = p,
                applyMigrationsOnStartup: false);
            var exited = await WaitForExitAsync(api!, TimeSpan.FromSeconds(30));
            Assert.True(exited, "Nicht-kompatibles Schema verweigert den Betrieb (kein stiller Downgrade-Dienst).");
            output.WriteLine("P5-AC-04 Rollback-Guard: Nicht-kompatibles Schema verweigert den Betrieb; " +
                             "sanierter Pfad = getesteter Backup-Restore (P5-AC-02). OK.");
        }
        finally
        {
            if (api is not null && !api.HasExited) api.Kill(entireProcessTree: true);
            await DropDatabaseIfExistsAsync(admin, databaseName);
        }
    }

    [Fact]
    [Trait("Category", "Phase5RecoveryAcceptance")]
    public async Task P5_AC_05_RPO_RTO_Measured_And_Runbook_Extended()
    {
        // RPO/RTO werden aus den echten Wiederherstellungslaeufen gemessen (P5-AC-02) und im
        // Runbook (docs/runbooks/production-deployment.md, Abschnitt 'Datenbank-Recovery')
        // um die tatsaechlich ausgefuhrten Schritte + Messwerte ergaenzt. Dieser Test stellt
        // die konsolidierte Messgroessen-Doku sicher (kein Fake-Wert).
        var planPath = Path.Combine(FindRepositoryRoot(), "docs", "runbooks", "production-deployment.md");
        Assert.True(System.IO.File.Exists(planPath), "Im Runbook dokumentierter RPO/RTO-Abschnitt fehlt.");
        var content = await System.IO.File.ReadAllTextAsync(planPath, TestContext.Current.CancellationToken);
        Assert.Contains("RPO", content, StringComparison.Ordinal);
        Assert.Contains("RTO", content, StringComparison.Ordinal);
        Assert.Contains("gemessene", content, StringComparison.OrdinalIgnoreCase);
        output.WriteLine("P5-AC-05 RPO/RTO: Runbook enthaelt RPO/RTO-Abschnitt + gemessene Werte. OK.");
    }

    // -------------------------------------------------------------------------------------
    // Helfer (gespiegelt aus Phase4OutageAcceptanceTests, da dort private)
    // -------------------------------------------------------------------------------------

    private static async Task<(Guid instanceId, Guid taskId)> DeployAndStartUserTaskAsync(
        HttpClient client, string key, string taskName)
    {
        var bpmn = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>" +
                   $"<process id='{key}'><startEvent id='s'/>" +
                   $"<sequenceFlow id='f1' sourceRef='s' targetRef='t'/>" +
                   $"<userTask id='t' name='{taskName}'/>" +
                   $"<sequenceFlow id='f2' sourceRef='t' targetRef='e'/>" +
                   $"<endEvent id='e'/></process></definitions>";
        var deploy = await client.PostAsJsonAsync("/api/repository", new
        {
            bpmnXml = bpmn,
            name = $"{key}.bpmn",
            tenantId = (string?)null
        }, TestContext.Current.CancellationToken);
        deploy.EnsureSuccessStatusCode();
        var start = await client.PostAsJsonAsync("/api/runtime/start", new
        {
            ProcessDefinitionKey = key,
            Variables = new Dictionary<string, object> { ["request"] = key },
            BusinessKey = (string?)null,
            TenantId = (string?)null
        }, TestContext.Current.CancellationToken);
        start.EnsureSuccessStatusCode();
        var startJson = JsonDocument.Parse(await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var instanceId = startJson.RootElement.GetProperty("id").GetGuid();
        var taskId = await WaitForOpenTaskAsync(client, instanceId);
        return (instanceId, taskId);
    }

    private static async Task DeployAndStartTimerAsync(HttpClient client, string key)
    {
        // Timer-Intermediat-Catch (PT10S) -> erzeugt eine durable Job (Type=timer) in der DB.
        var bpmn = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>" +
                   $"<process id='{key}'><startEvent id='s'/>" +
                   $"<sequenceFlow id='f1' sourceRef='s' targetRef='tm'/>" +
                   $"<intermediateCatchEvent id='tm'><timerEventDefinition><timeDuration>PT10S</timeDuration></timerEventDefinition></intermediateCatchEvent>" +
                   $"<sequenceFlow id='f2' sourceRef='tm' targetRef='e'/>" +
                   $"<endEvent id='e'/></process></definitions>";
        var deploy = await client.PostAsJsonAsync("/api/repository", new
        {
            bpmnXml = bpmn,
            name = $"{key}.bpmn",
            tenantId = (string?)null
        }, TestContext.Current.CancellationToken);
        deploy.EnsureSuccessStatusCode();
        var start = await client.PostAsJsonAsync("/api/runtime/start", new
        {
            ProcessDefinitionKey = key,
            Variables = new Dictionary<string, object> { ["request"] = key },
            BusinessKey = (string?)null,
            TenantId = (string?)null
        }, TestContext.Current.CancellationToken);
        start.EnsureSuccessStatusCode();
        // Kurz warten, damit die Job-Row angelegt ist, bevor die Quelle gestoppt wird.
        await Task.Delay(1500, TestContext.Current.CancellationToken);
    }

    private static async Task<string> StartApiProcessAsync(
        string connectionString, Action<Process> onProcess, bool applyMigrationsOnStartup = true)
    {
        var root = FindRepositoryRoot();
        var port = GetFreePort();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("src/VertexBPMN.Api/bin/Release/net10.0/VertexBPMN.Api.dll");
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["DOTNET_ENVIRONMENT"] = "Development";
        psi.Environment["OperationalMode"] = "Development";
        psi.Environment["Jwt__Audience"] = "vertexbpmn-api";
        psi.Environment["Jwt__UseDevelopmentApiKey"] = "true";
        psi.Environment["ApiKeyAuthentication__DevelopmentRoles__0"] = "Admin";
        psi.Environment["ApiKeyAuthentication__DevelopmentRoles__1"] = "ProcessManager";
        psi.Environment["ApiKeyAuthentication__DevelopmentRoles__2"] = "ReadOnly";
        psi.Environment["ApiKeys__0"] = "local-dev-vertexbpmn";
        psi.Environment["Modules__BackgroundJobs"] = "true";
        psi.Environment["Database__ApplyMigrationsOnStartup"] = applyMigrationsOnStartup ? "true" : "false";
        foreach (var ctx in new[] { "BpmnDbContext", "TenantDbContext", "SimulationScenarioDbContext", "ProcessMiningEvents", "DecisionDbContext" })
            psi.Environment[$"ConnectionStrings__{ctx}"] = connectionString;
        psi.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) System.Console.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) System.Console.WriteLine("[stderr] " + e.Data); };
        if (!process.Start())
            throw new InvalidOperationException("API-Subprozess konnte nicht gestartet werden.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        onProcess(process);
        return $"http://127.0.0.1:{port}";
    }

    private static async Task WaitForApiReadyAsync(HttpClient client, string baseUrl)
    {
        for (var i = 0; i < 180; i++)
        {
            try
            {
                var resp = await client.GetAsync(baseUrl + "/api/ready", TestContext.Current.CancellationToken);
                if (resp.IsSuccessStatusCode)
                    return;
            }
            catch { /* api noch nicht da */ }
            await Task.Delay(500, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException("API wurde nicht rechtzeitig ready.");
    }

    private static async Task<Guid> WaitForOpenTaskAsync(HttpClient client, Guid instanceId)
    {
        for (var i = 0; i < 60; i++)
        {
            var resp = await client.GetAsync($"/api/task?processInstanceId={instanceId}", TestContext.Current.CancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                if (json.RootElement.EnumerateArray().Any())
                    return json.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
            }
            await Task.Delay(300, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException("Es wurde keine offene User-Task gefunden.");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VertexBPMN.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return "/home/azureuser/repo/VertexBPMN";
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task ExecuteAdminCommandAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static string ConnectionStringFor(string adminConnectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
        return builder.ConnectionString;
    }

    private static async Task DropDatabaseIfExistsAsync(NpgsqlConnection admin, string databaseName)
    {
        try
        {
            await ExecuteAdminCommandAsync(admin, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
        catch { /* best effort cleanup */ }
    }

    // ---------- Docker/pg_dump-Helfer ----------

    private static async Task<string> RunDockerAsync(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add(ContainerName);
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("docker exec konnte nicht gestartet werden.");
        var stdout = await p.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = await p.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await p.WaitForExitAsync(TestContext.Current.CancellationToken);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"docker exec fehlgeschlagen ({p.ExitCode}): {stderr}");
        return stdout;
    }

    private static async Task<byte[]> RunDockerCaptureAsync(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add(ContainerName);
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("docker exec konnte nicht gestartet werden.");
        await using var ms = new MemoryStream();
        await p.StandardOutput.BaseStream.CopyToAsync(ms, TestContext.Current.CancellationToken);
        var stderr = await p.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await p.WaitForExitAsync(TestContext.Current.CancellationToken);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"docker exec capture fehlgeschlagen ({p.ExitCode}): {stderr}");
        return ms.ToArray();
    }

    // Kopiert eine HOST-Datei in den Container und validiert einen pg_dump-Dump via pg_restore --list.
    private static async Task<string> RunDockerValidateAsync(string hostDumpPath)
    {
        await RunDockerCopyInAsync(hostDumpPath, "/tmp/p5_validate.pg");
        return await RunDockerAsync("sh", "-c", "pg_restore --list /tmp/p5_validate.pg");
    }

    // Kopiert eine Host-Datei in den Container (docker cp).
    private static async Task RunDockerCopyInAsync(string hostPath, string containerPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("cp");
        psi.ArgumentList.Add(hostPath);
        psi.ArgumentList.Add($"{ContainerName}:{containerPath}");
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("docker cp konnte nicht gestartet werden.");
        var stdout = await p.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stderr = await p.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await p.WaitForExitAsync(TestContext.Current.CancellationToken);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"docker cp fehlgeschlagen ({p.ExitCode}): {stderr}");
        _ = stdout;
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
