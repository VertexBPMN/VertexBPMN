using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed partial class SourceControlConnectionBindingTests
{
	[Theory]
	[InlineData("configured")]
	[InlineData("missing-client")]
	[InlineData("revoked-trust")]
	public async Task Ssh_repository_availability_exposes_implemented_git_capabilities(string scenario)
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var configuration = SshConfigurationOptions(fixture);
		configuration = new()
		{
			Enabled = true,
			SshExecutablePath = scenario == "missing-client" ? null : Environment.ProcessPath,
			SshNetworkPolicies = configuration.SshNetworkPolicies,
			SshTrustProfiles = [configuration.SshTrustProfiles[0] with { IsRevoked = scenario == "revoked-trust" }]
		};
		var monitor = new Mock<IOptionsMonitor<SourceControlOptions>>();
		monitor.SetupGet(value => value.CurrentValue).Returns(configuration);
		var availability = await fixture.Store.GetRepositoryAvailabilityAsync(Context, fixture.Binding.Id, Admin, monitor.Object, Token);
		Assert.Equal(scenario == "configured", availability.Available);
		Assert.Equal(scenario == "configured" ? SourceControlCapability.Read | SourceControlCapability.Commit | SourceControlCapability.Push : SourceControlCapability.None, availability.Capabilities);
	}

	[Fact]
	public async Task Studio_profiled_read_route_accepts_SSH_and_preserves_shared_job_quota()
	{
		await using var fixture = await Fixture.OpenAsync(new("ssh://git.example:2222/repositories/models.git"));
		var basis = SshConfigurationOptions(fixture);
		var configuration = new SourceControlOptions
		{
			Enabled = true,
			SshNetworkPolicies = basis.SshNetworkPolicies,
			SshTrustProfiles = basis.SshTrustProfiles,
			Limits = new() { MaxConcurrentJobsTotal = 1 }
		};
		var active = fixture.Operation(SourceControlOperationState.Running);
		active.LeaseUntilUtcTicks = DateTimeOffset.UtcNow.AddMinutes(2).UtcTicks;
		fixture.Db.SourceControlOperations.Add(active);
		await fixture.Db.SaveChangesAsync(Token);
		var store = fixture.NewStore(fixture.Db, configuration);
		var actors = new CredentialActors();
		var credentials = SshCredentials();
		var options = Options.Create(configuration);
		var git = new ControlledGitProcess(options);
		var sshPolicies = SshExecutionRegistry(() => configuration);
		var ssh = new ProfiledSshReadService(store, actors, sshPolicies,
			new(Mock.Of<IServiceScopeFactory>(), options), git,
			new(store, actors, sshPolicies, new(store, credentials, actors, options), git, options));
		var httpsPolicies = ExecutionRegistry(() => new() { Enabled = true });
		var reads = new ProfiledGitReadService(store, actors, httpsPolicies, new(Mock.Of<IServiceScopeFactory>(), options), git,
			new(store, actors, httpsPolicies, new(store, new ProfileCredentials(), actors, options), git, options), options, ssh);
		await RejectAsync(SourceControlErrorCode.QuotaExceeded,
			() => reads.ReadAsync<SourceControlPage<string>>(Context, fixture.Binding, null, new(GitReadQueryKind.Branches) { PageSize = 10 }, Token));
		Assert.Equal(4, (await fixture.Db.SourceControlOperations.AsNoTracking().SingleAsync(row => row.Kind == (int)SourceControlOperationKind.Read, Token)).ExecutionContractVersion);
		Assert.Empty(credentials.Requests);
	}
}
