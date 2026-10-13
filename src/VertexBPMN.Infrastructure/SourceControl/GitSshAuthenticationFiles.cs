using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Private operation-local authentication files, outside repository and shared workspaces.</summary>
internal sealed class GitSshAuthenticationFiles : IAsyncDisposable
{
	private readonly string directory;
	private bool ownsDirectory;
	internal string KeyFile => Path.Combine(directory, "identity");
	internal string KnownHostsFile => Path.Combine(directory, "known_hosts");
	internal string EmptyConfigFile => Path.Combine(directory, "empty_config");
	internal string OwnershipFile => Path.Combine(directory, ".vertex-ssh-owner");
	internal string HostAlias { get; }
	private GitSshAuthenticationFiles(Uri remote)
	{
		directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "vertex-ssh-" + Guid.NewGuid().ToString("N")));
		HostAlias = "vertex-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(remote.IdnHost + ":" + remote.Port.ToString(System.Globalization.CultureInfo.InvariantCulture))));
	}
	internal static async Task<GitSshAuthenticationFiles> CreateAsync(ApprovedGitSshTarget approved, GitSshCredentialLease lease,
		GitSshAuthenticationFileRecovery recovery, CancellationToken cancellationToken)
	{
		var files = new GitSshAuthenticationFiles(approved.Target.Remote);
		try
		{
			files.CreateDirectory();
			GitSshAuthenticationPath.DemandPrivateDirectory(files.directory);
			if (Directory.EnumerateFileSystemEntries(files.directory).Any())
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			files.ownsDirectory = true;
			await recovery.WriteOwnershipAsync(files.directory, lease.ExpiresAt, cancellationToken);
			var key = lease.CopyKey(approved.Target.Remote);
			try
			{ await files.WriteAsync(files.KeyFile, key, cancellationToken); }
			finally { Array.Clear(key); }
			await files.WriteAsync(files.KnownHostsFile, Encoding.UTF8.GetBytes(string.Join('\n', approved.Trust.HostKeys.Select(key => files.HostAlias + " " + key)) + "\n"), cancellationToken);
			await files.WriteAsync(files.EmptyConfigFile, [], cancellationToken);
			return files;
		}
		catch { await files.DisposeAsync(); throw; }
	}

	private void CreateDirectory()
	{
		GitSshAuthenticationPath.Demand(directory);
		if (Directory.Exists(directory))
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
		if (OperatingSystem.IsWindows())
		{
			var user = WindowsIdentity.GetCurrent().User!;
			var security = new DirectorySecurity();
			security.SetAccessRuleProtection(true, false);
			security.SetOwner(user);
			security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
				InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
			new DirectoryInfo(directory).Create(security);
		}
		else
		{ Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
	}
	private async Task WriteAsync(string path, byte[] bytes, CancellationToken cancellationToken)
	{
		var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
		if (!OperatingSystem.IsWindows())
		{ options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; }
		await using var file = new FileStream(path, options);
		await file.WriteAsync(bytes, cancellationToken);
	}

	internal string Command(string executable, ApprovedGitSshTarget approved, GitSshCredentialLease lease, TimeSpan timeout)
	{
		if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
		{ throw new SourceControlSecurityException(SourceControlErrorCode.GitUnavailable); }
		var address = approved.CopyAddresses().FirstOrDefault() ?? throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		GitHttpsNetworkBoundary.DemandAddresses([address], approved.Network.AllowedAddressRanges, approved.Network.DeniedAddressRanges);
		string[] arguments = [executable, "-F", EmptyConfigFile, "-T", "-i", KeyFile,
			"-o", "HostName=" + address, "-o", "HostKeyAlias=" + HostAlias, "-o", "User=" + lease.Username,
			"-o", "Port=" + approved.Target.Remote.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
			"-o", "UserKnownHostsFile=" + KnownHostsFile, "-o", "GlobalKnownHostsFile=" + EmptyConfigFile,
			"-o", "StrictHostKeyChecking=yes", "-o", "VerifyHostKeyDNS=no", "-o", "CheckHostIP=no", "-o", "UpdateHostKeys=no",
			"-o", "IdentitiesOnly=yes", "-o", "IdentityAgent=none", "-o", "CertificateFile=none", "-o", lease.HasPassphrase ? "BatchMode=no" : "BatchMode=yes",
			"-o", "NumberOfPasswordPrompts=1",
			"-o", "PasswordAuthentication=no", "-o", "KbdInteractiveAuthentication=no", "-o", "PreferredAuthentications=publickey",
			"-o", "ForwardAgent=no", "-o", "ClearAllForwardings=yes", "-o", "PermitLocalCommand=no", "-o", "ProxyCommand=none",
			"-o", "ProxyJump=none", "-o", "ControlMaster=no", "-o", "ControlPath=none", "-o", "ControlPersist=no",
			"-o", "ConnectionAttempts=1", "-o", "LogLevel=ERROR", "-o", "ConnectTimeout=" + Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture)];
		return string.Join(' ', arguments.Select(Quote));
	}
	private static string Quote(string value)
	{
		value = value.Replace('\\', '/');
		if (value.Any(character => char.IsControl(character) || character is '\'' or '"' or '$' or '`'))
		{ throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput); }
		return "'" + value + "'";
	}
	public ValueTask DisposeAsync()
	{
		if (!ownsDirectory || !Directory.Exists(directory))
		{ return ValueTask.CompletedTask; }
		GitSshAuthenticationPath.DemandPrivateDirectory(directory);
		foreach (var path in new[] { KeyFile, KnownHostsFile, EmptyConfigFile, OwnershipFile })
		{ File.Delete(path); }
		Directory.Delete(directory);
		return ValueTask.CompletedTask;
	}
}
