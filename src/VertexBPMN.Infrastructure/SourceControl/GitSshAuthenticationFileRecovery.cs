using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Historical ownership proof permits cleanup after lease expiry without decrypting a private key.</summary>
internal sealed partial class GitSshAuthenticationFileRecovery(IDataProtectionProvider protection)
{
	private const int MaximumMarkerBytes = 4096;
	private static readonly TimeSpan RecoveryGrace = TimeSpan.FromMinutes(1);

	internal async Task WriteOwnershipAsync(string directory, DateTimeOffset expiresAt, CancellationToken cancellationToken)
	{
		GitSshAuthenticationPath.DemandPrivateDirectory(directory);
		var now = DateTimeOffset.UtcNow;
		if (expiresAt <= now.AddSeconds(30) || expiresAt > now.AddMinutes(3))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		var marker = Protector(directory).Protect(JsonSerializer.Serialize(new Ownership(1, now.UtcTicks, expiresAt.UtcTicks)));
		var configuration = new FileStreamOptions
		{
			Mode = FileMode.CreateNew,
			Access = FileAccess.Write,
			Share = FileShare.None,
			Options = FileOptions.Asynchronous
		};
		if (!OperatingSystem.IsWindows())
		{
			configuration.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
		}
		await using var file = new FileStream(Path.Combine(directory, ".vertex-ssh-owner"), configuration);
		await file.WriteAsync(Encoding.UTF8.GetBytes(marker), cancellationToken);
		await file.FlushAsync(cancellationToken);
		// FlushAsync has no flushToDisk overload; persist the proof before a private key can be written.
		await Task.Run(() => file.Flush(flushToDisk: true), cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
	}

	internal async Task<bool> PruneDirectoryAsync(string directory, DateTimeOffset now, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			GitSshAuthenticationPath.DemandPrivateDirectory(directory);
			var path = Path.Combine(directory, ".vertex-ssh-owner");
			if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != FileAttributes.None)
			{
				return false;
			}
			var marker = await ReadMarkerAsync(path, cancellationToken);
			var owner = JsonSerializer.Deserialize<Ownership>(Protector(directory).Unprotect(marker));
			if (owner is null || owner.Version != 1 || owner.CreatedUtcTicks <= 0 || owner.ExpiresUtcTicks <= owner.CreatedUtcTicks
				|| owner.ExpiresUtcTicks - owner.CreatedUtcTicks > TimeSpan.FromMinutes(3).Ticks
				|| owner.ExpiresUtcTicks > now.UtcTicks - RecoveryGrace.Ticks)
			{
				return false;
			}
			GitSshAuthenticationPath.DemandPrivateDirectory(directory);
			if (!CanRemoveFiles(directory))
			{
				return false;
			}
			cancellationToken.ThrowIfCancellationRequested();
			foreach (var name in GitSshAuthenticationPath.FileNames)
			{
				File.Delete(Path.Combine(directory, name));
			}
			Directory.Delete(directory);
			return true;
		}
		catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException
			or JsonException or SourceControlSecurityException or FormatException or ArgumentException)
		{
			return false;
		}
	}

	private static bool CanRemoveFiles(string directory) => Directory.EnumerateFileSystemEntries(directory).All(path =>
		GitSshAuthenticationPath.FileNames.Contains(Path.GetFileName(path), StringComparer.Ordinal)
		&& (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == FileAttributes.None);

	private static async Task<string> ReadMarkerAsync(string path, CancellationToken cancellationToken)
	{
		await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
		var bytes = new byte[MaximumMarkerBytes + 1];
		var count = 0;
		while (count < bytes.Length)
		{
			var read = await file.ReadAsync(bytes.AsMemory(count), cancellationToken);
			if (read == 0)
			{
				break;
			}
			count += read;
		}
		if (count > MaximumMarkerBytes)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
		}
		return new UTF8Encoding(false, true).GetString(bytes, 0, count);
	}

	private IDataProtector Protector(string directory) => protection.CreateProtector("VertexBPMN.SourceControl.SshAuthFiles.v1", Path.GetFullPath(directory));
	private sealed record Ownership([property: JsonRequired] int Version, [property: JsonRequired] long CreatedUtcTicks,
		[property: JsonRequired] long ExpiresUtcTicks);
}
