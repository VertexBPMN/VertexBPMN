using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VertexBPMN.Cli;

internal sealed class DashboardLauncher
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DashboardLauncher> _logger;
    private readonly List<Process> _ownedProcesses = new();

    public DashboardLauncher(IConfiguration configuration, ILogger<DashboardLauncher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        var apiUrl = GetValue("Dashboard:ApiUrl", "http://localhost:51870/");
        var studioUrl = GetValue("Dashboard:StudioUrl", "http://localhost:5263/");

        if (GetValue("Dashboard:AutoStartApi", true) && !await IsApiReadyAsync(apiUrl, cancellationToken))
            StartProject("Dashboard:ApiProject", "src/VertexBPMN.Api/VertexBPMN.Api.csproj", apiUrl);

        await WaitForApiAsync(apiUrl, cancellationToken);

        if (GetValue("Dashboard:AutoStartStudio", true))
            StartProject("Dashboard:StudioProject", "src/VertexBPMN.Studio/VertexBPMN.Studio.csproj", studioUrl,
                ("ApiBaseUrl", apiUrl));

        if (GetValue("Dashboard:OpenBrowser", true))
            OpenBrowser(studioUrl);

        await Console.Out.WriteLineAsync($"Dashboard: {studioUrl}");
    }

    public void StopOwnedProcesses()
    {
        foreach (var process in _ownedProcesses.Where(process => !process.HasExited))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Could not stop dashboard process {ProcessId}", process.Id);
            }
            finally
            {
                process.Dispose();
            }
        }

        _ownedProcesses.Clear();
    }

    private async Task WaitForApiAsync(string apiUrl, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(GetValue("Dashboard:WaitTimeoutSeconds", 30));
        var deadline = DateTime.UtcNow + timeout;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsApiReadyAsync(apiUrl, cancellationToken, client))
                return;
            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException($"VertexBPMN API did not become ready at {apiUrl}.");
    }

    private static async Task<bool> IsApiReadyAsync(
        string apiUrl,
        CancellationToken cancellationToken,
        HttpClient? client = null)
    {
        using var ownedClient = client is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(2) } : null;
        var httpClient = client ?? ownedClient!;

        try
        {
            using var response = await httpClient.GetAsync(
                new Uri(new Uri(apiUrl), "api/Health"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (
            exception is HttpRequestException ||
            exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private void StartProject(string configurationKey, string defaultProject, string url, params (string Key, string Value)[] environment)
    {
        var projectPath = ResolveProjectPath(GetValue(configurationKey, defaultProject));
        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Dashboard project was not found: {projectPath}", projectPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(projectPath)!,
            CreateNoWindow = false
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.Environment["ASPNETCORE_URLS"] = url;
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        foreach (var (key, value) in environment)
            startInfo.Environment[key] = value;

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start dashboard project: {projectPath}");
        _ownedProcesses.Add(process);
        _logger.LogInformation("Started dashboard project {ProjectPath} with process {ProcessId}", projectPath, process.Id);
    }

    private static void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static string ResolveProjectPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
            return Path.GetFullPath(configuredPath);

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, configuredPath);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
    }

    private string GetValue(string key, string fallback) =>
        _configuration[key] ?? fallback;

    private bool GetValue(string key, bool fallback) =>
        bool.TryParse(_configuration[key], out var value) ? value : fallback;

    private int GetValue(string key, int fallback) =>
        int.TryParse(_configuration[key], out var value) ? value : fallback;
}