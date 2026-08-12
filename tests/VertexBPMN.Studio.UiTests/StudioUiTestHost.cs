using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

public sealed class StudioUiTestHost : IAsyncLifetime
{
    private readonly ConcurrentQueue<string> _apiRequests = new();
    private WebApplication? _api;
    private Process? _studioProcess;
    private IPlaywright? _playwright;

    public Uri BaseAddress { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public IReadOnlyList<string> ApiRequests => _apiRequests.ToArray();

    public async ValueTask InitializeAsync()
    {
        var apiPort = GetFreePort();
        var studioPort = GetFreePort();
        var apiAddress = new Uri($"http://127.0.0.1:{apiPort}/");
        BaseAddress = new Uri($"http://127.0.0.1:{studioPort}/");

        var apiBuilder = WebApplication.CreateBuilder();
        apiBuilder.WebHost.UseUrls(apiAddress.ToString());
        _api = apiBuilder.Build();
        _api.Use(async (context, next) =>
        {
            _apiRequests.Enqueue($"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
            await next();
        });
        MapApiContracts(_api);
        await _api.StartAsync();

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var studioProject = Path.Combine(repoRoot, "src", "VertexBPMN.Studio", "VertexBPMN.Studio.csproj");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(studioProject);
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(BaseAddress.ToString());
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "UiTest";
        startInfo.Environment["ApiBaseUrl"] = apiAddress.ToString();
        startInfo.Environment["StudioAuthentication__Authority"] = "https://ui-test.invalid";
        startInfo.Environment["StudioAuthentication__ClientId"] = "ui-test";
        startInfo.Environment["StudioAuthentication__UiTestEnabled"] = "true";

        _studioProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Studio test host.");
        _ = _studioProcess.StandardOutput.ReadToEndAsync();
        _ = _studioProcess.StandardError.ReadToEndAsync();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var attempt = 0; attempt < 80; attempt++)
        {
            if (_studioProcess.HasExited)
                throw new InvalidOperationException("The Studio test host exited before becoming ready.");

            try
            {
                using var response = await client.GetAsync(BaseAddress);
                if (response.IsSuccessStatusCode)
                    break;
            }
            catch (HttpRequestException)
            {
                // Kestrel is still starting.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
            if (attempt == 79)
                throw new TimeoutException("The Studio test host did not become ready.");
        }

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
            await Browser.CloseAsync();
        _playwright?.Dispose();

        if (_studioProcess is { HasExited: false })
        {
            try
            {
                _studioProcess.Kill(entireProcessTree: true);
                await _studioProcess.WaitForExitAsync();
            }
            catch (InvalidOperationException)
            {
                // The process exited between the check and Kill.
            }
        }
        _studioProcess?.Dispose();

        if (_api is not null)
            await _api.StopAsync();
        if (_api is not null)
            await _api.DisposeAsync();
    }

    private static void MapApiContracts(WebApplication app)
    {
        app.MapGet("/api/repository", () => Results.Json(new[]
        {
            new
            {
                id = "11111111-1111-1111-1111-111111111111",
                key = "InvoiceProcess",
                name = "Invoice approval",
                version = 1,
                bpmnXml = "",
                tenantId = "tenant-a",
                createdAt = "2026-08-12T10:00:00Z",
                deploymentId = "22222222-2222-2222-2222-222222222222"
            }
        }));
        app.MapGet("/api/runtime", () => Results.Json(new[]
        {
            new
            {
                id = "33333333-3333-3333-3333-333333333333",
                processDefinitionId = "11111111-1111-1111-1111-111111111111",
                businessKey = "invoice-1001",
                tenantId = "tenant-a",
                startedAt = "2026-08-12T10:05:00Z",
                state = "Active",
                instanceId = "instance-1001",
                processId = "InvoiceProcess",
                activeTasks = Array.Empty<string>(),
                activeTokens = Array.Empty<string>(),
                variables = new Dictionary<string, object>(),
                createdAt = "2026-08-12T10:05:00Z",
                lastModified = "2026-08-12T10:05:00Z"
            }
        }));
        app.MapGet("/api/task", () => Results.Json(new[]
        {
            new
            {
                id = "44444444-4444-4444-4444-444444444444",
                processInstanceId = "33333333-3333-3333-3333-333333333333",
                name = "Approve invoice",
                type = "userTask",
                tenantId = "tenant-a",
                createdAt = "2026-08-12T10:06:00Z",
                lastModified = "2026-08-12T10:06:00Z",
                candidateRole = "approver",
                candidateUsers = Array.Empty<string>(),
                requiredFields = Array.Empty<string>()
            }
        }));
        app.MapGet("/api/engine/capabilities", () => Results.Json(new
        {
            engineType = "Simple",
            supportsCmmn = true,
            supportsWorkers = false,
            supportsDurablePersistence = true
        }));
        app.MapGet("/api/identity/list-tenants", () => Results.Json(new[]
        {
            new { id = "tenant-a", name = "Tenant A", description = "UI test tenant" }
        }));
        app.MapGet("/api/engine/connections", () => Results.Json(Array.Empty<object>()));
        app.MapGet("/api/history", () => Results.Json(Array.Empty<object>()));
        app.MapMethods("/api/{**path}", ["GET", "POST", "PUT", "DELETE", "PATCH"], () => Results.Json(Array.Empty<object>()));
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
