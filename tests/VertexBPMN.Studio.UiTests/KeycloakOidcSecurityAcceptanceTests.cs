using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

/// <summary>
/// Local-only K05 contracts against tokens issued by the real isolated Keycloak realm.
/// The dedicated password-grant client exists only in that disposable acceptance realm;
/// the Studio browser login continues to use Authorization Code with PKCE.
/// </summary>
public sealed class KeycloakOidcSecurityAcceptanceTests
{
    [Fact]
    [Trait("Category", "KeycloakOidcSecurityAcceptance")]
    public async Task T07_TotpEnrollmentAndLogin_RequireAValidSecondFactor()
    {
        var studioUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL");
        var password = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD");
        const string username = "vertexbpmn-mfa-user";

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = global::Chromium.Path
        });
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await GotoStudioAsync(page, studioUrl);
        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(password);
        await ClickWithoutImplicitNavigationWaitAsync(page.Locator("#kc-login"));

        var manualMode = page.Locator("#mode-manual");
        await manualMode.WaitForAsync();
        await ClickWithoutImplicitNavigationWaitAsync(manualMode);
        var secretElement = page.Locator("#kc-totp-secret-key");
        await secretElement.WaitForAsync();
        await AssertNoStudioSessionAsync(context, studioUrl);
        var blockedPage = await context.NewPageAsync();
        await blockedPage.GotoAsync(new Uri(new Uri(studioUrl), "connectors").ToString());
        Assert.Contains("/realms/vertexbpmn/", blockedPage.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("Connectors", await blockedPage.Locator("body").InnerTextAsync(), StringComparison.Ordinal);
        await blockedPage.CloseAsync();

        var secret = NormalizeTotpSecret(await secretElement.InnerTextAsync());
        var validEnrollmentCode = Totp(secret, DateTimeOffset.UtcNow);
        var invalidEnrollmentCode = DifferentTotp(validEnrollmentCode);
        await page.Locator("#totp").FillAsync(invalidEnrollmentCode);
        await page.Locator("#userLabel").FillAsync("VertexBPMN local acceptance");
        await ClickWithoutImplicitNavigationWaitAsync(page.Locator("#saveTOTPBtn"));
        await secretElement.WaitForAsync();
        await AssertNoStudioSessionAsync(context, studioUrl);

        validEnrollmentCode = Totp(secret, DateTimeOffset.UtcNow);
        await page.Locator("#totp").FillAsync(validEnrollmentCode);
        await ClickWithoutImplicitNavigationWaitAsync(page.Locator("#saveTOTPBtn"));
        await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true }).ClickAsync();
        await page.Locator("#username").WaitForAsync();
        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(password);
        await ClickWithoutImplicitNavigationWaitAsync(page.Locator("#kc-login"));

        var otp = page.Locator("#otp");
        await otp.WaitForAsync();
        await AssertNoStudioSessionAsync(context, studioUrl);
        var currentCode = Totp(secret, DateTimeOffset.UtcNow);
        await otp.FillAsync(DifferentTotp(currentCode));
        await ClickWithoutImplicitNavigationWaitAsync(page.Locator("#kc-login"));
        await otp.WaitForAsync();
        await AssertNoStudioSessionAsync(context, studioUrl);

        await WaitForUnusedTotpWindowAsync(secret, validEnrollmentCode);
        await otp.FillAsync(Totp(secret, DateTimeOffset.UtcNow));
        await ClickWithoutImplicitNavigationWaitAsync(page.Locator("#kc-login"));
        await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
    }

    [Fact]
    [Trait("Category", "KeycloakOidcSecurityAcceptance")]
    public async Task T06_RoleRevocation_RestrictsApiForTheStillOpenCircuitAfterRefresh()
    {
        var authority = RequiredEnvironment("Jwt__Authority").TrimEnd('/');
        var studioUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL");
        var apiUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_API_URL");
        var clientId = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_SECURITY_CLIENT_ID");
        var clientSecret = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_SECURITY_CLIENT_SECRET");
        var password = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD");
        var lifespan = RequiredTokenLifespan();
        const string username = "vertexbpmn-role-revocation";

        using var keycloak = new HttpClient();
        using var api = new HttpClient { BaseAddress = new Uri(apiUrl) };
        using var admin = await CreateKeycloakAdminClientAsync(keycloak, authority);
        var role = await GetClientRoleAsync(admin, username, "ProcessManager");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = global::Chromium.Path
        });
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await LoginAsync(page, studioUrl, username, password);
        await page.Locator("[data-testid='dashboard-refresh']").WaitForAsync();
        await WaitForRefreshWindowAsync(lifespan);

        await SetClientRoleAsync(admin, role, assign: false);
        try
        {
            using var refresh = await RefreshBrowserSessionAsync(page);
            Assert.Equal(200, refresh.RootElement.GetProperty("status").GetInt32());
            using (var body = JsonDocument.Parse(refresh.RootElement.GetProperty("body").GetString()!))
                Assert.True(body.RootElement.GetProperty("renewed").GetBoolean());

            var tokenWithoutRole = await PasswordAccessTokenAsync(
                keycloak,
                authority,
                clientId,
                clientSecret,
                username,
                password);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await SendAsync(api, HttpMethod.Get, "api/connector-templates?tenantId=tenant-a", tokenWithoutRole)).StatusCode);

            await page.Locator("a[href='/connectors']:visible").ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Connectors", Exact = true }).First.WaitForAsync();
            await page.GetByText(
                "Response status code does not indicate success: 403",
                new() { Exact = false }).WaitForAsync();
        }
        finally
        {
            await SetClientRoleAsync(admin, role, assign: true);
        }
    }

    [Fact]
    [Trait("Category", "KeycloakOidcSecurityAcceptance")]
    public async Task T06_DisabledAccount_DestroysTheRefreshableStudioSession()
    {
        var authority = RequiredEnvironment("Jwt__Authority").TrimEnd('/');
        var studioUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL");
        var password = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD");
        var lifespan = RequiredTokenLifespan();
        const string username = "vertexbpmn-account-lock";

        using var keycloak = new HttpClient();
        using var admin = await CreateKeycloakAdminClientAsync(keycloak, authority);
        var userId = await GetKeycloakEntityIdAsync(admin, $"users?username={username}&exact=true");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = global::Chromium.Path
        });
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await LoginAsync(page, studioUrl, username, password);
        await WaitForRefreshWindowAsync(lifespan);

        await SetUserEnabledAsync(admin, userId, enabled: false);
        try
        {
            using var refresh = await RefreshBrowserSessionAsync(page, manualRedirect: true);
            Assert.Equal("opaqueredirect", refresh.RootElement.GetProperty("type").GetString());
            Assert.Equal(0, refresh.RootElement.GetProperty("status").GetInt32());

            var cookies = await context.CookiesAsync([studioUrl]);
            Assert.DoesNotContain(cookies, cookie => cookie.Name == ".AspNetCore.Cookies");

            using var rejectedLogin = await PostTokenAsync(
                keycloak,
                $"{authority}/protocol/openid-connect/token",
                new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_SECURITY_CLIENT_ID"),
                    ["client_secret"] = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_SECURITY_CLIENT_SECRET"),
                    ["username"] = username,
                    ["password"] = password
                });
            Assert.Equal(HttpStatusCode.BadRequest, rejectedLogin.StatusCode);

            await page.GotoAsync(studioUrl);
            Assert.Contains("/realms/vertexbpmn/", page.Url, StringComparison.Ordinal);
            Assert.DoesNotContain("Dashboard", await page.Locator("body").InnerTextAsync(), StringComparison.Ordinal);
        }
        finally
        {
            await SetUserEnabledAsync(admin, userId, enabled: true);
        }
    }

    [Fact]
    [Trait("Category", "KeycloakOidcSecurityAcceptance")]
    public async Task T04_OpenCircuit_SerializesParallelRefreshAcrossRealTokenExpiry()
    {
        var studioUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL");
        var password = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD");
        var lifespan = int.Parse(
            RequiredEnvironment("VERTEXBPMN_KEYCLOAK_ACCESS_TOKEN_LIFESPAN"),
            System.Globalization.CultureInfo.InvariantCulture);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = global::Chromium.Path
        });
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await LoginAsync(page, studioUrl, "vertexbpmn-user", password);
        await page.Locator("[data-testid='dashboard-refresh']").WaitForAsync();

        await Task.Delay(TimeSpan.FromSeconds(lifespan + 2), TestContext.Current.CancellationToken);
        var refreshJson = await page.EvaluateAsync<string>("""
            async () => {
                const token = document.querySelector('meta[name="vertexbpmn-antiforgery"]')?.content;
                if (!token) throw new Error('The OIDC antiforgery token is missing.');
                const calls = Array.from({ length: 6 }, async () => {
                    const response = await fetch('/authentication/session/refresh', {
                        method: 'POST',
                        headers: { 'RequestVerificationToken': token }
                    });
                    return { status: response.status, body: await response.text() };
                });
                return JSON.stringify(await Promise.all(calls));
            }
            """);
        using (var refreshes = JsonDocument.Parse(refreshJson))
        {
            Assert.Equal(6, refreshes.RootElement.GetArrayLength());
            Assert.All(refreshes.RootElement.EnumerateArray(), refresh =>
            {
                Assert.Equal(200, refresh.GetProperty("status").GetInt32());
                using var body = JsonDocument.Parse(refresh.GetProperty("body").GetString()!);
                Assert.True(body.RootElement.GetProperty("renewed").GetBoolean());
            });
        }

        await page.Locator("[data-testid='dashboard-refresh']").ClickAsync();
        await page.Locator("[data-testid='dashboard-refresh']:not([disabled])").WaitForAsync();
        Assert.DoesNotContain("Failed to load dashboard data", await page.Locator("body").InnerTextAsync());
    }

    [Fact]
    [Trait("Category", "KeycloakOidcSecurityAcceptance")]
    public async Task T05_ConcurrentRealBrowserSessions_KeepTenantClaimsIsolated()
    {
        var studioUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL");
        var password = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = global::Chromium.Path
        });
        await using var tenantAContext = await browser.NewContextAsync();
        await using var tenantBContext = await browser.NewContextAsync();
        var tenantAPage = await tenantAContext.NewPageAsync();
        var tenantBPage = await tenantBContext.NewPageAsync();
        await LoginAsync(tenantAPage, studioUrl, "vertexbpmn-user", password);
        await LoginAsync(tenantBPage, studioUrl, "vertexbpmn-manager-b", password);

        await AssertTenantContextAsync(tenantAPage, "tenant-a", "tenant-b");
        await AssertTenantContextAsync(tenantBPage, "tenant-b", "tenant-a");

        await Task.WhenAll(
            tenantAPage.GotoAsync(new Uri(new Uri(studioUrl), "process-definitions").ToString()),
            tenantBPage.GotoAsync(new Uri(new Uri(studioUrl), "process-definitions").ToString()));
        await Task.WhenAll(
            tenantAPage.GetByRole(AriaRole.Heading, new() { Name = "Process Definitions", Exact = true }).First.WaitForAsync(),
            tenantBPage.GetByRole(AriaRole.Heading, new() { Name = "Process Definitions", Exact = true }).First.WaitForAsync());

        await AssertTenantContextAsync(tenantAPage, "tenant-a", "tenant-b");
        await AssertTenantContextAsync(tenantBPage, "tenant-b", "tenant-a");
        Assert.DoesNotContain("Failed to load", await tenantAPage.Locator("body").InnerTextAsync());
        Assert.DoesNotContain("Failed to load", await tenantBPage.Locator("body").InnerTextAsync());
    }

    [Fact]
    [Trait("Category", "KeycloakOidcSecurityAcceptance")]
    public async Task T02_AdminActions_AreVisibleOnlyToAdminInSeparateRealBrowserSessions()
    {
        var studioUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL");
        var password = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = global::Chromium.Path
        });

        await using (var readOnlyContext = await browser.NewContextAsync())
        {
            var readOnlyPage = await readOnlyContext.NewPageAsync();
            await LoginAsync(readOnlyPage, studioUrl, "vertexbpmn-readonly", password);
            await readOnlyPage.GotoAsync(new Uri(new Uri(studioUrl), "tenants").ToString());
            await readOnlyPage.GetByRole(AriaRole.Heading, new() { Name = "Tenants", Exact = true }).First.WaitForAsync();
            Assert.Equal(0, await readOnlyPage.Locator("[data-testid='tenant-admin-create']").CountAsync());
            Assert.Equal(0, await readOnlyPage.Locator("[data-testid='tenant-admin-actions']").CountAsync());
        }

        await using (var adminContext = await browser.NewContextAsync())
        {
            var adminPage = await adminContext.NewPageAsync();
            await LoginAsync(adminPage, studioUrl, "vertexbpmn-admin", password);
            await adminPage.GotoAsync(new Uri(new Uri(studioUrl), "tenants").ToString());
            await adminPage.GetByRole(AriaRole.Heading, new() { Name = "Tenants", Exact = true }).First.WaitForAsync();
            await adminPage.Locator("[data-testid='tenant-admin-create']").WaitForAsync();
            Assert.Equal(1, await adminPage.Locator("[data-testid='tenant-admin-create']").CountAsync());
        }
    }

    [Fact]
    [Trait("Category", "KeycloakOidcSecurityAcceptance")]
    public async Task T02_Roles_Tenants_And_NonAccessTokens_AreEnforcedByTheRealApi()
    {
        var authority = RequiredEnvironment("Jwt__Authority").TrimEnd('/');
        var apiUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_API_URL");
        var clientId = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_SECURITY_CLIENT_ID");
        var clientSecret = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_SECURITY_CLIENT_SECRET");
        var password = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD");

        using var keycloak = new HttpClient();
        using var api = new HttpClient { BaseAddress = new Uri(apiUrl) };
        var admin = await PasswordTokensAsync(keycloak, authority, clientId, clientSecret, "vertexbpmn-admin", password);
        var managerA = await PasswordTokensAsync(keycloak, authority, clientId, clientSecret, "vertexbpmn-user", password);
        var managerB = await PasswordTokensAsync(keycloak, authority, clientId, clientSecret, "vertexbpmn-manager-b", password);
        var readOnly = await PasswordTokensAsync(keycloak, authority, clientId, clientSecret, "vertexbpmn-readonly", password);
        var noTenant = await PasswordTokensAsync(keycloak, authority, clientId, clientSecret, "vertexbpmn-no-tenant", password);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(api, HttpMethod.Get, "api/connector-templates", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(api, HttpMethod.Get, "api/connector-templates", noTenant.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(api, HttpMethod.Get, "api/connector-templates", admin.IdToken)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(api, HttpMethod.Get, "api/connector-templates?tenantId=tenant-a", managerA.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(api, HttpMethod.Get, "api/connector-templates?tenantId=tenant-b", managerB.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(api, HttpMethod.Get, "api/connector-templates?tenantId=tenant-a", readOnly.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(api, HttpMethod.Get, "api/connector-templates?tenantId=tenant-b", managerA.AccessToken)).StatusCode);

        using var invalidDeployment = JsonContent.Create(new { bpmnXml = "", name = "", tenantId = "tenant-a" });
        var managerMutation = await SendAsync(api, HttpMethod.Post, "api/repository", managerA.AccessToken, invalidDeployment);
        Assert.Equal(HttpStatusCode.BadRequest, managerMutation.StatusCode);
        using var readOnlyDeployment = JsonContent.Create(new { bpmnXml = "", name = "", tenantId = "tenant-a" });
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(api, HttpMethod.Post, "api/repository", readOnly.AccessToken, readOnlyDeployment)).StatusCode);

        var tenantName = $"K05 foreign tenant {Guid.NewGuid():N}";
        using var tenantPayload = JsonContent.Create(new { name = tenantName, description = "K05 T02 isolation proof" });
        var createTenant = await SendAsync(api, HttpMethod.Post, "api/tenant", admin.AccessToken, tenantPayload);
        Assert.Equal(HttpStatusCode.Created, createTenant.StatusCode);
        using var created = JsonDocument.Parse(await createTenant.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var tenantId = created.RootElement.GetProperty("id").GetString()!;
        try
        {
            using var forbiddenPayload = JsonContent.Create(new { name = "not-authorized" });
            Assert.Equal(HttpStatusCode.Forbidden,
                (await SendAsync(api, HttpMethod.Post, "api/tenant", managerA.AccessToken, forbiddenPayload)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await SendAsync(api, HttpMethod.Get, $"api/tenant/{Uri.EscapeDataString(tenantId)}", managerA.AccessToken)).StatusCode);

            var visibleTenants = await SendAsync(api, HttpMethod.Get, "api/tenant", managerA.AccessToken);
            Assert.Equal(HttpStatusCode.OK, visibleTenants.StatusCode);
            Assert.DoesNotContain(tenantId, await visibleTenants.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
        finally
        {
            var delete = await SendAsync(api, HttpMethod.Delete, $"api/tenant/{Uri.EscapeDataString(tenantId)}", admin.AccessToken);
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }
    }

    [Fact]
    [Trait("Category", "KeycloakOidcSecurityAcceptance")]
    public async Task T03_Tampered_Unsigned_Malformed_And_ExpiredTokens_AreRejectedByTheRealApi()
    {
        var authority = RequiredEnvironment("Jwt__Authority").TrimEnd('/');
        var apiUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_API_URL");
        var clientId = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_SECURITY_CLIENT_ID");
        var clientSecret = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_SECURITY_CLIENT_SECRET");
        var password = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD");

        using var keycloak = new HttpClient();
        using var api = new HttpClient { BaseAddress = new Uri(apiUrl) };
        var tokens = await PasswordTokensAsync(
            keycloak,
            authority,
            clientId,
            clientSecret,
            "vertexbpmn-admin",
            password);

        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(api, HttpMethod.Get, "api/tenant", tokens.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(api, HttpMethod.Get, "api/tenant", TamperSignature(tokens.AccessToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(api, HttpMethod.Get, "api/tenant", UnsignedToken(authority))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(api, HttpMethod.Get, "api/tenant", "not-a-jwt")).StatusCode);

        var expiringTokens = await PasswordTokensAsync(
            keycloak,
            authority,
            RequiredEnvironment("VERTEXBPMN_KEYCLOAK_EXPIRING_CLIENT_ID"),
            clientSecret,
            "vertexbpmn-admin",
            password);
        var expiresAt = ReadExpiration(expiringTokens.AccessToken);
        var remaining = expiresAt - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
        Assert.InRange(remaining, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        await Task.Delay(remaining, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(api, HttpMethod.Get, "api/tenant", expiringTokens.AccessToken)).StatusCode);
    }

    [Fact]
    [Trait("Category", "KeycloakOidcSecurityAcceptance")]
    public async Task T03_KeycloakSignedTokens_WithWrongAudienceOrIssuer_AreRejectedByTheRealApi()
    {
        var authority = RequiredEnvironment("Jwt__Authority").TrimEnd('/');
        var apiUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_API_URL");
        var clientSecret = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_SECURITY_CLIENT_SECRET");
        var password = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD");
        var wrongAudienceClient = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_WRONG_AUDIENCE_CLIENT_ID");

        using var keycloak = new HttpClient();
        using var api = new HttpClient { BaseAddress = new Uri(apiUrl) };
        var wrongAudienceToken = await PasswordAccessTokenAsync(
            keycloak,
            authority,
            wrongAudienceClient,
            clientSecret,
            "vertexbpmn-admin",
            password);
        using (var payload = ReadPayload(wrongAudienceToken))
        {
            Assert.Equal(authority, payload.RootElement.GetProperty("iss").GetString());
            Assert.DoesNotContain("vertexbpmn-api", ReadAudiences(payload.RootElement));
        }

        var masterAuthority = new Uri(new Uri(authority + "/"), "../master").ToString().TrimEnd('/');
        var wrongIssuerToken = await PasswordAccessTokenAsync(
            keycloak,
            masterAuthority,
            "admin-cli",
            null,
            "vertexbpmn-admin",
            RequiredEnvironment("VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD"));
        using (var payload = ReadPayload(wrongIssuerToken))
        {
            Assert.Equal(masterAuthority, payload.RootElement.GetProperty("iss").GetString());
            Assert.NotEqual(authority, payload.RootElement.GetProperty("iss").GetString());
        }

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(api, HttpMethod.Get, "api/tenant", wrongAudienceToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(api, HttpMethod.Get, "api/tenant", wrongIssuerToken)).StatusCode);
    }

    private static async Task<OidcTokens> PasswordTokensAsync(
        HttpClient client,
        string authority,
        string clientId,
        string clientSecret,
        string username,
        string password)
    {
        using var response = await PostTokenAsync(
            client,
            $"{authority}/protocol/openid-connect/token",
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["username"] = username,
                ["password"] = password,
                ["scope"] = "openid"
            });
        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            using var errorDocument = JsonDocument.Parse(payload);
            var error = errorDocument.RootElement.TryGetProperty("error", out var errorValue)
                ? errorValue.GetString()
                : "unknown_error";
            var description = errorDocument.RootElement.TryGetProperty("error_description", out var descriptionValue)
                ? descriptionValue.GetString()
                : "No description returned.";
            Assert.Fail(
                $"Keycloak rejected the local security-test token request for '{username}' with HTTP {(int)response.StatusCode}: {error} ({description}).");
        }
        using var document = JsonDocument.Parse(payload);
        return new OidcTokens(
            document.RootElement.GetProperty("access_token").GetString()!,
            document.RootElement.GetProperty("id_token").GetString()!);
    }

    private static async Task<HttpClient> CreateKeycloakAdminClientAsync(HttpClient tokenClient, string authority)
    {
        var masterAuthority = new Uri(new Uri(authority + "/"), "../master").ToString().TrimEnd('/');
        var adminToken = await PasswordAccessTokenAsync(
            tokenClient,
            masterAuthority,
            "admin-cli",
            null,
            "vertexbpmn-admin",
            RequiredEnvironment("VERTEXBPMN_KEYCLOAK_ADMIN_PASSWORD"));
        var origin = new Uri(authority).GetLeftPart(UriPartial.Authority);
        var client = new HttpClient { BaseAddress = new Uri($"{origin}/admin/realms/vertexbpmn/") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        return client;
    }

    private static async Task<KeycloakRole> GetClientRoleAsync(
        HttpClient admin,
        string username,
        string roleName)
    {
        var userId = await GetKeycloakEntityIdAsync(admin, $"users?username={username}&exact=true");
        var clientId = await GetKeycloakEntityIdAsync(admin, "clients?clientId=vertexbpmn-api");
        using var response = await admin.GetAsync(
            $"clients/{Uri.EscapeDataString(clientId)}/roles/{Uri.EscapeDataString(roleName)}",
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var role = await response.Content.ReadFromJsonAsync<KeycloakRole>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException($"Keycloak returned no representation for role '{roleName}'.");
        return role with { UserId = userId, ClientId = clientId };
    }

    private static async Task<string> GetKeycloakEntityIdAsync(HttpClient admin, string path)
    {
        using var response = await admin.GetAsync(path, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(JsonValueKind.Array, payload.RootElement.ValueKind);
        Assert.Equal(1, payload.RootElement.GetArrayLength());
        return payload.RootElement[0].GetProperty("id").GetString()
            ?? throw new InvalidOperationException($"Keycloak entity '{path}' has no id.");
    }

    private static async Task SetClientRoleAsync(HttpClient admin, KeycloakRole role, bool assign)
    {
        var path = $"users/{Uri.EscapeDataString(role.UserId)}/role-mappings/clients/{Uri.EscapeDataString(role.ClientId)}";
        using var content = JsonContent.Create(new[] { new { role.Id, role.Name } });
        using var request = new HttpRequestMessage(assign ? HttpMethod.Post : HttpMethod.Delete, path)
        {
            Content = content
        };
        using var response = await admin.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task SetUserEnabledAsync(HttpClient admin, string userId, bool enabled)
    {
        using var response = await admin.PutAsJsonAsync(
            $"users/{Uri.EscapeDataString(userId)}",
            new { enabled },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonDocument> RefreshBrowserSessionAsync(IPage page, bool manualRedirect = false)
    {
        var refreshJson = await page.EvaluateAsync<string>(
            """
            async manualRedirect => {
                const token = document.querySelector('meta[name="vertexbpmn-antiforgery"]')?.content;
                if (!token) throw new Error('The OIDC antiforgery token is missing.');
                const response = await fetch('/authentication/session/refresh', {
                    method: 'POST',
                    headers: { 'RequestVerificationToken': token },
                    redirect: manualRedirect ? 'manual' : 'follow'
                });
                return JSON.stringify({
                    status: response.status,
                    type: response.type,
                    url: response.url,
                    body: response.type === 'opaqueredirect' ? '' : await response.text()
                });
            }
            """,
            manualRedirect);
        return JsonDocument.Parse(refreshJson);
    }

    private static async Task WaitForRefreshWindowAsync(int lifespan)
    {
        var seconds = Math.Max(1, lifespan - 58);
        await Task.Delay(TimeSpan.FromSeconds(seconds), TestContext.Current.CancellationToken);
    }

    private static int RequiredTokenLifespan() => int.Parse(
        RequiredEnvironment("VERTEXBPMN_KEYCLOAK_ACCESS_TOKEN_LIFESPAN"),
        System.Globalization.CultureInfo.InvariantCulture);

    private static async Task AssertNoStudioSessionAsync(IBrowserContext context, string studioUrl)
    {
        var cookies = await context.CookiesAsync([studioUrl]);
        Assert.DoesNotContain(cookies, cookie => cookie.Name == ".AspNetCore.Cookies");
    }

    private static async Task ClickWithoutImplicitNavigationWaitAsync(ILocator locator)
    {
        await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await locator.EvaluateAsync("element => element.click()");
    }

    private static async Task WaitForUnusedTotpWindowAsync(string secret, string previouslyUsedCode)
    {
        while (string.Equals(
            Totp(secret, DateTimeOffset.UtcNow),
            previouslyUsedCode,
            StringComparison.Ordinal))
        {
            await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        }
    }

    private static string NormalizeTotpSecret(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();

    private static string DifferentTotp(string validCode) =>
        string.Equals(validCode, "000000", StringComparison.Ordinal) ? "111111" : "000000";

    private static string Totp(string base32Secret, DateTimeOffset timestamp)
    {
        var key = DecodeBase32(base32Secret);
        var counter = timestamp.ToUnixTimeSeconds() / 30;
        Span<byte> counterBytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);
        var hash = HMACSHA1.HashData(key, counterBytes);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var character in value.TrimEnd('='))
        {
            var index = alphabet.IndexOf(character);
            if (index < 0)
                throw new FormatException("Keycloak returned an invalid Base32 TOTP secret.");
            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits < 8)
                continue;
            bits -= 8;
            bytes.Add((byte)(buffer >> bits));
            buffer &= (1 << bits) - 1;
        }
        return bytes.ToArray();
    }

    private static async Task<string> PasswordAccessTokenAsync(
        HttpClient client,
        string authority,
        string clientId,
        string? clientSecret,
        string username,
        string password)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = username,
            ["password"] = password,
            ["scope"] = "openid"
        };
        if (!string.IsNullOrWhiteSpace(clientSecret))
            form["client_secret"] = clientSecret;

        using var response = await PostTokenAsync(
            client,
            $"{authority}/protocol/openid-connect/token",
            form);
        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode,
            $"Keycloak rejected a local negative-token fixture with HTTP {(int)response.StatusCode}.");
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("access_token").GetString()!;
    }

    private static async Task<HttpResponseMessage> PostTokenAsync(
        HttpClient client,
        string endpoint,
        IReadOnlyDictionary<string, string> form)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await client.PostAsync(
                    endpoint,
                    new FormUrlEncodedContent(form),
                    TestContext.Current.CancellationToken);
            }
            catch (HttpRequestException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), TestContext.Current.CancellationToken);
            }
        }
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string? bearerToken,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task LoginAsync(IPage page, string studioUrl, string username, string password)
    {
        await GotoStudioAsync(page, studioUrl);
        await page.Locator("#username").FillAsync(username);
        await page.Locator("#password").FillAsync(password);
        await ClickWithoutImplicitNavigationWaitAsync(page.Locator("#kc-login"));
        await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
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

    private static async Task AssertTenantContextAsync(IPage page, string expected, string forbidden)
    {
        var tenantContext = page.Locator("[data-testid='tenant-context']:visible");
        await tenantContext.WaitForAsync();
        var text = await tenantContext.InnerTextAsync();
        Assert.Contains(expected, text, StringComparison.Ordinal);
        Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
    }

    private static string TamperSignature(string token)
    {
        var segments = token.Split('.');
        Assert.Equal(3, segments.Length);
        var final = segments[2][^1] == 'A' ? 'B' : 'A';
        return $"{segments[0]}.{segments[1]}.{segments[2][..^1]}{final}";
    }

    private static string UnsignedToken(string issuer)
    {
        var header = Base64Url("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64Url(JsonSerializer.Serialize(new
        {
            iss = issuer,
            aud = "vertexbpmn-api",
            sub = "unsigned-user",
            preferred_username = "unsigned-user",
            tenant_id = "tenant-a",
            roles = new[] { "Admin" },
            exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
        }));
        return $"{header}.{payload}.";
    }

    private static DateTimeOffset ReadExpiration(string token)
    {
        using var payload = ReadPayload(token);
        return DateTimeOffset.FromUnixTimeSeconds(payload.RootElement.GetProperty("exp").GetInt64());
    }

    private static JsonDocument ReadPayload(string token)
    {
        var segments = token.Split('.');
        Assert.Equal(3, segments.Length);
        return JsonDocument.Parse(DecodeBase64Url(segments[1]));
    }

    private static IReadOnlyList<string> ReadAudiences(JsonElement payload)
    {
        if (!payload.TryGetProperty("aud", out var audience))
            return [];
        return audience.ValueKind == JsonValueKind.Array
            ? audience.EnumerateArray().Select(value => value.GetString()!).ToArray()
            : [audience.GetString()!];
    }

    private static string Base64Url(string value) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{name} is required. Run this local-only test through scripts/keycloak-oidc-e2e.ps1 -SecurityAcceptance.");

    private sealed record OidcTokens(string AccessToken, string IdToken);
    private sealed record KeycloakRole(string Id, string Name, string UserId = "", string ClientId = "");
}
