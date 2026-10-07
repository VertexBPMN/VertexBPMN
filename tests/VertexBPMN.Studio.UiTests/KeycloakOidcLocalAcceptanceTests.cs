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
    public async Task SessionRefresh_WithoutSession_ReturnsUnauthorizedWithoutRedirect()
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(new Uri(RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL")), "authentication/session/refresh"));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

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
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC: browser launched");

        await GotoStudioAsync(page, studioUrl);
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC: login page opened");
        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(password);
        await page.Locator("#kc-login").ClickAsync();
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC: login submitted");

        await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
        await page.Locator("[data-testid='dashboard-refresh']").WaitForAsync();
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC: dashboard loaded");
        Assert.DoesNotContain("Failed to load dashboard data", await page.Locator("body").InnerTextAsync());

        await page.GotoAsync(new Uri(new Uri(studioUrl), "process-definitions").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Process Definitions", Exact = true }).First.WaitForAsync();
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC: process definitions loaded");
        Assert.DoesNotContain("Failed to load", await page.Locator("body").InnerTextAsync());

        // The local orchestrator configures a deliberately short token lifetime.
        // It is inside the server's 60-second refresh window and forces a real token-endpoint call.
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
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC: logout completed");
        Assert.Contains("/realms/vertexbpmn/", page.Url, StringComparison.Ordinal);
    }

    private static async Task GotoStudioAsync(IPage page, string studioUrl)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await page.GotoAsync(studioUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 45_000
                });
                return;
            }
            catch (PlaywrightException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), TestContext.Current.CancellationToken);
            }
            catch (TimeoutException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), TestContext.Current.CancellationToken);
            }
        }
    }

    [Fact]
    [Trait("Category", "KeycloakOidcLocalAcceptance")]
    public async Task AutomaticSessionRenewal_PreservesUnsavedEditorDraft_AndTenantContext()
    {
        var studioUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true, ExecutablePath = global::Chromium.Path
        });
        var page = await browser.NewPageAsync();
        await GotoStudioAsync(page, studioUrl);
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC draft: login page opened");
        await page.Locator("#username").FillAsync(RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER"));
        await page.Locator("#password").FillAsync(RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD"));
        await page.Locator("#kc-login").ClickAsync();
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC draft: login submitted");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
        await page.GotoAsync(new Uri(new Uri(studioUrl), "form-builder").ToString());
        await page.Locator(".studio-layout[data-interactive-ready='true']").WaitForAsync();
        var formName = page.GetByLabel("Form name", new() { Exact = true });
        var draftName = $"Unsaved OIDC acceptance draft {Guid.NewGuid():N}";
        await formName.FillAsync(draftName);
        await formName.PressAsync("Tab");
        await Assertions.Expect(formName).ToHaveValueAsync(draftName);
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC draft: unsaved form entered");
        var tenant = page.GetByRole(AriaRole.Combobox, new() { Name = "Tenant", Exact = true });
        var originalTenant = await tenant.InnerTextAsync();
        var navigations = 0;
        page.FrameNavigated += (_, frame) => { if (frame == page.MainFrame) Interlocked.Increment(ref navigations); };

        // Wait for the real periodic browser renewal, not an artificial fetch/reload.
        // A short token lifetime (e.g. 70 s) ensures this renews a token/cookie.
        var renewed = await page.WaitForResponseAsync(response => response.Url.EndsWith("/authentication/session/refresh", StringComparison.Ordinal)
            && response.Request.Method == "POST", new() { Timeout = 75_000 });
        Assert.Equal(200, renewed.Status);
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC draft: automatic renewal received");
        using var renewal = JsonDocument.Parse(await renewed.TextAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC draft: renewal body read");
        Assert.True(renewal.RootElement.GetProperty("renewed").GetBoolean(), "Use the isolated Keycloak test realm with a short token lifetime.");
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Equal(0, navigations);
        await Assertions.Expect(formName).ToHaveValueAsync(draftName);
        Assert.Equal(originalTenant, await tenant.InnerTextAsync());
        Assert.DoesNotContain("Sign in to your account", await page.Locator("body").InnerTextAsync());
        TestContext.Current.TestOutputHelper!.WriteLine("OIDC draft: draft and tenant preserved without navigation");
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{name} is required. Run this local-only test through scripts/keycloak-oidc-e2e.ps1.");
}
