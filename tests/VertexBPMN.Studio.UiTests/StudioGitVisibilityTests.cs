using System.Net;
using Microsoft.Playwright;
using VertexBPMN.SourceControl.Abstractions;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

/// <summary>Local browser regression; no external Git provider or repository writes.</summary>
public sealed class StudioGitVisibilityTests
{
    [Theory]
    [Trait("Category", "LocalUiAcceptance")]
    [InlineData(null, true, HttpStatusCode.OK)]
    [InlineData(SourceControlErrorCode.Disabled, false, HttpStatusCode.OK)]
    [InlineData(SourceControlErrorCode.GitUnavailable, true, HttpStatusCode.OK)]
    [InlineData(null, true, HttpStatusCode.ServiceUnavailable)]
    public async Task Git_workspace_visibility_follows_api_configuration(SourceControlErrorCode? reason,
        bool visible, HttpStatusCode statusCode)
    {
        var host = new StudioUiTestHost
        {
            GitAvailability = new(reason is null, SourceControlCapability.None, reason),
            GitAvailabilityStatusCode = statusCode
        };
        try
        {
            await host.InitializeAsync();
            var page = await host.Browser.NewPageAsync();
            await page.GotoAsync($"{host.BaseAddress}bpmn-modeler");
            await page.GetByTestId("bpmn-modeler-shell").WaitForAsync();
            await Assertions.Expect(page.Locator(".djs-container").First).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("git-workspace-availability")).ToHaveAttributeAsync("data-checked", "true",
                new() { Timeout = 15000 });
            if (visible)
            {
                await Assertions.Expect(page.GetByTestId("git-workspace")).ToBeVisibleAsync();
                if (statusCode != HttpStatusCode.OK)
                {
                    await page.GetByTestId("git-workspace").ClickAsync();
                    await Assertions.Expect(page.GetByTestId("git-error")).ToBeVisibleAsync();
                }
            }
            else
            {
                // A missing panel alone could also mean initialization has not run.
                Assert.Contains(host.ApiRequests, request => request == "GET /api/source-control/availability");
                await Assertions.Expect(page.GetByTestId("git-workspace")).ToHaveCountAsync(0);
            }
            Assert.DoesNotContain(host.ApiRequests, request =>
                request.StartsWith("POST /api/source-control", StringComparison.Ordinal)
                || request.StartsWith("DELETE /api/source-control", StringComparison.Ordinal));
        }
        finally
        {
            await host.DisposeAsync();
        }
    }
}
