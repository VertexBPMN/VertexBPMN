using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    private static async Task<OidcTokens> PasswordTokensAsync(
        HttpClient client,
        string authority,
        string clientId,
        string clientSecret,
        string username,
        string password)
    {
        using var response = await client.PostAsync(
            $"{authority}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["username"] = username,
                ["password"] = password,
                ["scope"] = "openid"
            }),
            TestContext.Current.CancellationToken);
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

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{name} is required. Run this local-only test through scripts/keycloak-oidc-e2e.ps1 -SecurityAcceptance.");

    private sealed record OidcTokens(string AccessToken, string IdToken);
}
