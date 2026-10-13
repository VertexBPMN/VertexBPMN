using System.Net;
using Microsoft.Extensions.Options;
using Moq;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Fact]
	public async Task Profile_transport_prepares_one_dns_snapshot_without_resolving_credentials()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await fixture.Store.CaptureConnectionTargetAsync(Context, fixture.Binding.Id, Admin, SourceControlOperationKind.Read, Token);
		var actors = new CredentialActors();
		var credentials = new ProfileCredentials();
		var options = HttpsPreparationOptions(fixture);
		var transport = PreparationTransport(fixture, actors, credentials, () => options);
		var calls = 0;
		var approved = await transport.PrepareAsync(Context, target, (host, token) =>
		{
			calls++;
			Assert.Equal(target.Remote.IdnHost, host);
			Assert.Equal(Token, token);
			return Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") });
		}, Token);
		Assert.Equal(1, calls);
		Assert.Equal(2, actors.Calls);
		Assert.Equal(target.Profile, approved.Profile);
		Assert.Equal("repository-network", approved.AdministrativePolicy!.Network.Id);
		Assert.Empty(credentials.Requests);
		Assert.Equal(0, credentials.MetadataCalls);
	}

	[Theory]
	[InlineData("network")]
	[InlineData("revoked")]
	[InlineData("role")]
	[InlineData("profile")]
	[InlineData("cancel")]
	public async Task Changes_during_dns_cannot_return_a_stale_profile_approval(string change)
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await fixture.Store.CaptureConnectionTargetAsync(Context, fixture.Binding.Id, Admin, SourceControlOperationKind.Read, Token);
		var actors = new CredentialActors();
		var credentials = new ProfileCredentials();
		var options = HttpsPreparationOptions(fixture);
		var transport = PreparationTransport(fixture, actors, credentials, () => options);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
		async Task<IPAddress[]> ResolveAsync(string host, CancellationToken token)
		{
			Assert.Equal(target.Remote.IdnHost, host);
			switch (change)
			{
				case "network":
					options = HttpsPreparationOptions(fixture, options.HttpsNetworkPolicies[0] with { AllowedAddressRanges = ["10.0.0.0/8"] });
					break;
				case "revoked":
					options = HttpsPreparationOptions(fixture, options.HttpsNetworkPolicies[0] with { IsRevoked = true });
					break;
				case "role":
					actors.Roles = [];
					break;
				case "profile":
					Assert.True(await fixture.Store.RevokeConnectionProfileAsync(Context, Admin, fixture.Profile.Id, 1, token));
					break;
				case "cancel":
					await cancellation.CancelAsync();
					break;
			}
			return [IPAddress.Parse("8.8.8.8")];
		}
		if (change == "cancel")
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.PrepareAsync(Context, target, ResolveAsync, cancellation.Token));
		}
		else
		{
			await Assert.ThrowsAsync<SourceControlSecurityException>(() => transport.PrepareAsync(Context, target, ResolveAsync, cancellation.Token));
		}
		Assert.Empty(credentials.Requests);
		Assert.Equal(0, credentials.MetadataCalls);
	}

	[Fact]
	public async Task Missing_administrative_network_reference_is_rejected_before_dns_or_credentials()
	{
		await using var fixture = await Fixture.OpenAsync();
		Assert.True(await fixture.AssignAsync());
		var target = await fixture.Store.CaptureConnectionTargetAsync(Context, fixture.Binding.Id, Admin, SourceControlOperationKind.Read, Token);
		var credentials = new ProfileCredentials();
		var options = new SourceControlOptions { Enabled = true };
		var transport = PreparationTransport(fixture, new(), credentials, () => options);
		var calls = 0;
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => transport.PrepareAsync(Context, target, (_, _) =>
		{
			calls++;
			return Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") });
		}, Token));
		Assert.Equal(0, calls);
		Assert.Equal(0, credentials.MetadataCalls);
	}

	private static SourceControlOptions HttpsPreparationOptions(Fixture fixture, GitHttpsNetworkPolicy? policy = null) => new()
	{
		Enabled = true,
		HttpsNetworkPolicies = [policy ?? new("repository-network", Context.TenantId, fixture.Binding.Remote, [], [])]
	};

	private static ProfiledGitHttpsTransport PreparationTransport(Fixture fixture, CredentialActors actors, ProfileCredentials credentials,
		Func<SourceControlOptions> configuration)
	{
		var monitor = new Mock<IOptionsMonitor<SourceControlOptions>>();
		monitor.SetupGet(value => value.CurrentValue).Returns(configuration);
		var options = Options.Create(configuration());
		return new(fixture.Store, actors, new(monitor.Object), new(fixture.Store, credentials, actors, options), new(options), options);
	}
}
