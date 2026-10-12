using System.Diagnostics;
using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;


/// <summary>Real local helper/IPC checks with synthetic credentials; no remote Git or network acceptance.</summary>
public sealed partial class BasicCredentialChannelTests
{
	private static readonly Uri Remote = new("https://git.internal:8443/team/models.git");
	private const string Username = "git-service";
	private const string Password = "synthetic-test-password";

	[Theory]
	[InlineData("git.internal:8443", "team/models.git", "", 0)]
	[InlineData("git.internal:8443", "team/models.git/", "username=git-service\n", 0)]
	[InlineData("git.internal:8443", "team/models.git", "username=attacker\n", 2)]
	[InlineData("git.internal:8443", "team/models.git", "username=x-access-token\n", 2)]
	[InlineData("git.internal:8443", "team/models.git", "password=forged\n", 2)]
	[InlineData("git.internal:8443", "team/models.git/child", "", 2)]
	[InlineData("git.internal", "team/models.git", "", 2)]
	[InlineData("other.internal:8443", "team/models.git", "", 2)]
	public async Task Helper_uses_server_selected_basic_identity_only_for_exact_repository(string host,
		string path, string extraFields, int expectedExit)
	{
		using var lease = Lease();
		await using var channel = new GitCredentialChannel(Remote, lease, TestContext.Current.CancellationToken);
		var result = await InvokeHelperAsync(channel.Name, $"protocol=https\nhost={host}\npath={path}\n{extraFields}\n");
		Assert.Equal(expectedExit, result.ExitCode);
		Assert.Equal(string.Empty, result.Error);
		Assert.Equal(expectedExit == 0 ? $"username={Username}\npassword={Password}\n\n" : "", result.Output);
	}

	[Theory]
	[InlineData("https://git.internal:8443/team/other.git")]
	[InlineData("https://git.internal/team/models.git")]
	[InlineData("https://other.internal:8443/team/models.git")]
	[InlineData("https://git.internal:8443/team/models.git/child")]
	public async Task Lease_cannot_be_reused_for_another_repository_even_by_another_channel(string remote)
	{
		using var lease = Lease();
		await using var channel = new GitCredentialChannel(new Uri(remote), lease, TestContext.Current.CancellationToken);
		var target = new Uri(remote);
		var result = await InvokeHelperAsync(channel.Name,
			$"protocol=https\nhost={target.Authority}\npath={target.AbsolutePath.TrimStart('/')}\n\n");
		Assert.Equal(2, result.ExitCode);
		Assert.Equal(string.Empty, result.Output);
		Assert.Equal(string.Empty, result.Error);
	}

	[Fact]
	public async Task Disposed_lease_cannot_release_a_credential_and_diagnostics_are_redacted()
	{
		using var lease = Lease();
		Assert.DoesNotContain(Password, lease.ToString(), StringComparison.Ordinal);
		Assert.DoesNotContain(Username, lease.ToString(), StringComparison.Ordinal);
		lease.Dispose();
		await using var channel = new GitCredentialChannel(Remote, lease, TestContext.Current.CancellationToken);
		var result = await InvokeHelperAsync(channel.Name, "protocol=https\nhost=git.internal:8443\npath=team/models.git\n\n");
		Assert.Equal(2, result.ExitCode);
		Assert.Equal(string.Empty, result.Output);
		Assert.Equal(string.Empty, result.Error);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" username")]
	[InlineData("username ")]
	[InlineData("user:name")]
	[InlineData("username\npassword=forged")]
	public void Basic_identity_cannot_inject_the_helper_protocol(string username)
	{
		var error = Assert.Throws<SourceControlSecurityException>(() =>
			new GitHttpBasicCredentialLease(Remote, username, Password, DateTimeOffset.UtcNow.AddMinutes(2)));
		Assert.Equal(SourceControlErrorCode.CredentialUnavailable, error.Code);
		Assert.DoesNotContain(Password, error.ToString(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(-60)]
	[InlineData(10)]
	[InlineData(600)]
	public void Lease_expiry_requires_a_bounded_live_operation(int seconds)
	{
		var error = Assert.Throws<SourceControlSecurityException>(() =>
			new GitHttpBasicCredentialLease(Remote, Username, Password, DateTimeOffset.UtcNow.AddSeconds(seconds)));
		Assert.Equal(SourceControlErrorCode.CredentialUnavailable, error.Code);
	}

	[Fact]
	public async Task Cancellation_prevents_any_credential_output()
	{
		using var lease = Lease();
		await using var buffer = new MemoryStream();
		await using var writer = new StreamWriter(buffer, Encoding.UTF8, leaveOpen: true);
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lease.WriteCredentialAsync(Remote, writer, cancelled.Token));
		Assert.Equal(0, buffer.Length);
	}

	private static GitHttpBasicCredentialLease Lease() => new(Remote, Username, Password, DateTimeOffset.UtcNow.AddMinutes(2));

	private static async Task<HelperResult> InvokeHelperAsync(string channelName, string input)
	{
		var cancellation = TestContext.Current.CancellationToken;
		var helper = Path.Combine(AppContext.BaseDirectory, "auth-helper", "VertexBPMN.SourceControl.AuthHelper"
			+ (OperatingSystem.IsWindows() ? ".exe" : ""));
		Assert.True(File.Exists(helper), "The build must package the real authentication helper.");
		var start = new ProcessStartInfo(helper)
		{
			UseShellExecute = false,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		start.Environment.Clear();
		foreach (var name in new[] { "SystemRoot", "WINDIR" })
		{
			if (Environment.GetEnvironmentVariable(name) is { } value)
			{
				start.Environment[name] = value;
			}
		}

		start.ArgumentList.Add(channelName);
		start.ArgumentList.Add("get");
		Assert.DoesNotContain(start.ArgumentList, value => value.Contains(Password, StringComparison.Ordinal));
		Assert.DoesNotContain(start.Environment.Values, value => value?.Contains(Password, StringComparison.Ordinal) == true);
		using var process = Process.Start(start)!;
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
		timeout.CancelAfter(TimeSpan.FromSeconds(15));
		var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
		var error = process.StandardError.ReadToEndAsync(timeout.Token);
		try
		{
			await process.StandardInput.WriteAsync(input.AsMemory(), timeout.Token);
			process.StandardInput.Close();
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
