using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

public sealed partial class KeycloakOidcSecurityAcceptanceTests
{
	[Fact]
	[Trait("Category", "KeycloakOidcSecurityAcceptance")]
	[Trait("Category", "KeycloakPasskeyAcceptance")]
	public async Task T13_Registration_AndPasswordlessLogin_PreserveTenantAndReadOnlyApiAccess()
	{
		Assert.Equal("1", RequiredEnvironment("VERTEXBPMN_KEYCLOAK_PASSKEY_ACCEPTANCE"));
		var studioUrl = RequiredEnvironment("VERTEXBPMN_OIDC_TEST_STUDIO_URL");
		var password = RequiredEnvironment("VERTEXBPMN_KEYCLOAK_TEST_USER_PASSWORD");
		const string username = "vertexbpmn-passkey-user";
		using var playwright = await Playwright.CreateAsync();
		await using var browser = await playwright.Chromium.LaunchAsync(new()
		{
			Headless = true,
			ExecutablePath = global::Chromium.Path
		});
		await using var context = await browser.NewContextAsync();
		var page = await context.NewPageAsync();
		var cdp = await context.NewCDPSessionAsync(page);
		var authenticatorId = await AddPasskeyAuthenticatorAsync(cdp);
		try
		{
			await RegisterPasskeyAsync(page, context, studioUrl, username, password);
			await AssertPasskeyCredentialAsync(username);
			var initialSignCount = await GetVirtualPasskeySignCountAsync(cdp, authenticatorId);
			await AssertAccountManagementLinkAsync(page, studioUrl);
			await cdp.SendAsync("WebAuthn.setAutomaticPresenceSimulation", new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["authenticatorId"] = authenticatorId,
				["enabled"] = false
			});
			await page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true }).ClickAsync();
			await page.Locator("#username").WaitForAsync();
			await AssertNoStudioSessionAsync(context, studioUrl);
			Assert.Equal(string.Empty, await page.Locator("#password").InputValueAsync());
			var passkeyLogin = page.Locator("#authenticateWebAuthnButton");
			await passkeyLogin.WaitForAsync();
			TestContext.Current.TestOutputHelper!.WriteLine("Keycloak passkey login control: #authenticateWebAuthnButton ({0})",
				await passkeyLogin.InnerTextAsync());
			await cdp.SendAsync("WebAuthn.setAutomaticPresenceSimulation", new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["authenticatorId"] = authenticatorId,
				["enabled"] = true
			});
			await ClickWithoutImplicitNavigationWaitAsync(passkeyLogin);
			await AssertPasskeyDashboardAsync(page);
			Assert.True(await GetVirtualPasskeySignCountAsync(cdp, authenticatorId) > initialSignCount);
			await AssertTenantContextAsync(page, "tenant-a", "tenant-b");
			await page.GotoAsync(new Uri(new Uri(studioUrl), "process-definitions").ToString());
			await page.GetByTestId("process-definitions-grid").WaitForAsync();
			Assert.DoesNotContain("Process definitions could not be loaded", await page.Locator("body").InnerTextAsync(), StringComparison.Ordinal);
			await AssertTenantContextAsync(page, "tenant-a", "tenant-b");
		}
		finally
		{
			await cdp.SendAsync("WebAuthn.removeVirtualAuthenticator", new Dictionary<string, object>(StringComparer.Ordinal) { ["authenticatorId"] = authenticatorId });
			await cdp.DetachAsync();
		}
	}

	private static async Task<string> AddPasskeyAuthenticatorAsync(ICDPSession cdp)
	{
		await cdp.SendAsync("WebAuthn.enable");
		var authenticator = await cdp.SendAsync("WebAuthn.addVirtualAuthenticator", new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["options"] = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["protocol"] = "ctap2",
				["transport"] = "internal",
				["hasResidentKey"] = true,
				["hasUserVerification"] = true,
				["isUserVerified"] = true,
				["automaticPresenceSimulation"] = true
			}
		});
		Assert.NotNull(authenticator);
		return authenticator.Value.GetProperty("authenticatorId").GetString()
			?? throw new InvalidOperationException("The virtual authenticator has no id.");
	}

	private static async Task<int> GetVirtualPasskeySignCountAsync(ICDPSession cdp, string authenticatorId)
	{
		var result = await cdp.SendAsync("WebAuthn.getCredentials", new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["authenticatorId"] = authenticatorId
		});
		Assert.NotNull(result);
		var credential = Assert.Single(result.Value.GetProperty("credentials").EnumerateArray());
		Assert.True(credential.GetProperty("isResidentCredential").GetBoolean());
		Assert.Equal("localhost", credential.GetProperty("rpId").GetString());
		return credential.GetProperty("signCount").GetInt32();
	}

	private static async Task AssertPasskeyDashboardAsync(IPage page)
	{
		try
		{
			await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
		}
		catch (TimeoutException)
		{
			// Inspect only visible control text; never emit callback query codes or credential data.
			TestContext.Current.TestOutputHelper!.WriteLine("Passkey page path: {0}; headings: {1}; errors: {2}",
				new Uri(page.Url).AbsolutePath,
				string.Join(" | ", await page.GetByRole(AriaRole.Heading).AllTextContentsAsync()),
				string.Join(" | ", await page.Locator("#input-error, #kc-feedback-text").AllTextContentsAsync()));
			throw;
		}
	}

	private static async Task RegisterPasskeyAsync(IPage page, IBrowserContext context, string studioUrl, string username, string password)
	{
		await GotoStudioAsync(page, studioUrl);
		await page.Locator("#username").FillAsync(username);
		await page.Locator("#password").FillAsync(password);
		await ClickWithoutImplicitNavigationWaitAsync(page.Locator("#kc-login"));
		await page.Locator("#registerWebAuthn").WaitForAsync();
		TestContext.Current.TestOutputHelper!.WriteLine("Keycloak registration control: #registerWebAuthn ({0})",
			await page.Locator("#registerWebAuthn").GetAttributeAsync("value")
			?? await page.Locator("#registerWebAuthn").InnerTextAsync());
		await AssertNoStudioSessionAsync(context, studioUrl);
		page.Dialog += async (_, dialog) => await dialog.AcceptAsync("VertexBPMN local passkey acceptance");
		await ClickWithoutImplicitNavigationWaitAsync(page.Locator("#registerWebAuthn"));
		await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
	}

	private static async Task AssertAccountManagementLinkAsync(IPage page, string studioUrl)
	{
		await page.GotoAsync(new Uri(new Uri(studioUrl), "sso").ToString());
		var accountLink = page.GetByTestId("sso-manage-sign-in");
		await accountLink.WaitForAsync();
		Assert.Equal("Manage sign-in methods", await accountLink.InnerTextAsync());
		Assert.Equal("_blank", await accountLink.GetAttributeAsync("target"));
		Assert.Equal("noopener noreferrer", await accountLink.GetAttributeAsync("rel"));
		Assert.Equal(RequiredEnvironment("StudioAuthentication__AccountManagementUrl"), await accountLink.GetAttributeAsync("href"));
	}

	private static async Task AssertPasskeyCredentialAsync(string username)
	{
		using var tokenClient = new HttpClient();
		using var admin = await CreateKeycloakAdminClientAsync(tokenClient, RequiredEnvironment("Jwt__Authority"));
		using var usersResponse = await admin.GetAsync($"users?username={Uri.EscapeDataString(username)}&exact=true", TestContext.Current.CancellationToken);
		usersResponse.EnsureSuccessStatusCode();
		using var users = JsonDocument.Parse(await usersResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
		var user = Assert.Single(users.RootElement.EnumerateArray());
		using var credentialsResponse = await admin.GetAsync($"users/{user.GetProperty("id").GetString()}/credentials", TestContext.Current.CancellationToken);
		credentialsResponse.EnsureSuccessStatusCode();
		using var credentials = JsonDocument.Parse(await credentialsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
		Assert.Single(credentials.RootElement.EnumerateArray(), credential => credential.GetProperty("type").GetString() == "webauthn-passwordless");
	}
}
