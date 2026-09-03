using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Playwright;
using Npgsql;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

/// <summary>
/// Starts the real API and Studio for explicitly enabled local browser tests.
/// PostgreSQL and RabbitMQ remain externally managed so the host works with
/// WSLC as well as native local installations.
/// </summary>
public sealed class LocalStudioE2ETestHost : IAsyncLifetime
{
    public const string EnabledEnvironmentVariable = "VERTEXBPMN_STUDIO_E2E_ENABLED";

    private readonly ConcurrentQueue<string> _apiLogs = new();
    private readonly ConcurrentQueue<string> _studioLogs = new();
    private readonly ConcurrentDictionary<IPage, BrowserArtifactSession> _browserArtifactSessions = new();
    private readonly ConcurrentStack<ApiCleanupRequest> _cleanupRequests = new();
    private readonly ConcurrentStack<ProcessDefinitionCleanup> _processDefinitionCleanups = new();
    private readonly List<IsolatedDatabase> _isolatedDatabases = [];
    private Process? _apiProcess;
    private Process? _studioProcess;
    private IPlaywright? _playwright;
    private string? _workingDirectory;
    private string? _apiKey;

    public static bool IsEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable(EnabledEnvironmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public string RunId { get; private set; } = string.Empty;
    public string TenantId => $"studio-e2e-{RunId}";
    public Uri ApiBaseAddress { get; private set; } = null!;
    public Uri StudioBaseAddress { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public IReadOnlyList<string> ApiLogs => _apiLogs.ToArray();
    public IReadOnlyList<string> StudioLogs => _studioLogs.ToArray();

    /// <summary>
    /// Gets the PostgreSQL databases created exclusively for the current test run.
    /// </summary>
    public IReadOnlyList<string> IsolatedDatabaseNames => _isolatedDatabases.Select(database => database.Name).ToArray();

    /// <summary>
    /// Creates an isolated browser page with tracing and diagnostic collection enabled.
    /// </summary>
    public async Task<IPage> CreatePageAsync([CallerMemberName] string scenarioName = "")
    {
        var context = await Browser.NewContextAsync();
        await context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        var page = await context.NewPageAsync();
        var session = new BrowserArtifactSession(scenarioName, context);
        page.PageError += (_, error) => session.BrowserConsole.Enqueue($"page-error: {error}");
        page.Console += (_, message) => session.BrowserConsole.Enqueue($"{message.Type}: {message.Text}");
        page.RequestFailed += (_, request) => session.FailedRequests.Enqueue(
            $"{request.Method} {request.Url}: {request.Failure}");
        _browserArtifactSessions[page] = session;
        return page;
    }

    /// <summary>
    /// Saves browser and server diagnostics before closing an isolated test page.
    /// </summary>
    public async Task ClosePageAsync(IPage page)
    {
        if (!_browserArtifactSessions.TryRemove(page, out var session))
        {
            await page.CloseAsync();
            return;
        }

        var artifactDirectory = GetScenarioArtifactDirectory(session.ScenarioName);
        Directory.CreateDirectory(artifactDirectory);
        var diagnosticErrors = new List<string>();

        try
        {
            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(artifactDirectory, "final-page.png"),
                FullPage = true
            });
        }
        catch (PlaywrightException exception)
        {
            diagnosticErrors.Add($"Screenshot: {exception.Message}");
        }

        try
        {
            await session.Context.Tracing.StopAsync(new()
            {
                Path = Path.Combine(artifactDirectory, "playwright-trace.zip")
            });
        }
        catch (PlaywrightException exception)
        {
            diagnosticErrors.Add($"Trace: {exception.Message}");
        }

        await File.WriteAllLinesAsync(
            Path.Combine(artifactDirectory, "browser-console.log"),
            session.BrowserConsole);
        await File.WriteAllLinesAsync(
            Path.Combine(artifactDirectory, "failed-requests.log"),
            session.FailedRequests);
        await File.WriteAllLinesAsync(Path.Combine(artifactDirectory, "api.log"), ApiLogs);
        await File.WriteAllLinesAsync(Path.Combine(artifactDirectory, "studio.log"), StudioLogs);
        if (diagnosticErrors.Count > 0)
            await File.WriteAllLinesAsync(Path.Combine(artifactDirectory, "artifact-errors.log"), diagnosticErrors);

        await session.Context.CloseAsync();
    }

