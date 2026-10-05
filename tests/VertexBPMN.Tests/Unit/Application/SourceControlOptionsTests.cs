using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VertexBPMN.Application;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class SourceControlOptionsTests
{
    [Fact]
    public void Disabled_configuration_needs_no_git_workspace_or_hosts()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<IOptions<SourceControlOptions>>().Value.Enabled);
    }

    [Fact]
    public void Enabled_configuration_is_bound_and_rejected_without_required_values()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["SourceControl:Enabled"] = "true" }).Build());
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SourceControlOptions>>().Value);
    }

    [Theory]
    [InlineData("https://github.com")]
    [InlineData("*.github.com")]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    [InlineData("metadata.local")]
    [InlineData("github.com:443")]
    [InlineData("github.com.")]
    public void Host_allowlist_rejects_noncanonical_targets(string host) =>
        Assert.True(new SourceControlOptionsValidator().Validate(null, Enabled([host])).Failed);

    [Fact]
    public void Valid_configuration_does_not_create_directories_or_probe_git()
    {
        var options = Enabled(["github.com"]);
        Assert.True(new SourceControlOptionsValidator().Validate(null, options).Succeeded);
        // No filesystem probes: existence/version/private ACLs are separately enforced by adapters.
    }

    [Fact]
    public void Invalid_limits_fail_closed()
    {
        var basis = Enabled(["github.com"]);
        Assert.True(new SourceControlOptionsValidator().Validate(null, new SourceControlOptions
        {
            Enabled = true, GitExecutablePath = basis.GitExecutablePath, WorkspaceRoot = basis.WorkspaceRoot,
            AllowedHosts = basis.AllowedHosts, Limits = new() { MaxConcurrentJobsTotal = 1, MaxConcurrentJobsPerTenant = 2 }
        }).Failed);
    }

    private static SourceControlOptions Enabled(string[] hosts) => new()
    {
        Enabled = true, GitExecutablePath = Path.Combine(Path.GetTempPath(), "git"),
        WorkspaceRoot = Path.Combine(Path.GetTempPath(), "vertex-options-contract-only"), AllowedHosts = hosts
    };
}
