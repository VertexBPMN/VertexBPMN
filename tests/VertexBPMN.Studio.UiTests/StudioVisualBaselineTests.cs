using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

/// <summary>
/// Captures every Studio route at the approved reference viewports for local visual comparison.
/// The test is intentionally opt-in and does not mutate product data.
/// </summary>
public sealed class StudioVisualBaselineTests(StudioUiTestHost host) : IClassFixture<StudioUiTestHost>
{
    private const string EnabledEnvironmentVariable = "VERTEXBPMN_UI_BASELINE_TESTS";

    [Fact]
    [Trait("Category", "LocalUiBaseline")]
    public async Task CaptureAllStudioPagesAtReferenceViewports()
    {
        Assert.SkipUnless(
            string.Equals(Environment.GetEnvironmentVariable(EnabledEnvironmentVariable), "true", StringComparison.OrdinalIgnoreCase),
            $"Set {EnabledEnvironmentVariable}=true to capture the local visual baseline.");

        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var outputDirectory = Path.Combine(
            repositoryRoot,
            "tests",
            "VertexBPMN.Studio.UiTests",
            "TestResults",
            "ui-modernization-baseline");
        Directory.CreateDirectory(outputDirectory);

        var scenarios = new[]
        {
            new BaselineScenario("dashboard", "", "Dashboard", null),
            new BaselineScenario("bpmn-modeler", "bpmn-modeler", "BPMN Modeler", "bpmn-modeler-shell"),
            new BaselineScenario("dmn-modeler", "dmn-modeler", "DMN Modeler", "dmn-modeler-shell"),
            new BaselineScenario("cmmn-modeler", "cmmn-modeler", "CMMN Modeler", "cmmn-modeler-shell"),
            new BaselineScenario("form-builder", "form-builder", "Form Builder", "form-builder-shell"),
            new BaselineScenario("tasks", "tasks", "Tasks", null),
            new BaselineScenario("process-definitions", "process-definitions", "Process Definitions", "process-definitions-grid"),
            new BaselineScenario("process-instances", "process-instances", "Process Instances", null),
            new BaselineScenario("deployments", "deployments", "Deployments", "deployments-table"),
            new BaselineScenario("history", "history", "History", null),
            new BaselineScenario("execution-details", "execution-details", "Execution Details", null),
            new BaselineScenario("workflow-triggers", "triggers", "Workflow Triggers", null),
            new BaselineScenario("messages-signals", "messages-signals", "Messages & Signals", null),
            new BaselineScenario("event-log", "event-log", "Event Log", "persistent-event-log-table"),
            new BaselineScenario("analytics", "analytics", "Process Analytics", null),
            new BaselineScenario("performance", "performance", "Performance", null),
            new BaselineScenario("health", "health", "Health and Operations", null),
            new BaselineScenario("simulation", "simulation", "Simulation", null),
            new BaselineScenario("debugging", "debugging", "Debugging Trace", null),
            new BaselineScenario("compliance", "compliance", "Compliance Evidence", null),
            new BaselineScenario("engine-management", "engine-management", "Engine Management", null),
            new BaselineScenario("tenants", "tenants", "Tenants", null),
            new BaselineScenario("credentials", "credentials", "Credentials", null),
            new BaselineScenario("configuration", "configuration", "Configuration", null),
            new BaselineScenario("feature-flags", "feature-flags", "Feature Flags", null),
            new BaselineScenario("migration", "migration", "Process Migration", null),
            new BaselineScenario("connectors", "connectors", "Connectors", null),
            new BaselineScenario("extensions", "extensions", "Extensions", null),
            new BaselineScenario("sso", "sso", "Single Sign-On (SSO)", null),
            new BaselineScenario("counter", "counter", "Counter", null),
            new BaselineScenario("error", "Error", "Something went wrong", null)
        };
        var viewports = new[]
        {
            new BaselineViewport("desktop", 1440, 900),
            new BaselineViewport("desktop-compact", 1280, 800),
            new BaselineViewport("tablet", 1024, 768),
            new BaselineViewport("mobile", 390, 844)
        };
        var results = new List<BaselineResult>();

        foreach (var viewport in viewports)
        {
            foreach (var scenario in scenarios)
            {
                var browserErrors = new ConcurrentQueue<string>();
                var page = await host.Browser.NewPageAsync();
                page.PageError += (_, error) => browserErrors.Enqueue(error);
                page.Console += (_, message) =>
                {
                    if (message.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
                        browserErrors.Enqueue($"console: {message.Text}");
                };

                try
                {
                    await page.SetViewportSizeAsync(viewport.Width, viewport.Height);
                    var navigationStarted = DateTimeOffset.UtcNow;
                    var response = await page.GotoAsync($"{host.BaseAddress}{scenario.Route}");
                    var navigationMilliseconds = (DateTimeOffset.UtcNow - navigationStarted).TotalMilliseconds;
                    Assert.NotNull(response);
                    Assert.True(response.Ok, $"{scenario.Route} returned HTTP {response.Status}.");
                    await page.GetByRole(AriaRole.Heading, new() { Name = scenario.Heading, Exact = true }).First.WaitForAsync();
                    if (scenario.ReadyTestId is not null)
                        await page.GetByTestId(scenario.ReadyTestId).WaitForAsync();
                    var readyMilliseconds = (DateTimeOffset.UtcNow - navigationStarted).TotalMilliseconds;

                    await Task.Delay(350, TestContext.Current.CancellationToken);
                    var layout = await page.EvaluateAsync<BaselineLayout>("""
                        () => ({
                            viewportWidth: window.innerWidth,
                            viewportHeight: window.innerHeight,
                            documentWidth: document.documentElement.scrollWidth,
                            documentHeight: document.documentElement.scrollHeight,
                            visibleNavigationLinks: [...document.querySelectorAll('nav a')]
                                .filter(element => {
                                    const style = getComputedStyle(element);
                                    const bounds = element.getBoundingClientRect();
                                    return style.visibility !== 'hidden' && style.display !== 'none'
                                        && bounds.width > 0 && bounds.height > 0;
                                }).length
                        })
                        """);

                    var fileName = $"{scenario.Name}-{viewport.Name}.png";
                    await page.ScreenshotAsync(new()
                    {
                        Path = Path.Combine(outputDirectory, fileName),
                        FullPage = true
                    });
                    results.Add(new BaselineResult(
                        scenario.Name,
                        viewport.Name,
                        fileName,
                        navigationMilliseconds,
                        readyMilliseconds,
                        layout));
                    Assert.Equal(layout.ViewportWidth, layout.DocumentWidth);
                    Assert.Empty(browserErrors);
                }
                finally
                {
                    await page.CloseAsync();
                }
            }
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "layout-metrics.json"),
            JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }),
            TestContext.Current.CancellationToken);
    }

    private sealed record BaselineScenario(string Name, string Route, string Heading, string? ReadyTestId);
    private sealed record BaselineViewport(string Name, int Width, int Height);
    private sealed record BaselineResult(
        string Scenario,
        string Viewport,
        string Screenshot,
        double NavigationMilliseconds,
        double ReadyMilliseconds,
        BaselineLayout Layout);
    private sealed class BaselineLayout
    {
        public int ViewportWidth { get; set; }
        public int ViewportHeight { get; set; }
        public int DocumentWidth { get; set; }
        public int DocumentHeight { get; set; }
        public int VisibleNavigationLinks { get; set; }
    }
}
