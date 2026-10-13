using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace VertexBPMN.Studio;

internal sealed class StudioPasskeyRegistration(bool configured, bool oidcActive)
{
	internal const string ActionName = "webauthn-register-passwordless";
	internal const string ActionItemKey = "vertex:keycloak-action";
	public bool IsEnabled { get; } = configured && oidcActive;

	public void MapEndpoints(WebApplication app)
	{
		if (!IsEnabled)
		{
			return;
		}

		app.MapGet("/authentication/passkey/register", (string? returnUrl) =>
		{
			var properties = new AuthenticationProperties
			{
				RedirectUri = StudioAuthenticationRedirects.LocalTarget(returnUrl, "/sso")
			};
			properties.Items[ActionItemKey] = ActionName;
			return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
		}).RequireAuthorization();
	}

	public static void ApplyAction(AuthenticationProperties properties, OpenIdConnectMessage message)
	{
		if (properties.Items.TryGetValue(ActionItemKey, out var action) && action == ActionName)
		{
			message.SetParameter("kc_action", ActionName);
		}
	}
}