    /// <summary>
    /// Registers an API request that is executed during fixture teardown in reverse order.
    /// </summary>
    public void RegisterApiCleanup(HttpMethod method, string relativeUri, string? tenantId = null) =>
        _cleanupRequests.Push(new(method, relativeUri, tenantId));

    /// <summary>
    /// Registers all versions of a process key for fixture teardown.
    /// </summary>
    public void RegisterProcessDefinitionCleanup(string processKey, string? tenantId = null) =>
        _processDefinitionCleanups.Push(new(processKey, tenantId));

    public HttpClient CreateApiClient()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("The local Studio E2E host has not been initialized.");

        var client = new HttpClient { BaseAddress = ApiBaseAddress };
        client.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async ValueTask InitializeAsync()
    {
        if (!IsEnabled)
            return;

        try
        {
            await InitializeCoreAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    private async Task InitializeCoreAsync()
    {
        RunId = Environment.GetEnvironmentVariable("VERTEXBPMN_STUDIO_E2E_RUN_ID")
                ?? Guid.NewGuid().ToString("N");
        _workingDirectory = Path.Combine(Path.GetTempPath(), "VertexBPMN", "StudioE2E", RunId);
        Directory.CreateDirectory(_workingDirectory);

        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Release";
        var apiProject = Path.Combine(repositoryRoot, "src", "VertexBPMN.Api", "VertexBPMN.Api.csproj");
        var studioProject = Path.Combine(repositoryRoot, "src", "VertexBPMN.Studio", "VertexBPMN.Studio.csproj");
        _apiKey = $"local-studio-e2e-{RunId}";

        ApiBaseAddress = new Uri($"http://127.0.0.1:{GetFreePort()}/");
        StudioBaseAddress = new Uri($"http://127.0.0.1:{GetFreePort()}/");

        var bpmnConnection = await CreateIsolatedDatabaseAsync(RequiredEnvironment("VERTEXBPMN_E2E_BPMN_CONNECTION"));
        var tenantConnection = await CreateIsolatedDatabaseAsync(RequiredEnvironment("VERTEXBPMN_E2E_TENANT_CONNECTION"));
        var simulationConnection = await CreateIsolatedDatabaseAsync(RequiredEnvironment("VERTEXBPMN_E2E_SIMULATION_CONNECTION"));
        var eventsConnection = await CreateIsolatedDatabaseAsync(RequiredEnvironment("VERTEXBPMN_E2E_EVENTS_CONNECTION"));
        var decisionConnection = await CreateIsolatedDatabaseAsync(RequiredEnvironment("VERTEXBPMN_E2E_DECISION_CONNECTION"));

        var apiEnvironment = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["OperationalMode"] = "Development",
            ["PathBase"] = string.Empty,
            ["Database__ApplyMigrationsOnStartup"] = "true",
            ["ConnectionStrings__BpmnDbContext"] = bpmnConnection,
            ["ConnectionStrings__TenantDbContext"] = tenantConnection,
            ["ConnectionStrings__SimulationScenarioDbContext"] = simulationConnection,
            ["ConnectionStrings__ProcessMiningEvents"] = eventsConnection,
            ["ConnectionStrings__DecisionDbContext"] = decisionConnection,
            ["ConnectionStrings__DependencyRegistry"] = $"Data Source={Path.Combine(_workingDirectory, "dependencies.db")}",
            ["ConnectionStrings__messaging"] = RequiredEnvironment("VERTEXBPMN_E2E_RABBITMQ_CONNECTION"),
            ["Runtime__Outbox__Enabled"] = "true",
            ["Runtime__Outbox__Provider"] = "RabbitMq",
            ["Modules__Telemetry"] = "false",
            ["Modules__Plugins"] = "false",
            ["Dependencies__Plugins__Enabled"] = "false",
            ["Dependencies__Ai__Enabled"] = "false",
            ["Dependencies__Mcp__Enabled"] = "false",
            ["RateLimiting__PermitLimit"] = "10000",
            ["AdvancedFeatures__SimulationExecution"] = "true",
            ["AdvancedFeatures__LiveProcessMigration"] = "true",
            ["AdvancedFeatures__CmmnExecution"] = "true",
            ["Jwt__Audience"] = "vertexbpmn-api",
            ["Jwt__UseDevelopmentApiKey"] = "true",
            ["ApiKeys__0"] = _apiKey,
            ["ApiKeyAuthentication__DevelopmentRoles__0"] = "Admin",
            ["ApiKeyAuthentication__DevelopmentRoles__1"] = "ProcessManager",
            ["ApiKeyAuthentication__DevelopmentRoles__2"] = "ReadOnly"
        };

        _apiProcess = StartProject(apiProject, configuration, ApiBaseAddress, apiEnvironment, _apiLogs);
        await WaitForEndpointAsync(
            new Uri(ApiBaseAddress, "api/ready"),
            _apiProcess,
            _apiLogs,
            "API readiness");

        var studioEnvironment = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["ApiBaseUrl"] = ApiBaseAddress.ToString(),
            ["StudioAuthentication__LocalDevelopmentEnabled"] = "true",
            ["StudioAuthentication__DevelopmentApiKey"] = _apiKey,
            ["StudioHttpsRedirection__Enabled"] = "false",
            ["Logging__EventLog__LogLevel__Default"] = "None"
        };

        _studioProcess = StartProject(studioProject, configuration, StudioBaseAddress, studioEnvironment, _studioLogs);
        await WaitForEndpointAsync(
            new Uri(StudioBaseAddress, "health"),
            _studioProcess,
            _studioLogs,
            "Studio readiness");

        _playwright = await Playwright.CreateAsync();
        var chromiumExecutable = global::Chromium.Path
                                 ?? throw new InvalidOperationException(
                                     $"No Chromium executable found for {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}.");
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = chromiumExecutable
        });
    }

    public async ValueTask DisposeAsync()
    {
        Exception? databaseCleanupFailure = null;
        try
        {
            foreach (var page in _browserArtifactSessions.Keys)
                await ClosePageAsync(page);

            await ExecuteCleanupRequestsAsync();

            if (Browser is not null)
                await Browser.CloseAsync();
        }
        finally
        {
            _playwright?.Dispose();
            await StopProcessAsync(_studioProcess);
            await StopProcessAsync(_apiProcess);
            await Task.Delay(TimeSpan.FromSeconds(2));

            try
            {
                await DropIsolatedDatabasesAsync();
            }
            catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
            {
                databaseCleanupFailure = exception;
            }

            if (!string.IsNullOrWhiteSpace(_workingDirectory) && Directory.Exists(_workingDirectory))
                await TryDeleteWorkingDirectoryAsync(_workingDirectory);
        }

        if (databaseCleanupFailure is not null)
            throw new InvalidOperationException("Persistent Real-E2E database cleanup failed.", databaseCleanupFailure);
    }

    private async Task<string> CreateIsolatedDatabaseAsync(string sourceConnectionString)
    {
        var source = new NpgsqlConnectionStringBuilder(sourceConnectionString);
        if (string.IsNullOrWhiteSpace(source.Database))
            throw new InvalidOperationException("Every local Real-E2E PostgreSQL connection must specify a database.");

        var databaseName = $"{source.Database}_e2e_{RunId}";
        if (databaseName.Length > 63)
            throw new InvalidOperationException($"The isolated PostgreSQL database name '{databaseName}' exceeds 63 characters.");
        if (databaseName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            throw new InvalidOperationException($"The isolated PostgreSQL database name '{databaseName}' contains unsupported characters.");
        if (string.IsNullOrWhiteSpace(source.Username))
            throw new InvalidOperationException("Every local Real-E2E PostgreSQL connection must specify a username.");
        var username = source.Username;

        var maintenance = new NpgsqlConnectionStringBuilder(sourceConnectionString)
        {
            Database = "postgres",
            Pooling = false,
            Timeout = 30
        };
        var wslcContainer = Environment.GetEnvironmentVariable("VERTEXBPMN_STUDIO_E2E_WSLC_POSTGRES_CONTAINER");
        if (!string.IsNullOrWhiteSpace(wslcContainer))
        {
            await RunWslcAsync("exec", wslcContainer!, "createdb", "-U", username, databaseName);
        }
        else
        {
            await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            try
            {
                await command.ExecuteNonQueryAsync();
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
            {
                throw new InvalidOperationException(
                    $"PostgreSQL user '{source.Username}' needs CREATEDB permission for isolated local Real-E2E databases.",
                    exception);
            }
        }

        _isolatedDatabases.Add(new(databaseName, maintenance.ConnectionString, username));
        source.Database = databaseName;
        return source.ConnectionString;
    }

    private async Task DropIsolatedDatabasesAsync()
    {
        if (_isolatedDatabases.Count == 0)
            return;

        NpgsqlConnection.ClearAllPools();
        var cleanupLog = new List<string>();
        foreach (var database in _isolatedDatabases.AsEnumerable().Reverse())
        {
            Exception? lastFailure = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await DropAndVerifyDatabaseAsync(database);
                    cleanupLog.Add($"Dropped and verified absent: {database.Name} (attempt {attempt})");
                    lastFailure = null;
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    break;
                }
                catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
                {
                    lastFailure = exception;
                    if (attempt < 3)
                    {
                        NpgsqlConnection.ClearAllPools();
                        await Task.Delay(TimeSpan.FromSeconds(attempt));
                    }
                }
            }

            if (lastFailure is not null)
                throw new InvalidOperationException(
                    $"Could not clean up isolated PostgreSQL database '{database.Name}' after three attempts.",
                    lastFailure);
        }

        var artifactRoot = Environment.GetEnvironmentVariable("VERTEXBPMN_STUDIO_E2E_ARTIFACTS")
                           ?? Path.Combine(_workingDirectory!, "artifacts");
        Directory.CreateDirectory(artifactRoot);
        await File.WriteAllLinesAsync(Path.Combine(artifactRoot, "database-cleanup.log"), cleanupLog);
        _isolatedDatabases.Clear();
    }

    private static async Task DropAndVerifyDatabaseAsync(IsolatedDatabase database)
    {
        var wslcContainer = Environment.GetEnvironmentVariable("VERTEXBPMN_STUDIO_E2E_WSLC_POSTGRES_CONTAINER");
        if (!string.IsNullOrWhiteSpace(wslcContainer))
        {
            await RunWslcAsync(
                "exec", wslcContainer!, "dropdb", "-U", database.Username,
                "--if-exists", "--force", database.Name);
            var exists = await RunWslcAsync(
                "exec", wslcContainer!, "psql", "-U", database.Username, "-d", "postgres",
                "-tAc", $"SELECT 1 FROM pg_database WHERE datname='{database.Name}'");
            if (string.Equals(exists.Trim(), "1", StringComparison.Ordinal))
                throw new InvalidOperationException($"Isolated PostgreSQL database '{database.Name}' still exists after cleanup.");
            return;
        }

        await using var connection = new NpgsqlConnection(database.MaintenanceConnectionString);
        await connection.OpenAsync();
        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{database.Name}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }

        await using var verify = connection.CreateCommand();
        verify.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)";
        verify.Parameters.AddWithValue("name", database.Name);
        var stillExists = (bool)(await verify.ExecuteScalarAsync() ?? true);
        if (stillExists)
            throw new InvalidOperationException($"Isolated PostgreSQL database '{database.Name}' still exists after cleanup.");
    }

    private static async Task<string> RunWslcAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("wslc.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("Could not start wslc.exe for PostgreSQL test-database management.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new InvalidOperationException("wslc.exe timed out while managing an isolated PostgreSQL database.");
        }

        var output = await standardOutput;
        var error = await standardError;
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"wslc.exe exited with code {process.ExitCode}: {string.Join(' ', arguments)}. {error}".Trim());
        return output;
    }

    private async Task ExecuteCleanupRequestsAsync()
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || ApiBaseAddress is null)
            return;

        using var client = CreateApiClient();
        while (_processDefinitionCleanups.TryPop(out var processCleanup))
        {
            var query = $"api/repository?key={Uri.EscapeDataString(processCleanup.ProcessKey)}";
            if (!string.IsNullOrWhiteSpace(processCleanup.TenantId))
                query += $"&tenantId={Uri.EscapeDataString(processCleanup.TenantId)}";

            try
            {
                var definitions = await client.GetFromJsonAsync<JsonElement[]>(query) ?? [];
                foreach (var definition in definitions)
                {
                    var id = definition.GetProperty("id").GetGuid();
                    var deleteUri = $"api/repository/{id}";
                    if (!string.IsNullOrWhiteSpace(processCleanup.TenantId))
                        deleteUri += $"?tenantId={Uri.EscapeDataString(processCleanup.TenantId)}";
                    using var response = await client.DeleteAsync(deleteUri);
                    if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                        _apiLogs.Enqueue($"Cleanup DELETE {deleteUri} returned HTTP {(int)response.StatusCode}.");
                }
            }
            catch (HttpRequestException exception)
            {
                _apiLogs.Enqueue($"Cleanup process {processCleanup.ProcessKey} failed: {exception.Message}");
            }
        }

        while (_cleanupRequests.TryPop(out var cleanup))
        {
            using var request = new HttpRequestMessage(cleanup.Method, cleanup.RelativeUri);
            if (!string.IsNullOrWhiteSpace(cleanup.TenantId))
                request.Headers.Add("X-Tenant-Id", cleanup.TenantId);

            try
            {
                using var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                    _apiLogs.Enqueue(
                        $"Cleanup {cleanup.Method} {cleanup.RelativeUri} returned HTTP {(int)response.StatusCode}.");
            }
            catch (HttpRequestException exception)
            {
                _apiLogs.Enqueue($"Cleanup {cleanup.Method} {cleanup.RelativeUri} failed: {exception.Message}");
            }
        }
    }

    private string GetScenarioArtifactDirectory(string scenarioName)
    {
        var root = Environment.GetEnvironmentVariable("VERTEXBPMN_STUDIO_E2E_ARTIFACTS")
                   ?? Path.Combine(_workingDirectory!, "artifacts");
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(scenarioName.Select(character =>
            invalidCharacters.Contains(character) ? '-' : character).ToArray());
        return Path.Combine(root, safeName);
    }

    private Process StartProject(
        string project,
        string configuration,
        Uri address,
        IReadOnlyDictionary<string, string?> environment,
        ConcurrentQueue<string> logs)
    {
        var dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrWhiteSpace(dotnetHost))
            dotnetHost = "dotnet";

        var startInfo = new ProcessStartInfo(dotnetHost)
        {
            WorkingDirectory = _workingDirectory!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
                 {
                     "run", "--project", project, "--configuration", configuration,
                     "--no-build", "--no-restore", "--no-launch-profile", "--urls", address.ToString()
                 })
            startInfo.ArgumentList.Add(argument);

        foreach (var (key, value) in environment)
            startInfo.Environment[key] = value;

        var process = Process.Start(startInfo)
                      ?? throw new InvalidOperationException($"Could not start {Path.GetFileNameWithoutExtension(project)}.");
        process.OutputDataReceived += (_, args) => EnqueueLog(logs, args.Data);
        process.ErrorDataReceived += (_, args) => EnqueueLog(logs, args.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static async Task WaitForEndpointAsync(
        Uri endpoint,
        Process process,
        ConcurrentQueue<string> logs,
        string description)
    {
        // Readiness includes a real RabbitMQ handshake. A very short HTTP timeout
        // cancels the health-check token and turns a healthy broker into a false negative.
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        string? lastResponse = null;
        var timeout = TimeSpan.FromSeconds(120);
        var started = Stopwatch.StartNew();
        while (started.Elapsed < timeout)
        {
            if (process.HasExited)
                throw new InvalidOperationException(
                    $"{description} process exited with code {process.ExitCode}.{Environment.NewLine}{string.Join(Environment.NewLine, logs)}");

            try
            {
                using var response = await client.GetAsync(endpoint);
                lastResponse = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException exception)
            {
                lastResponse = exception.Message;
            }
            catch (TaskCanceledException exception)
            {
                lastResponse = exception.Message;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException(
            $"{description} did not become ready at {endpoint}. Last response: {lastResponse}{Environment.NewLine}" +
            string.Join(Environment.NewLine, logs));
    }

    private static async Task TryDeleteWorkingDirectoryAsync(string directory)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException)
            {
                if (attempt == 19)
                    return;
                await Task.Delay(100);
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt == 19)
                    return;
                await Task.Delay(100);
            }
        }
    }

    private static async Task StopProcessAsync(Process? process)
    {
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
        catch (InvalidOperationException)
        {
            // The process already exited between the checks.
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void EnqueueLog(ConcurrentQueue<string> logs, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        logs.Enqueue(line);
        while (logs.Count > 2_000)
            logs.TryDequeue(out _);
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException(
            $"Required local E2E setting '{name}' is missing. Start the suite through scripts/test-studio-e2e.ps1.");

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed record BrowserArtifactSession(string ScenarioName, IBrowserContext Context)
    {
        public ConcurrentQueue<string> BrowserConsole { get; } = new();

        public ConcurrentQueue<string> FailedRequests { get; } = new();
    }

    private sealed record ApiCleanupRequest(HttpMethod Method, string RelativeUri, string? TenantId);

    private sealed record ProcessDefinitionCleanup(string ProcessKey, string? TenantId);

    private sealed record IsolatedDatabase(string Name, string MaintenanceConnectionString, string Username);
}
