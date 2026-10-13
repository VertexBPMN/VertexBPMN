using System.Diagnostics;
using VertexBPMN.Infrastructure.SourceControl;

namespace VertexBPMN.SourceControl.Tests;

public sealed class SshPassphraseChannelTests
{
	private const string TestPassphrase = "public-isolated-test-passphrase";
	private const string SyntheticKey = "-----BEGIN OPENSSH PRIVATE KEY-----\nAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n-----END OPENSSH PRIVATE KEY-----\n";

	[Theory]
	[InlineData("key", 0)]
	[InlineData("password", 2)]
	[InlineData("other-key", 2)]
	[InlineData("disposed", 2)]
	public async Task Real_helper_returns_a_passphrase_only_for_the_owned_key_and_live_lease(string scenario, int expectedExit)
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var keyFile = Path.Combine(Path.GetTempPath(), "vertex-ssh-test", "identity");
		using var lease = new GitSshCredentialLease(new("ssh://git.example:2222/repository.git"), "git", SyntheticKey,
			DateTimeOffset.UtcNow.AddMinutes(2), TestPassphrase);
		await using var channel = new GitSshPassphraseChannel(keyFile, lease, cancellationToken);
		if (scenario == "disposed")
		{
			lease.Dispose();
		}
		var path = keyFile.Replace('\\', '/');
		var prompt = scenario switch
		{
			"password" => "git@git.example's password: ",
			"other-key" => "Enter passphrase for key '/other/identity': ",
			_ => "Enter passphrase for key '" + path[..Math.Min(100, path.Length)] + "': "
		};
		var result = await InvokeHelperAsync(channel.Name, prompt, cancellationToken);
		Assert.Equal(expectedExit, result.ExitCode);
		Assert.Equal(expectedExit == 0 ? TestPassphrase + Environment.NewLine : "", result.Output);
		Assert.Empty(result.Error);
	}

	private static async Task<HelperResult> InvokeHelperAsync(string channel, string prompt, CancellationToken cancellationToken)
	{
		var helper = Path.Combine(AppContext.BaseDirectory, "auth-helper", "VertexBPMN.SourceControl.AuthHelper"
			+ (OperatingSystem.IsWindows() ? ".exe" : ""));
		Assert.True(File.Exists(helper), "Build must package the real authentication helper.");
		var start = new ProcessStartInfo(helper)
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		start.Environment.Remove("VERTEX_SOURCE_CONTROL_SSH_PASSPHRASE_PIPE");
		start.Environment["VERTEX_SOURCE_CONTROL_SSH_PASSPHRASE_PIPE"] = channel;
		start.ArgumentList.Add(prompt);
		Assert.DoesNotContain(start.ArgumentList, value => value.Contains(TestPassphrase, StringComparison.Ordinal));
		Assert.DoesNotContain(start.Environment.Values, value => value?.Contains(TestPassphrase, StringComparison.Ordinal) == true);
		using var process = Process.Start(start)!;
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(15));
		var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
		var error = process.StandardError.ReadToEndAsync(timeout.Token);
		try
		{
			await process.WaitForExitAsync(timeout.Token);
			return new(process.ExitCode, await output, await error);
		}
		finally
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
		}
	}

	private sealed record HelperResult(int ExitCode, string Output, string Error);
}
