using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed partial class GitHttpsTransportTests
{
	[Theory]
	[InlineData("sha1", GitAuthenticationKind.HttpBasic)]
	[InlineData("sha256", GitAuthenticationKind.HttpBasic)]
	[InlineData("sha1", GitAuthenticationKind.HttpBearer)]
	[InlineData("sha256", GitAuthenticationKind.HttpBearer)]
	public async Task Profile_push_is_confirmed_by_an_independent_clone_and_rejects_a_stale_ref(string objectFormat, GitAuthenticationKind authentication)
	{
		await using var fixture = await HttpsFixture.CreateAsync(objectFormat: objectFormat, username: "service-account", hostname: "git.vertex.invalid", authentication: authentication);
		var (binding, readApproval) = await ApproveFixtureAsync(fixture, "service-account");
		var profile = readApproval.Profile with { Authentication = authentication, GitCapabilities = SourceControlCapability.Read | SourceControlCapability.Commit | SourceControlCapability.Push };
		var approval = new ApprovedGitHttpsTarget(readApproval.Remote, profile, readApproval.CopyAddresses())
		{
			AdministrativePolicy = readApproval.AdministrativePolicy,
			RequiredCapabilities = SourceControlCapability.Push
		};
		using IGitCredentialLease lease = authentication == GitAuthenticationKind.HttpBasic
			? new GitHttpBasicCredentialLease(binding.Remote, "service-account", fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2))
			: new GitHttpBearerCredentialLease(binding.Remote, fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
		var token = TestContext.Current.CancellationToken;
		var context = new SourceControlContext("tenant-test", "actor-test");
		var basis = await FixtureRevisionAsync(fixture);
		await fixture.Runner.FetchApprovedProfileRevisionAsync(fixture.Workspace, context, binding, approval, basis, lease, token);
		var session = Guid.NewGuid();
		var branch = SourceControlInputPolicy.WorkBranch(session);
		var bytes = fixture.ModelBytes.Concat(Encoding.UTF8.GetBytes("\n")).ToArray();
		var snapshot = new ModelSnapshot("models/example.bpmn", SourceModelKind.Bpmn, Guid.NewGuid(), 1, bytes);
		var command = new CommitCommand(fixture.Workspace.OperationId, new("profile-commit"), session, basis, branch, "Confirmed profile snapshot", [snapshot]);
		var commit = await fixture.Runner.BuildCommitAsync(fixture.Workspace, binding, command, DateTimeOffset.UtcNow, token);
		var push = new PushCommand(fixture.Workspace.OperationId, new("profile-push"), branch, commit, ExpectedRemoteRef.Absent);
		await fixture.Runner.PushApprovedProfileAsync(fixture.Workspace, context, binding, approval, push, lease, token);
		Assert.Equal(commit, await fixture.Runner.ReadApprovedProfilePushHeadAsync(fixture.Workspace, context, binding, approval, push, lease, token));
		await VerifyApprovedCloneAsync(fixture, binding, profile, approval, lease, commit, bytes, token);
		var other = await fixture.Runner.BuildCommitAsync(fixture.Workspace, binding, command with { Message = "Conflicting snapshot" }, DateTimeOffset.UtcNow, token);
		var conflict = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Runner.PushApprovedProfileAsync(
			fixture.Workspace, context, binding, approval, push with { Commit = other }, lease, token));
		Assert.Equal(SourceControlErrorCode.RevisionConflict, conflict.Code);
		Assert.Equal(commit, await fixture.Runner.ReadApprovedProfilePushHeadAsync(fixture.Workspace, context, binding, approval, push, lease, token));
		Assert.Empty(Directory.EnumerateFiles(fixture.Workspace.ControlDirectory, "ca-*.pem"));
	}

	[Fact]
	public async Task Read_approval_does_not_authorize_a_native_push_even_with_a_write_capable_profile()
	{
		await using var fixture = await HttpsFixture.CreateAsync(username: "service-account", hostname: "git.vertex.invalid");
		var (binding, approved) = await ApproveFixtureAsync(fixture, "service-account");
		var profile = approved.Profile with { GitCapabilities = SourceControlCapability.Read | SourceControlCapability.Push };
		var readOnly = new ApprovedGitHttpsTarget(approved.Remote, profile, approved.CopyAddresses()) { AdministrativePolicy = approved.AdministrativePolicy };
		using var lease = new GitHttpBasicCredentialLease(binding.Remote, "service-account", fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
		var before = fixture.AuthenticatedRequests;
		var command = new PushCommand(fixture.Workspace.OperationId, new("unauthorized"), SourceControlInputPolicy.WorkBranch(Guid.NewGuid()),
			await FixtureRevisionAsync(fixture), ExpectedRemoteRef.Absent);
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Runner.PushApprovedProfileAsync(
			fixture.Workspace, new("tenant-test", "actor-test"), binding, readOnly, command, lease, TestContext.Current.CancellationToken));
		Assert.Equal(SourceControlErrorCode.Forbidden, error.Code);
		Assert.Equal(before, fixture.AuthenticatedRequests);
	}

	private static async Task VerifyApprovedCloneAsync(HttpsFixture fixture, RepositoryBinding binding, RepositoryConnectionProfile profile,
		ApprovedGitHttpsTarget approval, IGitCredentialLease lease, GitCommitId commit, byte[] expectedBytes, CancellationToken cancellationToken)
	{
		var directory = Path.Combine(fixture.Root, "independent-clone");
		var control = Path.Combine(directory, "control");
		Directory.CreateDirectory(control);
		var workspace = new GitWorkspace(directory, control, Guid.NewGuid(), 1);
		await fixture.Runner.InitializeAsync(workspace, commit, cancellationToken);
		var readApproval = new ApprovedGitHttpsTarget(approval.Remote, profile, approval.CopyAddresses()) { AdministrativePolicy = approval.AdministrativePolicy };
		await fixture.Runner.FetchApprovedProfileRevisionAsync(workspace, new("tenant-test", "actor-test"), binding, readApproval, commit, lease, cancellationToken);
		var actual = await fixture.Runner.ReadModelAsync(workspace, binding, new(commit, "models/example.bpmn"), Guid.NewGuid(), cancellationToken);
		Assert.Equal(expectedBytes, actual.CopyContent());
	}
}
