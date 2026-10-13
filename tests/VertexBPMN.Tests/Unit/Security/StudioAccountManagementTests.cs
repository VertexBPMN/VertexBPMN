using VertexBPMN.Studio;

namespace VertexBPMN.Tests.Unit.Security;

public sealed class StudioAccountManagementTests
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	public void Missing_configuration_omits_the_account_link(string? configuredValue) =>
		Assert.Null(new StudioAccountManagement(configuredValue, "Production").Url);

	[Theory]
	[InlineData("Production", "https://identity.example.org/realms/vertexbpmn/account/")]
	[InlineData("Development", "https://identity.example.org/account/")]
	[InlineData("OidcTest", "http://localhost:58080/realms/vertexbpmn/account/")]
	[InlineData("OidcTest", "http://127.0.0.1/account/")]
	[InlineData("OidcTest", "http://[::1]/account/")]
	public void Configured_safe_url_is_available(string environment, string configuredValue) =>
		Assert.Equal(configuredValue, new StudioAccountManagement(configuredValue, environment).Url);

	[Theory]
	[InlineData("Production", "http://localhost/account/")]
	[InlineData("Development", "http://localhost/account/")]
	[InlineData("UiTest", "http://localhost/account/")]
	[InlineData("OidcTest", "http://identity.example.org/account/")]
	[InlineData("Production", "//attacker.example/account/")]
	[InlineData("Production", "/account/")]
	[InlineData("Production", "javascript:alert(1)")]
	[InlineData("Production", "file:///account/")]
	[InlineData("Production", "https://user:password@identity.example.org/account/")]
	public void Invalid_configuration_fails_without_echoing_the_value(string environment, string configuredValue)
	{
		var error = Assert.Throws<InvalidOperationException>(() => new StudioAccountManagement(configuredValue, environment));
		Assert.Contains("StudioAuthentication:AccountManagementUrl", error.Message, StringComparison.Ordinal);
		Assert.DoesNotContain(configuredValue, error.Message, StringComparison.Ordinal);
	}
}
