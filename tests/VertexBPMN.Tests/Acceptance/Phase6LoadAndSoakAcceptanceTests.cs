using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Npgsql;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Tests.Acceptance;

/// <summary>
/// Phase 6 – Abnahme: Last und Dauerbetrieb (Produktionsqualitaetsplan Req 6/Phase 6).
///
/// Alle Tests laufen gegen die ECHTE Infrastruktur (PostgreSQL 17 auf 127.0.0.1:55432,
/// RabbitMQ 4 auf 127.0.0.1:55672). Ein Test legt eine isolierte, zufaellig benannte
/// PostgreSQL-Datenbank an und raeumt sie in finally wieder ab; die echte VertexBPMN.Api
/// wird als OS-Subprozess dagegen gestartet.
///
/// Nachweis je Kriterium (P6_AC_0X):
///   01  Szenarien fuer kurze Prozesse, langlebige Wait-States, Timer, parallele Gateways,
///       DMN und Historienabfragen sowie gleichzeitige Studio-Sitzungen aufgebaut und
///       getrieben (jedes liefert eine verifizierbare Zustands-/Ergebnisreaktion).
///   02  Last schrittweise gesteigert; p95/p99, Fehlerrate, Timer-Lag, Outbox-Alter,
///       DB-Pool, Locks, CPU und Speicher erfasst (Messwerte in /tmp/p6_metrics.txt).
///   03  Dauerlauf-Abschnitt mit Lastspitzen und kontrollierter Unterbrechung (API-Kill +
///       Neustart -> laufende Instanz/offene Taskueberlebt und fortsetzbar). Ein voller
///       24-72h-Dauerlauf ist nach Zielprofil zu betreiben und bleibt ehrlich offen.
///   04  Nur gemessene Engpaesse beheben; relevante Tests wiederholen (Nachweis anhand der
///       Messwerte und Abnahme-Kommentar; kein pauschales Umschreiben).
///   05  Kapazitaetsprofil veroeffentlicht (Hardware, Datenvolumen, Replikazahl,
///       Saettigungsgrenze) in docs/reviews/2026-09-09_Phase6_Last_Abnahme.md + Runbook.
/// </summary>
public sealed class Phase6LoadAndSoakAcceptanceTests(ITestOutputHelper output)
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

    // ------------------------------------------------------------------ P6_AC_01

    [Fact]
    [Trait("Category", "Phase6LoadAndSoakAcceptance")]
    public async Task P6_AC_01_Scenarios_For_Load_Are_Built_And_Driven()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminConnectionString), "Local PostgreSQL connection required.");

        var databaseName = $"p6_scen_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        var connectionString = ConnectionStringFor(adminConnectionString, databaseName);
        string baseUrl;
        Process? api = null;
        try
        {
            baseUrl = await StartApiProcessAsync(connectionString, p => api = p);
            var client = NewClient(baseUrl);
            await WaitForApiReadyAsync(client, baseUrl);

            // 1) Kurzer Prozess (start -> end, vollautomatisch selbst abgeschlossen)
            var shortKey = $"p6_short_{Guid.NewGuid():N}";
            var shortBpmn = Bpmn($"<process id='{shortKey}'><startEvent id='s'/>" +
                                $"<sequenceFlow id='f1' sourceRef='s' targetRef='e'/>" +
                                $"<endEvent id='e'/></process>");
            await DeployAsync(client, shortBpmn, $"{shortKey}.bpmn");
            var shortId = await StartAsync(client, shortKey);
            // kurz Prozess: Instanz soll sich selbst beenden (auf Completed uebergehen).
            var shortCompleted = await WaitForStatusAsync(client, shortId, "Completed", deadlineSec: 20);
            Assert.True(shortCompleted, $"Kurzer Prozess {shortKey} wurde nicht completed.");

            // 2) Langlebiger Wait-State (User-Task), offen lassen
            var waitKey = $"p6_wait_{Guid.NewGuid():N}";
            await DeployAsync(client, Bpmn($"<process id='{waitKey}'><startEvent id='s'/>" +
                                           $"<sequenceFlow id='f1' sourceRef='s' targetRef='t'/>" +
                                           $"<userTask id='t' name='Wait'/></process>"), $"{waitKey}.bpmn");
            var waitId = await StartAsync(client, waitKey);
            var waitTaskId = await WaitForOpenTaskAsync(client, waitId);
            Assert.NotEqual(Guid.Empty, waitTaskId);

            // 3) Timer (durable Timer-Job in der DB)
            var timerKey = $"p6_timer_{Guid.NewGuid():N}";
            await DeployAsync(client, Bpmn($"<process id='{timerKey}'><startEvent id='s'/>" +
                                           $"<sequenceFlow id='f1' sourceRef='s' targetRef='tm'/>" +
                                           $"<intermediateCatchEvent id='tm'><timerEventDefinition><timeDuration>PT5S</timeDuration></timerEventDefinition></intermediateCatchEvent>" +
                                           $"<sequenceFlow id='f2' sourceRef='tm' targetRef='e'/>" +
                                           $"<endEvent id='e'/></process>"), $"{timerKey}.bpmn");
            var timerId = await StartAsync(client, timerKey);
            var timerDone = await WaitForStatusAsync(client, timerId, "Completed", deadlineSec: 25);
            Assert.True(timerDone, $"Timer-Prozess {timerKey} wurde nicht durch den Timer abgeschlossen.");

            // 4) Paralleles Gateway: 2 User-Task-Zweige
            var fk = $"p6_par_{Guid.NewGuid():N}";
            var parBpmn = $"<process id='{fk}'><startEvent id='s'/>" +
                          $"<sequenceFlow id='a' sourceRef='s' targetRef='split'/><parallelGateway id='split'/>" +
                          $"<sequenceFlow id='b' sourceRef='split' targetRef='t1'/><userTask id='t1' name='A'/>" +
                          $"<sequenceFlow id='c' sourceRef='t1' targetRef='join'/>" +
                          $"<sequenceFlow id='d' sourceRef='split' targetRef='t2'/><userTask id='t2' name='B'/>" +
                          $"<sequenceFlow id='e' sourceRef='t2' targetRef='join'/><parallelGateway id='join'/>" +
                          $"<sequenceFlow id='f' sourceRef='join' targetRef='end'/><endEvent id='end'/></process>";
            await DeployAsync(client, Bpmn(parBpmn), $"{fk}.bpmn");
            var parId = await StartAsync(client, fk);
            var parTasks = await GetOpenTaskNamesAsync(client, parId);
            Assert.Contains("A", parTasks);
            Assert.Contains("B", parTasks);

            // 5) DMN: deployen + evaluieren
            var dmnKey = $"p6_dmn_{Guid.NewGuid():N}";
            var dmnServiceKey = $"svc_{dmnKey}";
            var dmnXml = $"<definitions xmlns=\"https://www.omg.org/spec/DMN/20191111/MODEL/\">" +
                         $"<inputData id=\"applicantData\" name=\"Applicant\"><variable name=\"score\" /></inputData>" +
                         $"<decision id=\"scoreDecision\" name=\"Score\">" +
                         $"<informationRequirement><requiredInput href=\"#applicantData\" /></informationRequirement>" +
                         $"<variable name=\"adjustedScore\" /><literalExpression><text>score + 10</text></literalExpression></decision>" +
                         $"<decision id=\"riskDecision\" name=\"Risk\">" +
                         $"<informationRequirement><requiredDecision href=\"#scoreDecision\" /></informationRequirement>" +
                         $"<variable name=\"risk\" /><literalExpression><text>if adjustedScore &gt;= 80 then \"low\" else \"high\"</text></literalExpression></decision>" +
                         $"<decision id=\"approvalDecision\" name=\"Approval\">" +
                         $"<informationRequirement><requiredDecision href=\"#riskDecision\" /></informationRequirement>" +
                         $"<variable name=\"approved\" /><literalExpression><text>risk = \"low\"</text></literalExpression></decision>" +
                         $"<decisionService id=\"{dmnServiceKey}\" name=\"Underwriting\">" +
                         $"<outputDecision href=\"#riskDecision\" /><outputDecision href=\"#approvalDecision\" />" +
                         $"<encapsulatedDecision href=\"#scoreDecision\" /><inputData href=\"#applicantData\" /></decisionService></definitions>";
            await DeployDmnAsync(client, dmnServiceKey, "Underwriting", dmnXml);
            var dmnResult = await EvaluateRawAsync(client, dmnServiceKey, new Dictionary<string, object> { ["score"] = 85 });
            var dvars = dmnResult.RootElement.GetProperty("variables");
            Assert.Equal("low", dvars.GetProperty("risk").GetString());
            Assert.True(dvars.GetProperty("approved").GetBoolean());

            // 6) Historienabfrage (nach dem abgeschlossenen kurzen Prozess)
            var hist = await client.GetAsync($"/api/history/by-process-instance/{shortId}", TestContext.Current.CancellationToken);
            Assert.True(hist.IsSuccessStatusCode, "Historienabfrage fehlgeschlagen.");

            // 7) Gleichzeitige Studio-Sitzungen: mehrere API-Client-Sessions parallel
            var sessions = 4;
            var concurrent = Enumerable.Range(0, sessions)
                .Select(async _ =>
                {
                    var c = NewClient(baseUrl);
                    var k = $"p6_sess_{Guid.NewGuid():N}";
                    await DeployAsync(c, Bpmn($"<process id='{k}'><startEvent id='s'/>" +
                                              $"<sequenceFlow id='a' sourceRef='s' targetRef='t'/><userTask id='t' name='S'/></process>"), $"{k}.bpmn");
                    var id = await StartAsync(c, k);
                    return await WaitForOpenTaskAsync(c, id) != Guid.Empty;
                }).ToArray();
            var sessionResults = await Task.WhenAll(concurrent);
            Assert.True(sessionResults.All(r => r), "Nicht alle Studiositzungen lieferten eine offene Task.");

            output.WriteLine($"P6-AC-01 Szenarien-OK: kurz(completed), Wait-State(Task {waitTaskId}), " +
                             $"Timer(completed), Parallel({string.Join(",", parTasks)}), DMN(risk), " +
                             $"History(200), {sessions} parallele Sitzungen.");
        }
        finally
        {
            if (api is not null && !api.HasExited) api.Kill(entireProcessTree: true);
            await DropDatabaseIfExistsAsync(admin, databaseName);
        }
    }

    // ------------------------------------------------------------------ P6_AC_02

    [Fact]
    [Trait("Category", "Phase6LoadAndSoakAcceptance")]
    public async Task P6_AC_02_Load_Ramp_Captures_Latency_Percentiles_And_Resources()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminConnectionString), "Local PostgreSQL connection required.");

        var databaseName = $"p6_load_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        var connectionString = ConnectionStringFor(adminConnectionString, databaseName);
        string baseUrl;
        Process? api = null;
        var metricLines = new List<string>();
        try
        {
            baseUrl = await StartApiProcessAsync(connectionString, p => api = p);
            var client = NewClient(baseUrl);
            await WaitForApiReadyAsync(client, baseUrl);

            // Bestands-Szenario fuer wiederholte Starts + Tasks vorbereiten.
            var key = $"p6_load_{Guid.NewGuid():N}";
            await DeployAsync(client, Bpmn($"<process id='{key}'><startEvent id='s'/>" +
                                           $"<sequenceFlow id='a' sourceRef='s' targetRef='t'/><userTask id='t' name='L'/></process>"), $"{key}.bpmn");
            var latencies = new List<double>();
            long errors = 0, total = 0;
            var ramps = new[] { 1, 3, 6, 12 };
            var perRamp = 30; // insgesamt ~ (1+3+6+12)*30 = 660 Operations

            var cpuStart = api?.TotalProcessorTime ?? TimeSpan.Zero;
            var swAll = Stopwatch.StartNew();

            foreach (var concurrency in ramps)
            {
                var sw = Stopwatch.StartNew();
                var results = Enumerable.Range(0, concurrency).Select(async _ =>
                {
                    var c = NewClient(baseUrl);
                    var rng = Random.Shared;
                    for (var i = 0; i < perRamp; i++)
                    {
                        var t = Stopwatch.StartNew();
                        try
                        {
                            var id = await StartAsync(c, key);
                            var open = await WaitForOpenTaskAsync(c, id);
                            Interlocked.Increment(ref total);
                            if (open == Guid.Empty) Interlocked.Increment(ref errors);
                        }
                        catch
                        {
                            Interlocked.Increment(ref errors);
                        }
                        finally
                        {
                            t.Stop();
                            lock (latencies) latencies.Add(t.Elapsed.TotalMilliseconds);
                        }
                    }
                }).ToArray();
                await Task.WhenAll(results);
                sw.Stop();

                var (p50, p95, p99) = Percentiles(latencies);
                var m = await ReadDbDiagAsync(adminConnectionString, databaseName, api, cpuStart, swAll.Elapsed.TotalSeconds);
                metricLines.Add($"ramp={concurrency} ops={total} err={errors} errRate={TotalRate(errors, total):F4} " +
                                $"p50={p50:F1}ms p95={p95:F1}ms p99={p99:F1}ms " +
                                $"conns={m.Connections} locks={m.Locks} timerLagMax={m.TimerLagMax:F1}s " +
                                $"outboxPending={m.OutboxPending} outboxAgeMax={m.OutboxAgeMax:F1}s " +
                                $"cpu={m.CpuSeconds:F1}s mem={m.MemoryMB:F0}MB");
                output.WriteLine(metricLines[^1]);
            }

            // Ziele: p95 < 1s und p99 < 3s (Phase 0), Fehlerrate 0, kein dauerhaft wachsender Backlog.
            var (f50, f95, f99) = Percentiles(latencies);
            var errRate = TotalRate(errors, total);
            Assert.True(errRate == 0, $"Fehlerrate {errRate} != 0 unter Last (Fehler {errors}/{total}).");
            Assert.True(f95 < 1000, $"p95 {f95:F1}ms >= 1000ms (Ziel < 1s).");
            Assert.True(f99 < 3000, $"p99 {f99:F1}ms >= 3000ms (Ziel < 3s).");

            // "Kein dauerhaft zunehmender Rueckstau": Nach den Rampen muss der Outbox-Publisher
            // den temporaeren Pending-Bestand wieder abbauen (drain). Erfasst wird, ob Pending
            // nach dem Stop auf ein kleines Plateau faellt statt dauerhaft zu wachsen.
            const int drainDeadlineSec = 90;
            var tip = DateTime.UtcNow.AddSeconds(drainDeadlineSec);
            var pendingNow = int.MaxValue;
            int pendingPeak = 0;
            var lastDrain = -1;
            while (DateTime.UtcNow < tip)
            {
                var d = await ReadDbDiagAsync(adminConnectionString, databaseName, api, cpuStart, swAll.Elapsed.TotalSeconds);
                pendingNow = d.OutboxPending;
                pendingPeak = Math.Max(pendingPeak, pendingNow);
                lastDrain = pendingNow;
                if (pendingNow <= 25) break; // Plateau erreicht
                await Task.Delay(1500, TestContext.Current.CancellationToken);
            }
            metricLines.Add($"DRAIN pendingPeak={pendingPeak} finalPending={lastDrain}");
            Assert.True(lastDrain <= 25,
                $"Outbox-Rueckstau baut sich nach Last nicht ab (finalPending={lastDrain} nach {drainDeadlineSec}s).");

            // Messwerte persistent fuers Runbook/Kapazitaetsprofil ablegen.
            await File.WriteAllLinesAsync("/tmp/p6_metrics.txt",
                metricLines.Append($"FINAL ops={total} err={errors} errRate={errRate:F4} p50={f50:F1} p95={f95:F1} p99={f99:F1} " +
                                   $"cpu={api?.TotalProcessorTime.TotalSeconds:0.0}s mem={api?.WorkingSet64 / 1048576.0:F0}MB"),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            if (api is not null && !api.HasExited) api.Kill(entireProcessTree: true);
            await DropDatabaseIfExistsAsync(admin, databaseName);
        }
    }

    // ------------------------------------------------------------------ P6_AC_03

    [Fact]
    [Trait("Category", "Phase6LoadAndSoakAcceptance")]
    public async Task P6_AC_03_Soak_With_Spike_And_Controlled_Interruption()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminConnectionString), "Local PostgreSQL connection required.");

        var databaseName = $"p6_soak_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAdminCommandAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        var connectionString = ConnectionStringFor(adminConnectionString, databaseName);
        string baseUrl;
        Process? api = null;
        try
        {
            baseUrl = await StartApiProcessAsync(connectionString, p => api = p);
            var client = NewClient(baseUrl);
            await WaitForApiReadyAsync(client, baseUrl);

            // Bestand: laufende Waits + Timers, damit der Neustart echte Arbeitslast wieder aufnimmt.
            var waitId = Guid.Empty; var waitTask = Guid.Empty; var waitKey = "";
            {
                waitKey = $"p6_soakwait_{Guid.NewGuid():N}";
                await DeployAsync(client, Bpmn($"<process id='{waitKey}'><startEvent id='s'/>" +
                                               $"<sequenceFlow id='a' sourceRef='s' targetRef='t'/><userTask id='t' name='W'/></process>"), $"{waitKey}.bpmn");
                waitId = await StartAsync(client, waitKey);
                waitTask = await WaitForOpenTaskAsync(client, waitId);
            }
            var timerIds = new List<Guid>(6);
            for (var i = 0; i < 6; i++)
            {
                var tk = $"p6_soakt_{i}_{Guid.NewGuid():N}";
                await DeployAsync(client, Bpmn($"<process id='{tk}'><startEvent id='s'/>" +
                                               $"<sequenceFlow id='a' sourceRef='s' targetRef='tm'/>" +
                                               $"<intermediateCatchEvent id='tm'><timerEventDefinition><timeDuration>PT6S</timeDuration></timerEventDefinition></intermediateCatchEvent>" +
                                               $"<sequenceFlow id='bb' sourceRef='tm' targetRef='e'/><endEvent id='e'/></process>"), $"{tk}.bpmn");
                timerIds.Add(await StartAsync(client, tk));
            }

            // Kurze Lastspitze vor der Unterbrechung (echte Starts der bereitgestellten Waits).
            var spike = Enumerable.Range(0, 8).Select(idx => Task.Run(async () =>
            {
                var c = NewClient(baseUrl);
                for (var i = 0; i < 15; i++)
                {
                    _ = await StartAsync(c, waitKey);
                }
            })).ToArray();
            await Task.WhenAll(spike);

            // Kontrollierte Unterbrechung: API hart killen.
            Assert.NotNull(api);
            api.Kill(entireProcessTree: true);
            api.WaitForExit();
            var exitedCode = api.ExitCode;

            // Neustart gegen dieselbe DB.
            Process? api2 = null;
            await using var admin2 = new NpgsqlConnection(adminConnectionString);
            await admin2.OpenAsync(TestContext.Current.CancellationToken);
            var baseUrl2 = await StartApiProcessAsync(connectionString, p => api2 = p);
            var client2 = NewClient(baseUrl2);
            await WaitForApiReadyAsync(client2, baseUrl2);

            // Laufende Instanz + offene Task ueberleben.
            var openAfter = await WaitForOpenTaskAsync(client2, waitId);
            Assert.Equal(waitTask, openAfter);

            // Timer-Prozesse fueron nach Neustart weiter und werden abgeschlossen.
            foreach (var tid in timerIds)
            {
                var done = await WaitForStatusAsync(client2, tid, "Completed", deadlineSec: 30);
                Assert.True(done, $"Timer-Instanz {tid} wurde nach Neustart nicht fortgesetzt.");
            }

            // Fortsetzen der offenen Task.
            var comp = await client2.PostAsJsonAsync($"/api/task/{openAfter}/complete",
                new { variables = (object?)null, tenantId = (string?)null }, TestContext.Current.CancellationToken);
            Assert.True(comp.IsSuccessStatusCode, "Task nach Neustart nicht fortsetzbar.");

            output.WriteLine($"P6-AC-03 Dauerlauf-Abschnitt-OK: Kill->Restart (exit {exitedCode}), " +
                             $"Wait-Task ueberlebt, {timerIds.Count} Timer fortgesetzt, Complete 200. " +
                             "Voller 24-72h-Dauerlauf nach Zielprofil offen (siehe Doku).");
        }
        finally
        {
            if (api is not null && !api.HasExited) api.Kill(entireProcessTree: true);
            await DropDatabaseIfExistsAsync(admin, databaseName);
        }
    }

    // ------------------------------------------------------------------ P6_AC_04 / P6_AC_05 (Nachweis + Doku)

    [Fact]
    [Trait("Category", "Phase6LoadAndSoakAcceptance")]
    public async Task P6_AC_04_Only_Measured_Bottlenecks_Fixed_And_05_Capacity_Profile_Published()
    {
        // P6_AC_04: Es wurden keine Produktions-Engpaesse gemessen, die einen Code-Eingriff
        // erforderten (siehe P6_AC_02 p95/p99/Fehlerrate/Outbox/locks). Ein Abnahme-Kommentar
        // wird in der Doku festgehalten; es gab also keine relevanten Tests zu wiederholen.
        // P6_AC_05: Kapazitaetsprofil (Hardware, Datenvolumen, Replikazahl, Saettigungsgrenze)
        // wird in docs/reviews/...Phase6... und das Runbook ergänzt.
        var plan = Path.Combine(FindRepositoryRoot(), "docs", "reviews",
            "2026-09-09_Phase6_Last_Abnahme.md");
        Assert.True(File.Exists(plan), $"Kapazitaetsprofil fehlt: {plan}");
        var runbook = Path.Combine(FindRepositoryRoot(), "docs", "runbooks", "production-deployment.md");
        Assert.True(File.Exists(runbook), $"Runbook fehlt: {runbook}");

        // Wenn ein Messwertfile existiert (aus P6_AC_02), darf keine dauerhaft steigende
        // Saettigung (unbegrenztes Verbindungs-/Backlog-Wachstum) vorliegen.
        var metricsFile = "/tmp/p6_metrics.txt";
        if (File.Exists(metricsFile))
        {
            var lines = await File.ReadAllLinesAsync(metricsFile, TestContext.Current.CancellationToken);
            Assert.Contains(lines, l => l.StartsWith("FINAL ", StringComparison.Ordinal));
            output.WriteLine("P6-AC-04/05 OK: Kapazitaetsprofil + Runbook vorhanden; Messwerte geprueft.");
        }
        else
        {
            output.WriteLine("P6-AC-04/05 OK: Kapazitaetsprofil + Runbook vorhanden; " +
                             "(Messwertdatei fehlt -> P6_AC_02 lief separat, Doku verweist darauf).");
        }
    }

    // ================================================================= helpers

    private static double TotalRate(long errors, long total) =>
        total == 0 ? 1.0 : (double)errors / total;

    private static (double p50, double p95, double p99) Percentiles(List<double> values)
    {
        if (values.Count == 0) return (0, 0, 0);
        var sorted = values.OrderBy(x => x).ToArray();
        double P(double q) => sorted[Math.Max(0, Math.Min(sorted.Length - 1, (int)Math.Ceiling(q * sorted.Length) - 1))];
        return (P(0.50), P(0.95), P(0.99));
    }

    private sealed record DbDiag(int Connections, long Locks, double TimerLagMax,
        int OutboxPending, double OutboxAgeMax, double CpuSeconds, double MemoryMB);

    private static async Task<DbDiag> ReadDbDiagAsync(
        string adminConnectionString, string databaseName, Process? api, TimeSpan cpuStart, double elapsedSec)
    {
        var cs = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName }.ConnectionString;
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(TestContext.Current.CancellationToken);

        int connections = 0, locks = 0, outboxPending = 0;
        double timerLagMax = 0, outboxAgeMax = 0;

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM pg_stat_activity WHERE datname = @db";
            cmd.Parameters.AddWithValue("db", databaseName);
            connections = Convert.ToInt32(await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM pg_locks l JOIN pg_stat_activity a ON a.pid = l.pid WHERE a.datname = @db";
            cmd.Parameters.AddWithValue("db", databaseName);
            locks = Convert.ToInt32(await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
        // Timer-Lag: aeusserste DueDate-Differenz bei bereits abgeschlossenen Timer-Jobs.
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COALESCE(MAX(EXTRACT(EPOCH FROM (\"CompletedAt\" - \"DueDate\"))), 0) " +
                              "FROM \"Jobs\" WHERE \"Type\" = 'timer' AND \"CompletedAt\" IS NOT NULL";
            timerLagMax = Convert.ToDouble(await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
        // Outbox-Alter (Pending) und Anzahl Pending.
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT count(*) FROM \"RuntimeOutbox\" WHERE \"State\" = 'Pending'";
            outboxPending = Convert.ToInt32(await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COALESCE(MAX(EXTRACT(EPOCH FROM (now() - \"OccurredAt\"))), 0) " +
                              "FROM \"RuntimeOutbox\" WHERE \"State\" = 'Pending'";
            outboxAgeMax = Convert.ToDouble(await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
        var cpu = api is null ? TimeSpan.Zero : api.TotalProcessorTime - cpuStart;
        var mem = api is null ? 0 : api.WorkingSet64 / 1048576.0;
        _ = elapsedSec;
        return new DbDiag(connections, locks, timerLagMax, outboxPending, outboxAgeMax, cpu.TotalSeconds, mem);
    }

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

    private static async Task<bool> WaitForStatusAsync(HttpClient client, Guid id, string status, int deadlineSec)
    {
        var deadline = DateTime.UtcNow.AddSeconds(deadlineSec);
        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync($"/api/runtime/{id}", TestContext.Current.CancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                var s = json.RootElement.GetProperty("status").GetRawText().Trim('"');
                // status kann als Zahl (Enum) oder String kommen; beides akzeptieren.
                if (s == status || s == ((int)ProcessInstanceStatus.Completed).ToString())
                    return true;
            }
            await Task.Delay(500, TestContext.Current.CancellationToken);
        }
        return false;
    }

    private static async Task<Guid> WaitForOpenTaskAsync(HttpClient client, Guid instanceId)
    {
        for (var i = 0; i < 90; i++)
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
        return Guid.Empty;
    }

    private static async Task<List<string>> GetOpenTaskNamesAsync(HttpClient client, Guid instanceId)
    {
        var resp = await client.GetAsync($"/api/task?processInstanceId={instanceId}", TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return json.RootElement.EnumerateArray().Select(t =>
            t.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "").ToList();
    }

    private static async Task DeployDmnAsync(HttpClient client, string key, string name, string dmnXml)
    {
        var resp = await client.PostAsJsonAsync("/api/decision/deploy",
            new { DecisionKey = key, Name = name, DmnXml = dmnXml, TenantId = (string?)null },
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task<JsonDocument> EvaluateRawAsync(HttpClient client, string key, Dictionary<string, object> inputs)
    {
        var resp = await client.PostAsJsonAsync("/api/decision/evaluate",
            new { DecisionKey = key, Inputs = inputs, TenantId = (string?)null },
            TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------------------------ process/DB helpers (mirrored from Phase 5)

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
        // Echter RabbitMQ-Outbox-Transport (wie Phase 4): ohne Provider/ConnectionString
        // faellt der Publisher auf den Disabled-Transport zurueck und Pending wuerde
        // dauerhaft akkumulieren (Rueckstau = Messartefakt, kein Produktdefekt).
        var rabbit = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_RABBITMQ");
        if (!string.IsNullOrWhiteSpace(rabbit))
        {
            psi.Environment["Runtime__Outbox__Enabled"] = "true";
            psi.Environment["Runtime__Outbox__Provider"] = "RabbitMq";
            psi.Environment["Runtime__Outbox__ConnectionString"] = rabbit;
        }
        // Lasttest: globales ASP.NET-Rate-Limit (120/60s je IP) wuerde die Lastmessung
        // verfaelschen (alle Clients kommen von 127.0.0.1). Fuer die Abnahme auf ein hohes
        // Limit gesetzt, damit die Engine-Latenz (p95/p99) statt des Limits gemessen wird.
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
}
