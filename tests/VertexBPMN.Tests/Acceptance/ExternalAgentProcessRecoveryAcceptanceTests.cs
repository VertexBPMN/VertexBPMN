using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Npgsql;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

public sealed class ExternalAgentProcessRecoveryAcceptanceTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "ExternalAgentProcessRecovery")]
    public async Task E11_worker_crash_database_outage_and_api_restart_drain_durable_job_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminString = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN");
        var postgresContainer = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_CONTAINER");
        var postgresVolume = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_VOLUME");
        var authority = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_WORKER_AUTHORITY");
        var workerSecret = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_WORKER_CLIENT_SECRET");
        var model = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_OLLAMA_MODEL");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(adminString)
                          && !string.IsNullOrWhiteSpace(postgresContainer)
                          && !string.IsNullOrWhiteSpace(postgresVolume)
                          && !string.IsNullOrWhiteSpace(authority)
                          && !string.IsNullOrWhiteSpace(workerSecret)
                          && !string.IsNullOrWhiteSpace(model),
            "Use scripts/test-external-agent-recovery-local.ps1 to provide isolated infrastructure.");

        var database = "a07_recovery_" + Guid.NewGuid().ToString("N");
        await using (var admin = await OpenPostgresWithRetryAsync(adminString!, ct))
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            await create.ExecuteNonQueryAsync(ct);
        var connectionString = new NpgsqlConnectionStringBuilder(adminString)
        {
            Database = database, Pooling = false, Timeout = 5, CommandTimeout = 10
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseVertexNpgsql(connectionString).Options;
        Process? api = null;
        Process? worker = null;
        var postgresUnavailable = false;
        try
        {
            var jobId = await SeedJobAsync(options, ct);
            var port = GetFreePort();
            api = StartApi(connectionString, authority!, port);
            await WaitForApiAsync(api, port, ct);
            worker = StartWorker(port, authority!, workerSecret!, model!);

            await WaitForStateAsync(options, jobId, ExternalTaskState.Leased, TimeSpan.FromSeconds(45), ct);
            worker.Kill(entireProcessTree: true);
            await worker.WaitForExitAsync(ct);
            output.WriteLine("Worker hard-crashed after acquiring a durable lease.");

            api.Kill(entireProcessTree: true);
            await api.WaitForExitAsync(ct);
            api.Dispose();
            api = null;
            output.WriteLine("API hard-crashed while the durable lease was outstanding.");

            var adminSettings = new NpgsqlConnectionStringBuilder(adminString);
            await RunWslcAsync(["stop", postgresContainer!], ct);
            postgresUnavailable = true;
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
            await RunWslcAsync(["container", "remove", postgresContainer!], ct);
            await RunWslcAsync([
                "run", "--detach", "--name", postgresContainer!,
                "--publish", $"127.0.0.1:{adminSettings.Port}:5432",
                "--env", $"POSTGRES_USER={adminSettings.Username}",
                "--env", $"POSTGRES_PASSWORD={adminSettings.Password}",
                "--env", "POSTGRES_DB=postgres",
                "--volume", $"{postgresVolume}:/var/lib/postgresql/data",
                "postgres:17-alpine"
            ], ct);
            postgresUnavailable = false;
            await WaitForPostgresAsync(connectionString, ct);
            output.WriteLine("Dedicated PostgreSQL was stopped and recreated from its durable volume.");

            api = StartApi(connectionString, authority!, port);
            await WaitForApiAsync(api, port, ct);
            output.WriteLine("API restarted against the same durable database.");

            await WaitForReclaimableAsync(options, jobId, TimeSpan.FromSeconds(45), ct);
            worker.Dispose();
            worker = StartWorker(port, authority!, workerSecret!, model!);
            await WaitForDrainAsync(options, jobId, TimeSpan.FromMinutes(4), ct);

            await using var verify = new BpmnDbContext(options);
            var job = await verify.ExternalTaskJobs.AsNoTracking().SingleAsync(item => item.Id == jobId, ct);
            var attempts = await verify.ExternalTaskAttempts.AsNoTracking()
                .Where(item => item.JobId == jobId).OrderBy(item => item.AttemptNumber).ToListAsync(ct);
            var continuations = await verify.ExternalTaskContinuations.AsNoTracking()
                .Where(item => item.JobId == jobId).ToListAsync(ct);
            Assert.Equal(ExternalTaskState.Completed, job.State);
            Assert.Equal(2, attempts.Count);
            Assert.Equal("lease_expired", attempts[0].EndReason);
            Assert.NotNull(attempts[1].EndedAt);
            Assert.Single(continuations);
            Assert.Equal(ExternalTaskContinuationState.Applied, continuations[0].State);
            Assert.Single(await verify.Tasks.AsNoTracking()
                .Where(item => item.ProcessInstanceId == job.ProcessInstanceId && item.ActivityId == "human-review")
                .ToListAsync(ct));
            output.WriteLine("E11 passed: two bounded attempts, one completion, one applied continuation, one human task.");
        }
        finally
        {
            Kill(worker);
            Kill(api);
            try
            {
                if (!postgresUnavailable)
                {
                    await using var admin = await OpenPostgresWithRetryAsync(adminString!, CancellationToken.None);
                    await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
                    await drop.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }
            catch (Exception exception)
            {
                output.WriteLine($"Best-effort recovery database cleanup failed: {exception.GetType().Name}.");
            }
        }
    }

    private static async Task RunWslcAsync(IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("wslc.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start wslc.exe.");
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"wslc {string.Join(' ', arguments)} failed: {await stderr}");
        _ = await stdout;
    }

    private static async Task<Guid> SeedJobAsync(DbContextOptions<BpmnDbContext> options, CancellationToken ct)
    {
        var configuration = ContractConfiguration();
        await using var db = new BpmnDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(cancellationToken: ct);
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "a07", TenantId = "tenant-a" };
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(), Key = "a07-recovery", Name = "a07", TenantId = "tenant-a",
            TenantScope = "tenant-a", Version = 1, DeploymentId = deployment.Id,
            BpmnXml = "<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:vertex='https://vertexbpmn.io/schema/bpmn/1.0'><process id='a07-recovery'><startEvent id='s'/><serviceTask id='agent-review'><extensionElements><vertex:externalTask topic='agent.contract-review' agentProfileRef='contract-reviewer.v1' maxRetries='1' deadlineSeconds='600'/><vertex:ioMapping><vertex:input name='document' expression='document'/><vertex:input name='documentId' expression='documentId'/><vertex:input name='documentVersion' expression='documentVersion'/><vertex:output name='result' target='contractReview'/></vertex:ioMapping></extensionElements></serviceTask><userTask id='human-review'/><endEvent id='e'/><sequenceFlow id='f1' sourceRef='s' targetRef='agent-review'/><sequenceFlow id='f2' sourceRef='agent-review' targetRef='human-review'/><sequenceFlow id='f3' sourceRef='human-review' targetRef='e'/></process></definitions>"
        };
        var process = new ProcessInstance
        {
            Id = Guid.NewGuid(), ProcessDefinitionId = definition.Id, TenantId = "tenant-a",
            Status = ProcessInstanceStatus.Running, Revision = 1,
            Variables = new Dictionary<string, object>
            {
                ["document"] = "# Liability\nSupplier liability is unlimited.",
                ["documentId"] = "e11-document",
                ["documentVersion"] = "e11-v1"
            }
        };
        var activityExecutionId = Guid.NewGuid();
        var wait = new ExecutionToken
        {
            Id = Guid.NewGuid(), ProcessInstanceId = process.Id, CurrentNodeId = "agent-review",
            NodeType = "serviceTask", State = ExecutionToken.WaitingState,
            ActivityExecutionId = activityExecutionId, ScopeExecutionId = process.Id, Revision = 1
        };
        var inputs = new Dictionary<string, object>
        {
            ["document"] = "# Liability\nSupplier liability is unlimited.",
            ["documentId"] = "e11-document", ["documentVersion"] = "e11-v1"
        };
        var resolved = await new ConfiguredExternalTaskContractResolver(configuration).ResolveAsync("tenant-a",
            new VertexBPMN.Domain.Model.Bpmn.ExternalTaskDefinition("agent.contract-review", "contract-reviewer.v1", 1, 600),
            inputs, ct);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var job = new ExternalTaskJob
        {
            Id = Guid.NewGuid(), TenantId = "tenant-a", ProcessInstanceId = process.Id,
            DefinitionId = definition.Id, DefinitionVersion = 1, ActivityId = "agent-review",
            ActivityExecutionId = activityExecutionId, WaitTokenId = wait.Id, ScopeExecutionId = process.Id,
            Topic = "agent.contract-review", AgentProfileRef = "contract-reviewer.v1",
            ContractVersion = "contract-review.v1", AgentProfileVersion = "contract-reviewer.v1",
            InputSnapshot = System.Text.Json.JsonSerializer.Serialize(inputs),
            DefinitionSnapshot = "{\"mappings\":{\"vertex:ioMapping.output.result\":\"contractReview\"}}",
            SchemaSnapshot = resolved.SchemaSnapshot, State = ExternalTaskState.Ready, Revision = 1,
            CreatedAt = now, AvailableAt = now, Deadline = now + 600_000, MaxAttempts = 2
        };
        db.AddRange(deployment, definition, process, wait, job);
        await db.SaveChangesAsync(ct);
        return job.Id;
    }

    private static IConfigurationRoot ContractConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["ExternalTasks:Enabled"] = "true",
            ["ExternalTasks:Contracts:0:TenantId"] = "tenant-a",
            ["ExternalTasks:Contracts:0:Enabled"] = "true",
            ["ExternalTasks:Contracts:0:Topic"] = "agent.contract-review",
            ["ExternalTasks:Contracts:0:Version"] = "contract-review.v1",
            ["ExternalTasks:Contracts:0:AgentProfileRef"] = "contract-reviewer.v1",
            ["ExternalTasks:Contracts:0:AgentProfileVersion"] = "contract-reviewer.v1",
            ["ExternalTasks:Contracts:0:MaxAttempts"] = "2",
            ["ExternalTasks:Contracts:0:MaxDeadlineSeconds"] = "600"
        };
        var inputs = new[] { ("document", 65536), ("documentId", 256), ("documentVersion", 256) };
        for (var i = 0; i < inputs.Length; i++) AddField(values, "Inputs", i, inputs[i].Item1, "string", inputs[i].Item2);
        var outputs = new[] { ("schemaVersion", "string", 64), ("documentVersion", "string", 256),
            ("summary", "string", 4096), ("findings", "string", 65536), ("uncertainties", "string", 32768),
            ("requiresHumanReview", "boolean", 1), ("promptVersion", "string", 128), ("documentHash", "string", 64) };
        for (var i = 0; i < outputs.Length; i++) AddField(values, "Outputs", i, outputs[i].Item1, outputs[i].Item2, outputs[i].Item3);
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static void AddField(IDictionary<string, string?> values, string group, int index,
        string name, string type, int maximum)
    {
        var prefix = $"ExternalTasks:Contracts:0:{group}:{index}";
        values[$"{prefix}:Name"] = name; values[$"{prefix}:Type"] = type;
        values[$"{prefix}:MaxLength"] = maximum.ToString(); values[$"{prefix}:AllowExternalTransfer"] = "true";
    }

    private static Process StartApi(string connectionString, string authority, int port)
    {
        var psi = DotnetProcess("src/VertexBPMN.Api/bin/Release/net10.0/VertexBPMN.Api.dll");
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "OidcTest";
        psi.Environment["DOTNET_ENVIRONMENT"] = "OidcTest";
        psi.Environment["OperationalMode"] = "OidcTest";
        psi.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        psi.Environment["Database__ApplyMigrationsOnStartup"] = "true";
        psi.Environment["Jwt__Authority"] = authority;
        psi.Environment["Jwt__Issuer"] = authority;
        psi.Environment["Jwt__Audience"] = "vertexbpmn-api";
        psi.Environment["Jwt__RequireHttpsMetadata"] = "false";
        psi.Environment["Jwt__UseDevelopmentApiKey"] = "false";
        psi.Environment["Logging__LogLevel__Default"] = "Error";
        ApplyContractEnvironment(psi.Environment);
        foreach (var context in new[] { "BpmnDbContext", "TenantDbContext", "SimulationScenarioDbContext", "ProcessMiningEvents", "DecisionDbContext" })
            psi.Environment[$"ConnectionStrings__{context}"] = connectionString;
        return Start(psi, "api");
    }

    private static Process StartWorker(int apiPort, string authority, string secret, string model)
    {
        var psi = DotnetProcess("src/VertexBPMN.AgentWorker/bin/Release/net10.0/VertexBPMN.AgentWorker.dll");
        psi.Environment["ExternalTaskWorker__Enabled"] = "true";
        psi.Environment["ExternalTaskWorker__BaseAddress"] = $"http://127.0.0.1:{apiPort}/";
        psi.Environment["ExternalTaskWorker__TokenEndpoint"] = $"{authority.TrimEnd('/')}/protocol/openid-connect/token";
        psi.Environment["ExternalTaskWorker__ClientId"] = "vertexbpmn-contract-reviewer";
        psi.Environment["ExternalTaskWorker__ClientSecret"] = secret;
        psi.Environment["ExternalTaskWorker__Scope"] = "openid";
        psi.Environment["ExternalTaskWorker__Topics__0"] = "agent.contract-review";
        psi.Environment["ExternalTaskWorker__LeaseSeconds"] = "15";
        psi.Environment["ExternalTaskWorker__HeartbeatSeconds"] = "5";
        psi.Environment["ExternalTaskWorker__PollMilliseconds"] = "250";
        psi.Environment["ContractReviewer__Enabled"] = "true";
        psi.Environment["ContractReviewer__Model"] = model;
        psi.Environment["ContractReviewer__Endpoint"] = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_OLLAMA_ENDPOINT") ?? "http://127.0.0.1:11434/";
        psi.Environment["ContractReviewer__MaximumRuntimeSeconds"] = "180";
        psi.Environment["Logging__LogLevel__Default"] = "Warning";
        return Start(psi, "worker");
    }

    private static ProcessStartInfo DotnetProcess(string assembly)
    {
        var psi = new ProcessStartInfo("dotnet") { WorkingDirectory = FindRoot(), RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add(assembly);
        return psi;
    }

    private static Process Start(ProcessStartInfo psi, string label)
    {
        var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine($"[{label}] {e.Data}"); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine($"[{label}:stderr] {e.Data}"); };
        Assert.True(process.Start(), $"Could not start {label} process.");
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        return process;
    }

    private static void ApplyContractEnvironment(IDictionary<string, string?> environment)
    {
        foreach (var pair in ContractConfiguration().AsEnumerable().Where(item => item.Value is not null))
            environment[pair.Key.Replace(":", "__", StringComparison.Ordinal)] = pair.Value;
    }

    private static async Task WaitForApiAsync(Process api, int port, CancellationToken ct)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var i = 0; i < 120; i++)
        {
            if (api.HasExited)
                throw new InvalidOperationException($"API exited prematurely with {api.ExitCode}.");
            try { if ((await client.GetAsync($"http://127.0.0.1:{port}/api/ready", ct)).IsSuccessStatusCode) return; }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException) { }
            await Task.Delay(500, ct);
        }
        throw new TimeoutException("API did not become ready.");
    }

    private static async Task WaitForStateAsync(DbContextOptions<BpmnDbContext> options, Guid jobId,
        ExternalTaskState state, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var db = new BpmnDbContext(options);
                if ((await db.ExternalTaskJobs.AsNoTracking().SingleAsync(item => item.Id == jobId, ct)).State == state) return;
            }
            catch (Exception exception) when (IsTransientPostgresFailure(exception)) { }
            await Task.Delay(250, ct);
        }
        throw new TimeoutException($"Job did not reach {state}.");
    }

    private static async Task WaitForReclaimableAsync(DbContextOptions<BpmnDbContext> options, Guid jobId,
        TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var db = new BpmnDbContext(options);
                var state = (await db.ExternalTaskJobs.AsNoTracking().SingleAsync(item => item.Id == jobId, ct)).State;
                if (state is ExternalTaskState.Ready or ExternalTaskState.RetryScheduled) return;
            }
            catch (Exception exception) when (IsTransientPostgresFailure(exception)) { }
            await Task.Delay(500, ct);
        }
        throw new TimeoutException("Expired lease was not recovered.");
    }

    private static async Task WaitForDrainAsync(DbContextOptions<BpmnDbContext> options, Guid jobId,
        TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var db = new BpmnDbContext(options);
                var job = await db.ExternalTaskJobs.AsNoTracking().SingleAsync(item => item.Id == jobId, ct);
                var applied = await db.ExternalTaskContinuations.AsNoTracking()
                    .CountAsync(item => item.JobId == jobId && item.State == ExternalTaskContinuationState.Applied, ct);
                if (job.State == ExternalTaskState.Completed && applied == 1) return;
            }
            catch (Exception exception) when (IsTransientPostgresFailure(exception)) { }
            await Task.Delay(1000, ct);
        }
        throw new TimeoutException("Recovered job did not drain.");
    }

    private static async Task WaitForPostgresAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = await OpenPostgresWithRetryAsync(connectionString, ct);
    }

    private static async Task<NpgsqlConnection> OpenPostgresWithRetryAsync(
        string connectionString,
        CancellationToken ct)
    {
        for (var i = 0; i < 60; i++)
        {
            var connection = new NpgsqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(ct);
                return connection;
            }
            catch (NpgsqlException)
            {
                await connection.DisposeAsync();
                await Task.Delay(500, ct);
            }
        }
        throw new TimeoutException("PostgreSQL did not recover.");
    }

    private static bool IsTransientPostgresFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
            if (current is NpgsqlException or TimeoutException) return true;
        return false;
    }

    private static void Kill(Process? process)
    {
        if (process is null) return;
        try { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(15_000); } } catch { }
        process.Dispose();
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "VertexBPMN.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;
    }

}
