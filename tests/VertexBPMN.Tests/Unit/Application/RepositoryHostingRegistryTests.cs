using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class RepositoryHostingRegistryTests
{
	[Fact]
	public void Unknown_and_case_changed_provider_ids_have_no_fallback()
	{
		var github = new RecordingAdapter("github");
		var other = new RecordingAdapter("gitlab");
		var registry = new RepositoryHostingRegistry([github, other]);
		Assert.Same(github, registry.Resolve("github"));
		Assert.Same(other, registry.Resolve("gitlab"));
		foreach (var id in new[] { "GitHub", "unknown", "", "../github" })
		{
			Assert.Equal(SourceControlErrorCode.ProviderUnavailable,
				Assert.Throws<SourceControlSecurityException>(() => registry.Resolve(id)).Code);
		}
		Assert.Equal(0, github.Calls);
		Assert.Equal(0, other.Calls);
	}

	[Fact]
	public void Duplicate_provider_registration_fails_at_host_composition()
	{
		Assert.Throws<ArgumentException>(() => new RepositoryHostingRegistry([new RecordingAdapter("github"), new RecordingAdapter("github")]));
	}

	[Fact]
	public async Task Explicit_profile_never_enters_the_legacy_adapter()
	{
		var github = new RecordingAdapter("github");
		var registry = new RepositoryHostingRegistry([github]);
		var binding = CreateBinding() with { ConnectionProfileId = Guid.NewGuid() };
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => registry.OpenLegacyAsync(new("tenant", "actor"), binding,
			["Admin"], RepositoryPermission.PullRequest, TestContext.Current.CancellationToken));
		Assert.Equal(SourceControlErrorCode.ProviderUnavailable, error.Code);
		Assert.Equal(0, github.Calls);
	}

	[Fact]
	public async Task Legacy_selection_preserves_binding_actor_permission_and_cancellation()
	{
		var github = new RecordingAdapter("github");
		var registry = new RepositoryHostingRegistry([github]);
		var binding = CreateBinding();
		var context = new SourceControlContext("tenant", "actor");
		var roles = new[] { "Admin" };
		using var session = await registry.OpenLegacyAsync(context, binding, roles, RepositoryPermission.Read, TestContext.Current.CancellationToken);
		Assert.Same(context, github.Context);
		Assert.Same(binding, github.Binding);
		Assert.Same(roles, github.Roles);
		Assert.Equal(RepositoryPermission.Read, github.Permission);
		Assert.Equal(TestContext.Current.CancellationToken, github.Token);
	}

	private static RepositoryBinding CreateBinding() => new(Guid.NewGuid(), "tenant", new("https://github.com/team/models.git"),
		"reference", "master", "master", ["models"]);

	private sealed class RecordingAdapter(string id) : IRepositoryHostingAdapter
	{
		public string ProviderId => id;
		public int Calls { get; private set; }
		public SourceControlContext? Context { get; private set; }
		public RepositoryBinding? Binding { get; private set; }
		public IReadOnlyCollection<string>? Roles { get; private set; }
		public RepositoryPermission Permission { get; private set; }
		public CancellationToken Token { get; private set; }
		public Task<IRepositoryHostingSession> OpenAsync(SourceControlContext context, RepositoryBinding binding,
			IReadOnlyCollection<string> roles, RepositoryPermission permission, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Calls++;
			Context = context;
			Binding = binding;
			Roles = roles;
			Permission = permission;
			Token = cancellationToken;
			return Task.FromResult<IRepositoryHostingSession>(new UnusedSession());
		}
	}

	private sealed class UnusedSession : IRepositoryHostingSession
	{
		public Task<PullRequestReceipt> CreateAsync(PullRequestCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<PullRequestReceipt> ReconcileAsync(PullRequestCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<PullRequestReceipt> ReadAsync(PullRequestCommand command, long number, CancellationToken cancellationToken) => throw new NotSupportedException();
		public Task<IReadOnlyList<PullRequestReview>> ReadReviewsAsync(long number, CancellationToken cancellationToken) => throw new NotSupportedException();
		public void Dispose() { }
	}
}
