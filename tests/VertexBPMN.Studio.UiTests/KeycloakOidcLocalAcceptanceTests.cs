using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

/// <summary>
/// Explicit local-only acceptance test against a real Keycloak, API and Studio.
/// Start it through scripts/keycloak-oidc-e2e.ps1; it is intentionally absent from CI.
/// </summary>
public sealed class KeycloakOidcLocalAcceptanceTests
{
    [Fact]
    [Trait("Category", "KeycloakOidcLocalAcceptance")]
    public async Task BrowserLogin_ApiBackedPage_SessionRefresh_AndLogout_WorkEndToEnd()
    {
        var studioUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL");
        var username = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER");
        var password = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = global::Chromium.Path
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync(studioUrl);
        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(password);
        await page.Locator("#kc-login").ClickAsync();

        await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
        await page.Locator("[data-testid='dashboard-refresh']").WaitForAsync();
        Assert.DoesNotContain("Failed to load dashboard data", await page.Locator("body").InnerTextAsync());

        await page.GotoAsync(new Uri(new Uri(studioUrl), "process-definitions").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Process Definitions", Exact = true }).First.WaitForAsync();
        Assert.DoesNotContain("Failed to load", await page.Locator("body").InnerTextAsync());

        // The local orchestrator configures a 70-second access-token lifetime.
        // Crossing the 60-second server refresh window forces a real token-endpoint call.
        await Task.Delay(TimeSpan.FromSeconds(12), TestContext.Current.CancellationToken);
        var refreshJson = await page.EvaluateAsync<string>("""
            async () => {
                const token = document.querySelector('meta[name="vertexbpmn-antiforgery"]')?.content;
                if (!token) throw new Error('The OIDC antiforgery token is missing.');
                const response = await fetch('/authentication/session/refresh', {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': token }
                });
                return JSON.stringify({ status: response.status, body: await response.text() });
            }
            """);
        using (var refresh = JsonDocument.Parse(refreshJson))
        {
            Assert.Equal(200, refresh.RootElement.GetProperty("status").GetInt32());
            using var body = JsonDocument.Parse(refresh.RootElement.GetProperty("body").GetString()!);
            Assert.True(body.RootElement.GetProperty("renewed").GetBoolean());
        }

        await page.ReloadAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Process Definitions", Exact = true }).First.WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true }).ClickAsync();
        await page.Locator("#username").WaitForAsync();
        Assert.Contains("/realms/vertexbpmn/", page.Url, StringComparison.Ordinal);
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{name} is required. Run this local-only test through scripts/keycloak-oidc-e2e.ps1.");
}
