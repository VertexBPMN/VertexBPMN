using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.Playwright;
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
    private Process? _apiProcess;
    private Process? _studioProcess;
    private IPlaywright? _playwright;
    private string? _workingDirectory;

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
        var apiKey = $"local-studio-e2e-{RunId}";

        ApiBaseAddress = new Uri($"http://127.0.0.1:{GetFreePort()}/");
        StudioBaseAddress = new Uri($"http://127.0.0.1:{GetFreePort()}/");

        var apiEnvironment = new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["OperationalMode"] = "Development",
            ["PathBase"] = string.Empty,
            ["Database__ApplyMigrationsOnStartup"] = "true",
            ["ConnectionStrings__BpmnDbContext"] = RequiredEnvironment("VERTEXBPMN_E2E_BPMN_CONNECTION"),
            ["ConnectionStrings__TenantDbContext"] = RequiredEnvironment("VERTEXBPMN_E2E_TENANT_CONNECTION"),
            ["ConnectionStrings__SimulationScenarioDbContext"] = RequiredEnvironment("VERTEXBPMN_E2E_SIMULATION_CONNECTION"),
            ["ConnectionStrings__ProcessMiningEvents"] = RequiredEnvironment("VERTEXBPMN_E2E_EVENTS_CONNECTION"),
            ["ConnectionStrings__DecisionDbContext"] = RequiredEnvironment("VERTEXBPMN_E2E_DECISION_CONNECTION"),
            ["ConnectionStrings__DependencyRegistry"] = $"Data Source={Path.Combine(_workingDirectory, "dependencies.db")}",
            ["ConnectionStrings__messaging"] = RequiredEnvironment("VERTEXBPMN_E2E_RABBITMQ_CONNECTION"),
            ["Runtime__Outbox__Enabled"] = "true",
            ["Runtime__Outbox__Provider"] = "RabbitMq",
            ["Modules__Telemetry"] = "false",
            ["Modules__Plugins"] = "false",
            ["Dependencies__Plugins__Enabled"] = "false",
            ["Dependencies__Ai__Enabled"] = "false",
            ["Dependencies__Mcp__Enabled"] = "false",
            ["AdvancedFeatures__SimulationExecution"] = "true",
            ["AdvancedFeatures__LiveProcessMigration"] = "true",
            ["AdvancedFeatures__CmmnExecution"] = "true",
            ["Jwt__Audience"] = "vertexbpmn-api",
            ["Jwt__UseDevelopmentApiKey"] = "true",
            ["ApiKeys__0"] = apiKey
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
            ["StudioAuthentication__DevelopmentApiKey"] = apiKey,
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
        try
        {
            if (Browser is not null)
                await Browser.CloseAsync();
        }
        finally
        {
            _playwright?.Dispose();
            await StopProcessAsync(_studioProcess);
            await StopProcessAsync(_apiProcess);

            if (!string.IsNullOrWhiteSpace(_workingDirectory) && Directory.Exists(_workingDirectory))
                await TryDeleteWorkingDirectoryAsync(_workingDirectory);
        }
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
}
