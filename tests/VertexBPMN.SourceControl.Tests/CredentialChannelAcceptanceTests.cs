using System.Diagnostics;
using VertexBPMN.Infrastructure.SourceControl;

namespace VertexBPMN.SourceControl.Tests;

public sealed class CredentialChannelAcceptanceTests
{
    [Theory]
    [InlineData("github.com", "example/models.git", 0)]
    [InlineData("attacker.example", "example/models.git", 2)]
    [InlineData("github.com", "other/models.git", 2)]
    [InlineData("github.com", "example/models.git\npassword=forged", 2)]
    public async Task Native_helper_receives_only_repository_bound_ephemeral_credential(string host, string path, int expectedExit)
    {
        var helper = Path.Combine(AppContext.BaseDirectory, "auth-helper", "VertexBPMN.SourceControl.AuthHelper.dll");
        Assert.True(File.Exists(helper), "Build must package the real helper for local acceptance.");
        using var lease = new GitHubTokenLease("local-test-not-a-real-token", DateTimeOffset.UtcNow.AddMinutes(2));
        await using var channel = new GitCredentialChannel(new Uri("https://github.com/example/models.git"), lease, TestContext.Current.CancellationToken);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(helper);
        start.ArgumentList.Add(channel.Name);
        start.ArgumentList.Add("get");
        Assert.DoesNotContain(start.ArgumentList, x => x.Contains("local-test-not-a-real-token"));
        using var process = Process.Start(start)!;
        await process.StandardInput.WriteAsync($"protocol=https\nhost={host}\npath={path}\n\n");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        Assert.Equal(expectedExit, process.ExitCode);
        Assert.Equal(string.Empty, await error);
        Assert.Equal(expectedExit == 0 ? "username=x-access-token\npassword=local-test-not-a-real-token\n\n" : "", await output);
    }
}
