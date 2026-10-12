using VertexBPMN.Infrastructure.SourceControl;

namespace VertexBPMN.SourceControl.Tests;

public sealed partial class BasicCredentialChannelTests
{
	[Theory]
	[InlineData("capability[]=authtype\n", 0)]
	[InlineData("", 2)]
	[InlineData("capability[]=state\n", 2)]
	[InlineData("capability[]=authtype\nusername=attacker\n", 2)]
	[InlineData("capability[]=authtype\ncredential=forged\n", 2)]
	public async Task Bearer_helper_requires_negotiation_and_never_falls_back_to_basic(string fields, int expectedExit)
	{
		using var lease = new GitHttpBearerCredentialLease(Remote, Password, DateTimeOffset.UtcNow.AddMinutes(2));
		await using var channel = new GitCredentialChannel(Remote, lease, TestContext.Current.CancellationToken);
		var result = await InvokeHelperAsync(channel.Name, $"{fields}protocol=https\nhost={Remote.Authority}\npath=team/models.git\n\n");
		Assert.Equal(expectedExit, result.ExitCode);
		Assert.Empty(result.Error);
		Assert.Equal(expectedExit == 0 ? $"capability[]=authtype\nauthtype=Bearer\ncredential={Password}\nephemeral=true\n\n" : "", result.Output);
		Assert.DoesNotContain("password=", result.Output, StringComparison.Ordinal);
		Assert.DoesNotContain("username=", result.Output, StringComparison.Ordinal);
		Assert.DoesNotContain(Password, lease.ToString(), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("other.internal:8443", "team/models.git")]
	[InlineData("git.internal:8443", "team/other.git")]
	public async Task Bearer_negotiation_cannot_change_the_accepted_repository(string host, string path)
	{
		using var lease = new GitHttpBearerCredentialLease(Remote, Password, DateTimeOffset.UtcNow.AddMinutes(2));
		await using var channel = new GitCredentialChannel(Remote, lease, TestContext.Current.CancellationToken);
		var result = await InvokeHelperAsync(channel.Name, $"capability[]=authtype\nprotocol=https\nhost={host}\npath={path}\n\n");
		Assert.Equal(2, result.ExitCode);
		Assert.Empty(result.Output);
		Assert.Empty(result.Error);
	}
}
