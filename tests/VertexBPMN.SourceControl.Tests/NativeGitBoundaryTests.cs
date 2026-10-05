using System.Text;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class NativeGitBoundaryTests
{
    [Fact]
    public async Task Real_git_initializes_bare_repository_with_no_template_or_ambient_helpers()
    {
        var executable = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_GIT");
        Assert.True(executable is not null && File.Exists(executable), "Set VERTEXBPMN_TEST_GIT to the installed Git executable for local acceptance.");
        var directory = Path.Combine(Path.GetTempPath(), "vertex-git-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var control = Path.Combine(directory, "control");
        Directory.CreateDirectory(Path.Combine(control, "hooks"));
        try
        {
            var runner = new ControlledGitProcess(Options.Create(new SourceControlOptions { Enabled = true, GitExecutablePath = executable }));
            var workspace = new GitWorkspace(directory, control, Guid.NewGuid(), 1);
            Assert.StartsWith("git version ", Encoding.UTF8.GetString(await runner.VersionAsync(workspace, TestContext.Current.CancellationToken)));
            await runner.InitializeAsync(workspace, TestContext.Current.CancellationToken);
            Assert.Equal(SourceControlErrorCode.RevisionConflict, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
                runner.InitializeAsync(workspace, TestContext.Current.CancellationToken))).Code);
            var git = Path.Combine(directory, "repository.git");
            Assert.True(File.Exists(Path.Combine(git, "HEAD")));
            Assert.Contains("bare = true", await File.ReadAllTextAsync(Path.Combine(git, "config"), TestContext.Current.CancellationToken));
            Assert.False(Directory.Exists(Path.Combine(git, "hooks")));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.VersionAsync(workspace, cancelled.Token));
            Assert.DoesNotContain("credential", await File.ReadAllTextAsync(Path.Combine(git, "config"), TestContext.Current.CancellationToken));
            using var lease = new GitHubTokenLease("not-real", DateTimeOffset.UtcNow.AddMinutes(2));
            Assert.Equal(SourceControlErrorCode.InvalidInput, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
                runner.FetchAsync(workspace, new Uri("http://127.0.0.1/repo"), "master", lease, TestContext.Current.CancellationToken))).Code);
        }
        finally
        {
            // Exact directory created by this test; validate link-free tree before removing.
            _ = SourceControlWorkspace.MeasureBytes(directory, long.MaxValue);
            Directory.Delete(directory, recursive: true);
        }
    }
}
