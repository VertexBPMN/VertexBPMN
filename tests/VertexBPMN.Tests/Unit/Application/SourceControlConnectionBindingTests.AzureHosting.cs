using System.Text;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Theory]
	[InlineData("HostingHttpBearer", "Bearer")]
	[InlineData("HostingAzureDevOpsPat", "Basic")]
	public async Task Azure_hosting_authentication_uses_only_its_explicit_API_credential(string type, string scheme)
	{
		await using var fixture = await Fixture.OpenAsync(new Uri("https://dev.azure.com/team/project/_git/repository"));
		var setup = await HostedPushAsync(fixture, AzureHosting());
		var execution = await HostingCredentialExecutionAsync(fixture, setup);
		var credentials = HostingCredentials();
		credentials.Metadata = credentials.Metadata with { Type = type };
		using var lease = await HostingBroker(setup, credentials).IssueAzureAsync(Context, execution, HostingApproval(execution), Token);
		Assert.Equal(scheme, lease.Authorization().Scheme);
		if (scheme == "Basic")
		{ Assert.Equal("VertexBPMN:synthetic-hosting-token", Encoding.UTF8.GetString(Convert.FromBase64String(lease.Authorization().Parameter!))); }
		else
		{ Assert.Equal("synthetic-hosting-token", lease.Authorization().Parameter); }
		Assert.All(credentials.Requests, reference => Assert.StartsWith("api-credential/", reference, StringComparison.Ordinal));
	}

	[Theory]
	[InlineData("revocation")]
	[InlineData("rotation")]
	public async Task Azure_hosting_rejects_revocation_or_mixed_credentials_before_token_issuance(string change)
	{
		await using var fixture = await Fixture.OpenAsync(new Uri("https://dev.azure.com/team/project/_git/repository"));
		var setup = await HostedPushAsync(fixture, AzureHosting());
		var execution = await HostingCredentialExecutionAsync(fixture, setup);
		var credentials = EntraCredentials();
		credentials.Metadata = credentials.Metadata with { Id = "api-credential", Type = "HostingAzureDevOpsEntra" };
		credentials.OnSecret = async key =>
		{
			if (key != "clientSecret")
			{ return; }
			if (change == "rotation")
			{ credentials.Metadata = credentials.Metadata with { LastModified = DateTime.UtcNow }; }
			else
			{ Assert.True(await setup.Store.RevokeConnectionProfileAsync(Context, Admin, setup.Profile.Id, 2, Token)); }
		};
		var issuer = new EntraIssuer();
		var broker = new ProfiledHostingCredentialBroker(setup.Store, credentials, new CredentialActors { Roles = ["Admin"] }, Options.Create(setup.Configuration), issuer);
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => broker.IssueAzureAsync(Context, execution, HostingApproval(execution), Token));
		Assert.Equal(0, issuer.Calls);
	}

	[Fact]
	public async Task Azure_Entra_hosting_uses_the_explicit_application_and_bounded_Bearer_lease()
	{
		await using var fixture = await Fixture.OpenAsync(new Uri("https://dev.azure.com/team/project/_git/repository"));
		var setup = await HostedPushAsync(fixture, AzureHosting());
		var execution = await HostingCredentialExecutionAsync(fixture, setup);
		var credentials = EntraCredentials();
		credentials.Metadata = credentials.Metadata with { Id = "api-credential", Type = "HostingAzureDevOpsEntra" };
		var issuer = new EntraIssuer();
		using var lease = await new ProfiledHostingCredentialBroker(setup.Store, credentials, new CredentialActors { Roles = ["Admin"] }, Options.Create(setup.Configuration), issuer)
			.IssueAzureAsync(Context, execution, HostingApproval(execution), Token);
		Assert.Equal("synthetic-entra-token", lease.Authorization().Parameter);
		Assert.Equal(1, issuer.Calls);
		Assert.True(lease.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(2));
	}

	private static RepositoryHostingConnection AzureHosting() => new(AzureDevOpsHostingTargetPolicy.ProviderId,
		new(new Uri("https://dev.azure.com/team/project/_apis/git/repositories/b38e16b1-e292-45c3-b697-a4a6f37a825f"), "api-network"),
		"team/d3321197-7da9-46ed-a7ec-5d5af8d0d73c/b38e16b1-e292-45c3-b697-a4a6f37a825f", "api-credential", SourceControlCapability.PullRequest);
}
