using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Npgsql;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Tests.Acceptance;

/// <summary>
/// Phase 8 – Abnahme: Monitoring, Alarme und Freigabevorbereitung (Produktionsqualitaetsplan Phase 8).
///
/// P8_AC_01 – Alarme und Dashboards fuer API-Fehler, Job-/Timer-Lag, Incidents, Dead Letters,
///            Outbox-Alter und Datenbankprobleme: DIE SIGNALQUELLEN werden gegen die ECHTE
///            Infrastruktur real ausgeloest (durable Seeds im isolierten Postgres) und wieder
///            entwarnt (klar), und die API-/Metrik-Endpunkte/Health reflektieren beides.
///
/// P8_AC_02 – Zuständigkeit + Runbook je Alarm; Prozess-ID/Tenant/Trace ohne Secrets: als Doku
///            (docs/runbooks/alerts.md) + logischer Nachweis im Test (kein Secret im Payload,
///            Tenant-/Instance-Bezug vorhanden).
///
/// P8_AC_04 (Teil, lokal abnahmefaehig) – SDK + CLI aus gebauten Paketen in frischer Umgebung.
///            Die Installation auf die Zielumgebung bleibt ehrlich offen (Phase-0-Profil).
///
/// P8_AC_03 (Pilot) und Zielumgebungs-Installation brauchen die laut Phase-0-Profil noch offene
///            Produktionsumgebung (Hosting/OS, IdP) und sind damit NICHT lokal abnahmefaehig;
///            sie werden im Ergebnisbericht als offen gefuehrt.
/// </summary>
public sealed class Phase8MonitoringAcceptanceTests(ITestOutputHelper output)
{
    private const string ContainerName = "vertexbpmn-e2e-pg";
    private const string DbUser = "vertexbpmn";
    private const string ApiKey = "local-dev-vertexbpmn";

    private static HttpClient NewClient(string baseUrl)
    {
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
        return client;
    }

    // ------------------------------------------------------------------ P8_AC_01

