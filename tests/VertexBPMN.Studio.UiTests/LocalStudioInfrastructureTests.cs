using System.Collections.Concurrent;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

[Trait("Category", "LocalStudioE2E")]
public sealed class LocalStudioInfrastructureTests(LocalStudioE2ETestHost host)
    : IClassFixture<LocalStudioE2ETestHost>
{
    [Fact]
    public async Task RealApiAndStudio_BecomeReady_AndDashboardUsesTheRealBackend()
    {
        Assert.SkipUnless(
            LocalStudioE2ETestHost.IsEnabled,
            "Local real E2E tests run only through scripts/test-studio-e2e.ps1.");

        using var apiClient = new HttpClient { BaseAddress = host.ApiBaseAddress };
        using var readiness = await apiClient.GetAsync("api/ready", TestContext.Current.CancellationToken);
        var readinessBody = await readiness.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(readiness.IsSuccessStatusCode, readinessBody);

        var browserErrors = new ConcurrentQueue<string>();
        var failedRequests = new ConcurrentQueue<string>();
        var page = await host.Browser.NewPageAsync();
        page.PageError += (_, error) => browserErrors.Enqueue(error);
        page.RequestFailed += (_, request) => failedRequests.Enqueue(
            $"{request.Method} {request.Url}: {request.Failure}");

        try
        {
            var response = await page.GotoAsync(host.StudioBaseAddress.ToString());
            Assert.NotNull(response);
            Assert.True(response.Ok, $"Studio returned HTTP {response.Status}.");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
            await page.GetByText("VertexBPMN Studio", new() { Exact = true }).First.WaitForAsync();

            Assert.Empty(browserErrors);
            Assert.Empty(failedRequests);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
