using System.Security.AccessControl;
using System.Security.Principal;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Only direct operation directories in the service account's temp root can own SSH files.</summary>
internal static class GitSshAuthenticationPath
{
	internal const string Prefix = "vertex-ssh-";
	internal static IReadOnlyList<string> FileNames { get; } = Array.AsReadOnly<string>(["identity", "known_hosts", "empty_config", ".vertex-ssh-owner"]);

	internal static void Demand(string directory)
	{
		var resolved = Path.GetFullPath(directory);
		var root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
		var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		var name = Path.GetFileName(resolved);
		if (!string.Equals(Path.GetDirectoryName(resolved), root, comparison) || !name.StartsWith(Prefix, StringComparison.Ordinal)
			|| !Guid.TryParseExact(name[Prefix.Length..], "N", out _))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		for (var item = new DirectoryInfo(resolved); item is not null; item = item.Parent)
		{
			if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != FileAttributes.None)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
		}
	}

	internal static void DemandPrivateDirectory(string directory)
	{
		Demand(directory);
		if (OperatingSystem.IsWindows())
		{
			var user = WindowsIdentity.GetCurrent().User!;
			var permissions = new DirectoryInfo(directory).GetAccessControl();
			if (!permissions.AreAccessRulesProtected || !user.Equals(permissions.GetOwner(typeof(SecurityIdentifier))))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			foreach (FileSystemAccessRule rule in permissions.GetAccessRules(true, true, typeof(SecurityIdentifier)))
			{
				if (rule.AccessControlType == AccessControlType.Allow && !user.Equals(rule.IdentityReference))
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
				}
			}
		}
		else if ((File.GetUnixFileMode(directory) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
			| UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != UnixFileMode.None)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}
}
