using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using VertexBPMN.Studio;

namespace VertexBPMN.Tests.Unit.Security;

public sealed class StudioPasskeyRegistrationTests
{
	[Theory]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(false, false)]
	public async Task Disabled_or_test_authentication_does_not_map_the_endpoint(bool configured, bool oidcActive)
	{
		await using var app = await CreateAppAsync(new StudioPasskeyRegistration(configured, oidcActive));
		using var client = app.GetTestClient();
		using var response = await client.GetAsync("/authentication/passkey/register", TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task Anonymous_access_challenges_login_without_starting_registration()
	{
		await using var app = await CreateAppAsync(new StudioPasskeyRegistration(configured: true, oidcActive: true));
		using var client = app.GetTestClient();
		using var response = await client.GetAsync("/authentication/passkey/register", TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.NotNull(response.Headers.Location);
		Assert.False(QueryHelpers.ParseQuery(response.Headers.Location.Query).ContainsKey("kc_action"));
	}

	[Theory]
	[InlineData("//attacker.example/", "/sso")]
	[InlineData("/\\attacker.example/", "/sso")]
	[InlineData("https://attacker.example/", "/sso")]
	[InlineData("/sso\r\nLocation: evil", "/sso")]
	[InlineData("/process-definitions", "/process-definitions")]
	public async Task Authenticated_action_is_fixed_and_return_target_is_local(string returnTarget, string expected)
	{
		await using var app = await CreateAppAsync(new StudioPasskeyRegistration(configured: true, oidcActive: true));
		using var client = app.GetTestClient();
		using var login = await client.GetAsync("/test-login", TestContext.Current.CancellationToken);
		client.DefaultRequestHeaders.Add("Cookie", login.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));
		using var response = await client.GetAsync($"/authentication/passkey/register?returnUrl={Uri.EscapeDataString(returnTarget)}&kc_action=attacker", TestContext.Current.CancellationToken);
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.NotNull(response.Headers.Location);
		var query = QueryHelpers.ParseQuery(response.Headers.Location.Query);
		Assert.Equal(StudioPasskeyRegistration.ActionName, query["kc_action"].ToString());
		Assert.True(query.ContainsKey("code_challenge"));
		Assert.True(query.ContainsKey("nonce"));
		var options = app.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
		Assert.Equal(expected, options.StateDataFormat.Unprotect(query["state"])!.RedirectUri);
	}

	[Fact]
	public void Unrecognized_action_property_is_not_forwarded()
	{
		var properties = new AuthenticationProperties();
		properties.Items[StudioPasskeyRegistration.ActionItemKey] = "attacker";
		var message = new OpenIdConnectMessage();
		StudioPasskeyRegistration.ApplyAction(properties, message);
		Assert.Null(message.GetParameter("kc_action"));
	}

	private static async Task<WebApplication> CreateAppAsync(StudioPasskeyRegistration registration)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddAuthorization();
		builder.Services.AddAuthentication(options =>
		{
			options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
			options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
		}).AddCookie().AddOpenIdConnect(options =>
		{
			options.ClientId = "routing-contract";
			options.ResponseType = "code";
			options.UsePkce = true;
			options.Configuration = new OpenIdConnectConfiguration
			{
				Issuer = "https://identity.example.org",
				AuthorizationEndpoint = "https://identity.example.org/auth",
				TokenEndpoint = "https://identity.example.org/token"
			};
			options.Events.OnRedirectToIdentityProvider = context =>
			{
				StudioPasskeyRegistration.ApplyAction(context.Properties, context.ProtocolMessage);
				return Task.CompletedTask;
			};
		});
		var app = builder.Build();
		app.UseAuthentication();
		app.UseAuthorization();
		registration.MapEndpoints(app);
		app.MapGet("/test-login", async context => await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
			new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "routing-contract")], CookieAuthenticationDefaults.AuthenticationScheme))));
		await app.StartAsync(TestContext.Current.CancellationToken);
		return app;
	}
}
