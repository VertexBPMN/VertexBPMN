using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

public sealed partial class KeycloakOidcSecurityAcceptanceTests
{
	[Fact]
	[Trait("Category", "KeycloakOidcSecurityAcceptance")]
	[Trait("Category", "KeycloakPasskeyAcceptance")]
	public async Task T14_InitiatedAction_UsesPar_RegistersOrCancels_AndRejectsExternalReturnTargets()
	{
		Assert.Equal("1", RequiredEnvironment("VERTEXBPMN_KEYCLOAK_PASSKEY_ACCEPTANCE"));
		var studioUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL");
		const string username = "vertexbpmn-passkey-aia-user";
		using var playwright = await Playwright.CreateAsync();
		await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, ExecutablePath = global::Chromium.Path });
		await using var context = await browser.NewContextAsync();
		var page = await context.NewPageAsync();
		await LoginAsync(page, studioUrl, username, RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD"));
		var cdp = await context.NewCDPSessionAsync(page);
		var authenticatorId = await AddPasskeyAuthenticatorAsync(cdp);
		var pushedRequests = 0;
		page.Request += (_, request) =>
		{
			var address = new Uri(request.Url);
			if (address.AbsolutePath.EndsWith("/protocol/openid-connect/auth", StringComparison.Ordinal)
				&& QueryHelpers.ParseQuery(address.Query).ContainsKey("request_uri"))
			{
				Interlocked.Increment(ref pushedRequests);
			}
		};
		try
		{
			await AssertCredentialCountAsync(username, 0);
			await StartPasskeyActionAsync(page, studioUrl);
			await page.Locator("#cancelWebAuthnAIA").WaitForAsync();
			TestContext.Current.TestOutputHelper!.WriteLine("AIA cancellation control: {0}", await page.Locator("#cancelWebAuthnAIA").InnerTextAsync());
			await ClickWithoutImplicitNavigationWaitAsync(page.Locator("#cancelWebAuthnAIA"));
			await AssertActionReturnedToStudioAsync(page, context, studioUrl);
			await AssertCredentialCountAsync(username, 0);
			Assert.Equal(1, Volatile.Read(ref pushedRequests));
			await StartPasskeyActionAsync(page, studioUrl);
			page.Dialog += async (_, dialog) => await dialog.AcceptAsync("VertexBPMN initiated passkey action");
			await ClickWithoutImplicitNavigationWaitAsync(page.Locator("#registerWebAuthn"));
			await AssertActionReturnedToStudioAsync(page, context, studioUrl);
			await AssertPasskeyCredentialAsync(username);
			Assert.Equal(2, Volatile.Read(ref pushedRequests));
			await page.GotoAsync(new Uri(new Uri(studioUrl), "process-definitions").ToString());
			await page.GetByTestId("process-definitions-grid").WaitForAsync();
		}
		finally
		{
			await cdp.SendAsync("WebAuthn.removeVirtualAuthenticator", new Dictionary<string, object>(StringComparer.Ordinal) { ["authenticatorId"] = authenticatorId });
			await cdp.DetachAsync();
		}
	}

	[Fact]
	[Trait("Category", "KeycloakOidcSecurityAcceptance")]
	[Trait("Category", "KeycloakPasskeyAcceptance")]
	public async Task T15_PasskeyLogin_WithoutTenant_DoesNotCreateAStudioSession()
	{
		Assert.Equal("1", RequiredEnvironment("VERTEXBPMN_KEYCLOAK_PASSKEY_ACCEPTANCE"));
		var studioUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL");
		const string username = "vertexbpmn-passkey-no-tenant";
		using var playwright = await Playwright.CreateAsync();
		await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, ExecutablePath = global::Chromium.Path });
		await using var context = await browser.NewContextAsync();
		var page = await context.NewPageAsync();
		var cdp = await context.NewCDPSessionAsync(page);
		var authenticatorId = await AddPasskeyAuthenticatorAsync(cdp);
		try
		{
			await RegisterPasskeyAsync(page, context, studioUrl, username, RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD"));
			await AssertPasskeyCredentialAsync(username);
			var initialSignCount = await GetVirtualPasskeySignCountAsync(cdp, authenticatorId);
			await cdp.SendAsync("WebAuthn.setAutomaticPresenceSimulation", new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["authenticatorId"] = authenticatorId,
				["enabled"] = false
			});
			await page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true }).ClickAsync();
			await page.Locator("#username").WaitForAsync();
			await AssertNoStudioSessionAsync(context, studioUrl);
			// Remove the tenant before code redemption, so Keycloak issues genuinely signed
			// tenant-free tokens without disabling its own mandatory-profile checks.
			await page.RouteAsync(new Uri(new Uri(studioUrl), "signin-oidc") + "**", async route =>
			{
				await SetPasskeyTenantAsync(username, []);
				await route.ContinueAsync();
			});
			await cdp.SendAsync("WebAuthn.setAutomaticPresenceSimulation", new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["authenticatorId"] = authenticatorId,
				["enabled"] = true
			});
			await page.Locator("#authenticateWebAuthnButton").ClickAsync();
			await page.WaitForURLAsync(address => new Uri(address).GetLeftPart(UriPartial.Authority) == new Uri(studioUrl).GetLeftPart(UriPartial.Authority));
			Assert.True(await GetVirtualPasskeySignCountAsync(cdp, authenticatorId) > initialSignCount);
			await AssertNoStudioSessionAsync(context, studioUrl);
			Assert.Equal(0, await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).CountAsync());
			var refreshStatus = await context.APIRequest.PostAsync(new Uri(new Uri(studioUrl), "authentication/session/refresh").ToString());
			Assert.Equal(401, refreshStatus.Status);
			await refreshStatus.DisposeAsync();
		}
		finally
		{
			await SetPasskeyTenantAsync(username, ["tenant-a"]);
			await cdp.SendAsync("WebAuthn.removeVirtualAuthenticator", new Dictionary<string, object>(StringComparer.Ordinal) { ["authenticatorId"] = authenticatorId });
			await cdp.DetachAsync();
		}
	}

	private static async Task StartPasskeyActionAsync(IPage page, string studioUrl)
	{
		await page.GotoAsync(new Uri(new Uri(studioUrl), "sso").ToString());
		await page.GetByTestId("sso-add-passkey").WaitForAsync();
		var endpoint = new Uri(new Uri(studioUrl), "authentication/passkey/register?returnUrl=" + Uri.EscapeDataString("//attacker.example/"));
		await page.GotoAsync(endpoint.ToString());
		await page.Locator("#registerWebAuthn").WaitForAsync();
	}

	private static async Task AssertActionReturnedToStudioAsync(IPage page, IBrowserContext context, string studioUrl)
	{
		await page.GetByRole(AriaRole.Heading, new() { Name = "Single Sign-On (SSO)", Exact = true }).WaitForAsync();
		Assert.Equal(new Uri(new Uri(studioUrl), "sso").ToString(), page.Url);
		Assert.Contains(await context.CookiesAsync([studioUrl]), cookie => cookie.Name == ".AspNetCore.Cookies");
		await AssertTenantContextAsync(page, "tenant-a", "tenant-b");
	}

	private static async Task AssertCredentialCountAsync(string username, int expected)
	{
		using var tokenClient = new HttpClient();
		using var admin = await CreateKeycloakAdminClientAsync(tokenClient, RequiredEnvironment("Jwt__Authority"));
		var userId = await GetKeycloakEntityIdAsync(admin, $"users?username={Uri.EscapeDataString(username)}&exact=true");
		using var response = await admin.GetAsync($"users/{userId}/credentials", TestContext.Current.CancellationToken);
		response.EnsureSuccessStatusCode();
		using var credentials = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
		Assert.Equal(expected, credentials.RootElement.EnumerateArray().Count(item => item.GetProperty("type").GetString() == "webauthn-passwordless"));
	}

	private static async Task SetPasskeyTenantAsync(string username, string[] tenants)
	{
		using var tokenClient = new HttpClient();
		using var admin = await CreateKeycloakAdminClientAsync(tokenClient, RequiredEnvironment("Jwt__Authority"));
		var userId = await GetKeycloakEntityIdAsync(admin, $"users?username={Uri.EscapeDataString(username)}&exact=true");
		using var response = await admin.PutAsJsonAsync($"users/{userId}", new { attributes = new Dictionary<string, string[]>(StringComparer.Ordinal) { ["tenant_id"] = tenants } }, TestContext.Current.CancellationToken);
		response.EnsureSuccessStatusCode();
	}
}
