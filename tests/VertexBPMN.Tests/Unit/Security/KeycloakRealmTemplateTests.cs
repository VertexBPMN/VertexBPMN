using System.Text.Json;

namespace VertexBPMN.Tests.Unit.Security;

public sealed class KeycloakRealmTemplateTests
{
    private static readonly string RealmTemplatePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "deploy", "keycloak", "vertexbpmn-realm.json"));
    private static readonly string UserProfileTemplatePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "deploy", "keycloak", "vertexbpmn-user-profile.json"));

    [Fact]
    public void RealmTemplate_IsSecretFreeAndContainsNoUsers()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RealmTemplatePath));
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("users", out _));
        Assert.DoesNotContain(EnumeratePropertyNames(root), name =>
            name.Equals("secret", StringComparison.OrdinalIgnoreCase)
            || name.Equals("password", StringComparison.OrdinalIgnoreCase)
            || name.Equals("credentials", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RealmTemplate_DefinesStrictStudioAuthorizationCodeClient()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RealmTemplatePath));
        var studio = FindByName(document.RootElement.GetProperty("clients"), "clientId", "vertexbpmn-studio");

        Assert.False(studio.GetProperty("publicClient").GetBoolean());
        Assert.True(studio.GetProperty("standardFlowEnabled").GetBoolean());
        Assert.False(studio.GetProperty("implicitFlowEnabled").GetBoolean());
        Assert.False(studio.GetProperty("directAccessGrantsEnabled").GetBoolean());
        Assert.False(studio.GetProperty("serviceAccountsEnabled").GetBoolean());
        Assert.Equal(
            "S256",
            studio.GetProperty("attributes").GetProperty("pkce.code.challenge.method").GetString());
        Assert.Equal(
            "http://localhost:5263/*",
            studio.GetProperty("attributes").GetProperty("post.logout.redirect.uris").GetString());
        Assert.Equal(
            [
                "http://localhost:5263/signin-oidc",
                "https://localhost:5263/signin-oidc",
                "http://localhost:5264/signin-oidc"
            ],
            studio.GetProperty("redirectUris").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void RealmTemplate_MapsRequiredAudienceRolesAndTenantClaims()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RealmTemplatePath));
        var root = document.RootElement;
        var scope = FindByName(root.GetProperty("clientScopes"), "name", "vertexbpmn-claims");
        var mappers = scope.GetProperty("protocolMappers");

        var subject = FindByName(mappers, "protocolMapper", "oidc-sub-mapper");
        Assert.Equal("true", subject.GetProperty("config").GetProperty("lightweight.claim").GetString());

        var audience = FindByName(mappers, "protocolMapper", "oidc-audience-mapper");
        Assert.Equal(
            "vertexbpmn-api",
            audience.GetProperty("config").GetProperty("included.client.audience").GetString());

        var roles = FindByName(mappers, "protocolMapper", "oidc-usermodel-client-role-mapper");
        Assert.Equal("roles", roles.GetProperty("config").GetProperty("claim.name").GetString());
        Assert.Equal(
            "vertexbpmn-api",
            roles.GetProperty("config").GetProperty("usermodel.clientRoleMapping.clientId").GetString());

        var tenant = FindByName(mappers, "protocolMapper", "oidc-usermodel-attribute-mapper");
        Assert.Equal("tenant_id", tenant.GetProperty("config").GetProperty("user.attribute").GetString());
        Assert.Equal("tenant_id", tenant.GetProperty("config").GetProperty("claim.name").GetString());

        var roleNames = root.GetProperty("roles")
            .GetProperty("client")
            .GetProperty("vertexbpmn-api")
            .EnumerateArray()
            .Select(role => role.GetProperty("name").GetString())
            .ToArray();
        Assert.Equal(["Admin", "ProcessManager", "ReadOnly"], roleNames);
    }

    [Fact]
    public void UserProfile_ManagesTenantAsAdminOnlySingleValue()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(UserProfileTemplatePath));
        var root = document.RootElement;
        var tenant = FindByName(root.GetProperty("attributes"), "name", "tenant_id");
        using var realmDocument = JsonDocument.Parse(File.ReadAllText(RealmTemplatePath));
        var embeddedProfile = realmDocument.RootElement
            .GetProperty("components")
            .GetProperty("org.keycloak.userprofile.UserProfileProvider")[0]
            .GetProperty("config")
            .GetProperty("kc.user.profile.config")[0]
            .GetString();

        Assert.False(root.TryGetProperty("unmanagedAttributePolicy", out _));
        Assert.Equal(JsonSerializer.Serialize(root), JsonSerializer.Serialize(JsonDocument.Parse(embeddedProfile!).RootElement));
        Assert.False(tenant.GetProperty("multivalued").GetBoolean());
        Assert.Equal(
            ["admin", "user"],
            tenant.GetProperty("permissions").GetProperty("view").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            ["admin"],
            tenant.GetProperty("permissions").GetProperty("edit").EnumerateArray().Select(item => item.GetString()));
    }

    private static JsonElement FindByName(JsonElement array, string propertyName, string expectedValue) =>
        array.EnumerateArray().Single(item =>
            string.Equals(item.GetProperty(propertyName).GetString(), expectedValue, StringComparison.Ordinal));

    private static IEnumerable<string> EnumeratePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return property.Name;
                foreach (var nestedName in EnumeratePropertyNames(property.Value))
                    yield return nestedName;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nestedName in EnumeratePropertyNames(item))
                    yield return nestedName;
            }
        }
    }
}