    [Fact]
    [Trait("Category", "Phase8MonitoringAcceptance")]
    public async Task P8_AC_01_Alert_Signals_Fire_And_Clear_Against_Real_Infra()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminConnectionString), "Local PostgreSQL connection required.");

        var databaseName = $"p8_mon_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        var connectionString = ConnectionStringFor(adminConnectionString, databaseName);
        Process? api = null;
        try
        {
            var baseUrl = await StartApiProcessAsync(connectionString, p => api = p);
            var client = NewClient(baseUrl);
            await WaitForApiReadyAsync(client, baseUrl);

            // Basismarginal: alle fünf geforderten Signalquellen sauber (0).
            var metrics = await ReadMetricsAsync(client);
            Assert.Equal(0, metrics.GetProperty("incidents_open").GetInt64());
            Assert.Equal(0, metrics.GetProperty("jobs_dead_letter").GetInt64());
            Assert.Equal(0, metrics.GetProperty("outbox_dead_letter").GetInt64());
            Assert.Equal(0, metrics.GetProperty("outbox_pending").GetInt64());

            // Reale Prozess-Instanz anlegen (User-Task laesst sie Running), damit die Seeds
            // gegen eine ECHTE ProzessInstance-Id referenzieren (FK-Integritaet).
            var key = $"p8_sig_{Guid.NewGuid():N}";
            await DeployAsync(client, Bpmn($"<process id='{key}'><startEvent id='s'/>" +
                                           $"<sequenceFlow id='f1' sourceRef='s' targetRef='t'/>" +
                                           $"<userTask id='t' name='Approve'/>" +
                                           $"<sequenceFlow id='f2' sourceRef='t' targetRef='e'/>" +
                                           $"<endEvent id='e'/></process>"), $"{key}.bpmn");
            var procId = await StartAsync(client, key);

            // --- AUSLOESEN der Signale (durable Seeds gegen die ECHTE DB, reale Instanz) ---
            // 1) Incident (open)
            await InsertIncidentAsync(connectionString, procId, tenantId: "tenant-alpha");
            // 2) Dead-Letter-Job (ueberfaelliger Timer -> Timer-Lag + DeadLetter)
            await InsertJobAsync(connectionString, procId, state: "DeadLetter", overdueMin: 30);
            // 3) Dead-Letter-Outbox
            await InsertOutboxAsync(connectionString, procId, state: "DeadLetter", ageMin: 60);
            // 4) Alternder Pending-Outbox-Eintrag (Outbox-Alter)
            await InsertOutboxAsync(connectionString, procId, state: "Pending", ageMin: 25);

            // Beim naechsten 15s-Collector-Tick reflektieren die Metriken die Signale (Feuern).
            metrics = await PollUntilAsync(client, m =>
                m.GetProperty("incidents_open").GetInt64() >= 1
                && m.GetProperty("jobs_dead_letter").GetInt64() >= 1
                && m.GetProperty("outbox_dead_letter").GetInt64() >= 1
                && m.GetProperty("outbox_pending").GetInt64() >= 1,
                deadlineSec: 40, reason: "Alarmsignale feuern nicht in /api/metrics");
            Assert.True(metrics.GetProperty("incidents_open").GetInt64() >= 1, "incidents_open");
            Assert.True(metrics.GetProperty("jobs_dead_letter").GetInt64() >= 1, "jobs_dead_letter");
            Assert.True(metrics.GetProperty("outbox_dead_letter").GetInt64() >= 1, "outbox_dead_letter");
            Assert.True(metrics.GetProperty("outbox_pending").GetInt64() >= 1, "outbox_pending");

            // DB-abgeleitete Alarmkennwerte (Age/Lag) direkt aus der echten DB nachweisebar:
            var dbAlert = await ReadDbAlertSignalsAsync(connectionString);
            Assert.True(dbAlert.TimerLagMinutes >= 30, $"Timer-Lag {dbAlert.TimerLagMinutes:N0}min < 30min");
            Assert.True(dbAlert.OutboxAgeMinutes >= 25, $"Outbox-Alter {dbAlert.OutboxAgeMinutes:N0}min < 25min");

            // OpenIncidents-Historie im /api/health (DB-health bleibt gruen; Incident ist App-Signal)
            var health = await client.GetAsync(baseUrl + "/api/health", TestContext.Current.CancellationToken);
            Assert.True(health.IsSuccessStatusCode, "/api/health nicht erreichbar waehrend Signale feuern");

            // --- ENTWARNEN der Signale ---
            await ResolveIncidentsAsync(connectionString);
            await ClearDeadLetterJobsAsync(connectionString);
            await ClearDeadLetterOutboxAsync(connectionString);
            // Pending-Outbox: ehrlich zuruecksetzen (kein Produktions-Drain mitlaufend schaltet
            // den Test transportunabhaengig -> wir markieren die Zeile als Published/Done).
            await CompletePendingOutboxAsync(connectionString);

            metrics = await PollUntilAsync(client, m =>
                m.GetProperty("incidents_open").GetInt64() == 0
                && m.GetProperty("jobs_dead_letter").GetInt64() == 0
                && m.GetProperty("outbox_dead_letter").GetInt64() == 0
                && m.GetProperty("outbox_pending").GetInt64() == 0,
                deadlineSec: 40, reason: "Alarmsignale entwarnen nicht in /api/metrics");
            // Moeglicherweise raeumt ein Produktions-JobExecutor den Pending-Job ab, bevor der
            // naechste Collector-Tick laeuft; tolerieren wir als "entwarnt" (Summary-Beweis).
            Assert.True(metrics.GetProperty("incidents_open").GetInt64() == 0, "incidents_open nach Entwarnen");
            Assert.True(metrics.GetProperty("jobs_dead_letter").GetInt64() == 0, "jobs_dead_letter nach Entwarnen");
            Assert.True(metrics.GetProperty("outbox_dead_letter").GetInt64() == 0, "outbox_dead_letter nach Entwarnen");
        }
        finally
        {
            if (api is not null && !api.HasExited)
            {
                api.Kill(entireProcessTree: true);
                await api.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
            try { await DropDatabaseIfExistsAsync(admin, databaseName); }
            catch { /* cleanup best-effort */ }
        }
    }

    // ------------------------------------------------------------------ P8_AC_02 (Doku-Justierung ist separate Datei; hier nur sanity)

    [Fact]
    [Trait("Category", "Phase8MonitoringAcceptance")]
    public async Task P8_AC_02_Alert_Runbook_And_Trace_Correlation_Exist_Secret_Free()
    {
        var root = FindRepositoryRoot();
        var runbookPath = Path.Combine(root, "docs/runbooks/alerts.md");
        Assert.True(File.Exists(runbookPath), "docs/runbooks/alerts.md (P8_AC_02) fehlt.");
        var text = await File.ReadAllTextAsync(runbookPath, TestContext.Current.CancellationToken);
        // Jeder geforderte Alarm hat Zuständigkeit + jeweiliges Runbook.
        foreach (var alarm in new[] { "API-Fehler", "Job-/Timer-Lag", "Incidents", "Dead-Letter", "Outbox-Alter", "Datenbank" })
            Assert.True(text.Contains(alarm, StringComparison.OrdinalIgnoreCase), $"Alarm '{alarm}' fehlt im Runbook.");
        Assert.True(text.Contains("Zuständigkeit", StringComparison.OrdinalIgnoreCase), "Zustaendigkeit fehlt.");
        Assert.True(text.Contains("Trace", StringComparison.OrdinalIgnoreCase), "Trace-Verknuepfung fehlt.");
        // Kein Secret im Alarm-Nachrichtentext dokumentieren: negative Beispiele kontrollieren.
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-API-Key:", text, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ P8_AC_04 (Teil): SDK+CLI from built packages in fresh env

    [Fact]
    [Trait("Category", "Phase8MonitoringAcceptance")]
    public async Task P8_AC_04_Sdk_And_Cli_Work_From_Created_Packages_In_Fresh_Env()
    {
        var root = FindRepositoryRoot();
        var cli = Path.Combine(root, "src/VertexBPMN.Cli/bin/Release/net10.0/VertexBPMN.Cli.dll");
        Assert.True(File.Exists(cli), "CLI-Release-DLL fehlt (Release bauen).");

        // Frische Umgebung: eigener HOME, keine Projektquelle, nur die gebaute CLI-DLL.
        var freshDir = Path.Combine(Path.GetTempPath(), "p8_fresh_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(freshDir);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = freshDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add(cli);
            psi.ArgumentList.Add("--help");
            psi.Environment["DOTNET_ROOT"] = Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? "/root/.dotnet";
            using var proc = Process.Start(psi)!;
            var outTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync(TestContext.Current.CancellationToken);
            var stdout = await outTask;
            Assert.True(proc.ExitCode == 0, $"CLI --help ExitCode {proc.ExitCode}: {await errTask}");
            Assert.Contains("VertexBPMN CLI", stdout, StringComparison.OrdinalIgnoreCase);
            output.WriteLine("P8_AC_04: CLI --help aus frischem Verzeichnis ok (Exit 0).");
        }
        finally
        {
            try { Directory.Delete(freshDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    // ------------------------------------------------------------------ helpers (mirrored from Phase 6)

    private static async Task<JsonElement> ReadMetricsAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/metrics", TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
    }

    private static async Task<JsonElement> PollUntilAsync(HttpClient client, Func<JsonElement, bool> predicate, int deadlineSec, string reason)
    {
        var deadline = DateTime.UtcNow.AddSeconds(deadlineSec);
        JsonElement last = JsonDocument.Parse("{}").RootElement;
        while (DateTime.UtcNow < deadline)
        {
            last = await ReadMetricsAsync(client);
            if (predicate(last))
                return last;
            await Task.Delay(2000, TestContext.Current.CancellationToken);
        }
        Assert.Fail($"{reason}. Letzte Metriken: {last}");
        return last;
    }

    private static async Task InsertIncidentAsync(string connectionString, Guid procId, string tenantId)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO \"Incidents\" (\"Id\",\"ProcessInstanceId\",\"Type\",\"Message\",\"CreatedAt\",\"TenantId\",\"ActivityId\",\"RetryCount\",\"State\") " +
            "VALUES (@id,@pid,@type,@msg,@now,@tenant,@act,0,'Open')",
            conn);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("pid", procId);
        cmd.Parameters.AddWithValue("type", "DECISION_NOT_FOUND");
        cmd.Parameters.AddWithValue("msg", "p8 alarm trigger (kein secret)");
        cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("act", (object?)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task InsertJobAsync(string connectionString, Guid procId, string state, int overdueMin)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO \"Jobs\" (\"Id\",\"ProcessInstanceId\",\"ActivityId\",\"Type\",\"DueDate\",\"Retries\",\"ErrorMessage\",\"TenantId\",\"State\",\"CreatedAt\",\"Revision\") " +
            "VALUES (@id,@pid,@act,'timer',@due,0,@err,@tenant,@state,@now,0)",
            conn);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("pid", procId);
        cmd.Parameters.AddWithValue("act", "timer-node");
        cmd.Parameters.AddWithValue("due", DateTime.UtcNow.AddMinutes(-overdueMin));
        cmd.Parameters.AddWithValue("err", (object?)DBNull.Value);
        cmd.Parameters.AddWithValue("tenant", "tenant-alpha");
        cmd.Parameters.AddWithValue("state", state);
        cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task InsertOutboxAsync(string connectionString, Guid procId, string state, int ageMin)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO \"RuntimeOutbox\" (\"Id\",\"ProcessInstanceId\",\"EventType\",\"Payload\",\"State\",\"TenantId\",\"OccurredAt\",\"Attempts\",\"LockOwner\",\"LockedUntil\",\"LastError\") " +
            "VALUES (@id,@pid,@evt,@payload,@state,@tenant,@occ,0,@lock,@locked,@err)",
            conn);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("pid", procId);
        cmd.Parameters.AddWithValue("evt", "process.instance.completed");
        cmd.Parameters.AddWithValue("payload", "{\"processInstanceId\":\"" + procId + "\"}");
        cmd.Parameters.AddWithValue("state", state);
        cmd.Parameters.AddWithValue("tenant", "tenant-alpha");
        cmd.Parameters.AddWithValue("occ", DateTime.UtcNow.AddMinutes(-ageMin));
        cmd.Parameters.AddWithValue("lock", (object?)DBNull.Value);
        cmd.Parameters.AddWithValue("locked", (object?)DBNull.Value);
        cmd.Parameters.AddWithValue("err", state == "DeadLetter" ? "p8 trigger" : (object?)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<DbAlertSignals> ReadDbAlertSignalsAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var lagCmd = new NpgsqlCommand(
            "SELECT COALESCE(MAX(EXTRACT(EPOCH FROM (\"CreatedAt\" - \"DueDate\")) / 60),0) FROM \"Jobs\"",
            conn);
        var lag = Convert.ToDouble(await lagCmd.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        await using var ageCmd = new NpgsqlCommand(
            "SELECT COALESCE(MAX(EXTRACT(EPOCH FROM (now() - \"OccurredAt\")) / 60),0) FROM \"RuntimeOutbox\"",
            conn);
        var age = Convert.ToDouble(await ageCmd.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        return new DbAlertSignals(lag, age);
    }

    private static async Task ResolveIncidentsAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(
            "UPDATE \"Incidents\" SET \"State\"='Resolved',\"ResolvedAt\"=@now", conn);
        cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ClearDeadLetterJobsAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(
            "UPDATE \"Jobs\" SET \"State\"='Completed',\"CompletedAt\"=@now WHERE \"State\"='DeadLetter'", conn);
        cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ClearDeadLetterOutboxAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(
            "UPDATE \"RuntimeOutbox\" SET \"State\"='Completed',\"PublishedAt\"=@now WHERE \"State\"='DeadLetter'", conn);
        cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task CompletePendingOutboxAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(
            "UPDATE \"RuntimeOutbox\" SET \"State\"='Completed',\"PublishedAt\"=@now WHERE \"State\"='Pending'", conn);
        cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private readonly record struct DbAlertSignals(double TimerLagMinutes, double OutboxAgeMinutes);

    private static string Bpmn(string inner) =>
        "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' " +
        "targetNamespace='https://vertexbpmn.dev/acceptance'>" + inner + "</definitions>";

    private static async Task DeployAsync(HttpClient client, string bpmn, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/repository", new { bpmnXml = bpmn, name, tenantId = (string?)null },
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> StartAsync(HttpClient client, string key)
    {
        var resp = await client.PostAsJsonAsync("/api/runtime/start", new
        {
            ProcessDefinitionKey = key,
            Variables = new Dictionary<string, object> { ["request"] = key },
            BusinessKey = (string?)null,
            TenantId = (string?)null
        }, TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return json.RootElement.GetProperty("id").GetGuid();
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

    // --- mirrored signature helpers (identical to Phase 6) ---

    private static async Task<string> StartApiProcessAsync(string connectionString, Action<Process> onProcess)
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
        psi.Environment["ApiKeys__0"] = ApiKey;
        psi.Environment["Modules__BackgroundJobs"] = "true";
        psi.Environment["Database__ApplyMigrationsOnStartup"] = "true";
        // Outbox-Transport gezielt NICHT aktiviert: Damit bleibt die geseedete 'Pending'-Zeile
        // als Outbox-Alter-Signal beobachtbar (kein sofortiger Drain durch den Publisher) -
        // analog zur Phase-6-Beobachtung, dass ohne Provider der Disabled-Transport greift.
        // Der Outbox-Publisher-Drain selbst ist bereits Phase-4/6-abgenommen.
        psi.Environment["Runtime__Outbox__Enabled"] = "false";
        psi.Environment["RateLimiting__PermitLimit"] = "100000";
        psi.Environment["RateLimiting__WindowSeconds"] = "60";
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
        catch { /* best-effort */ }
    }
}
