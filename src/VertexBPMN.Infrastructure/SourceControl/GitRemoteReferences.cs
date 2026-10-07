using System.Security.Cryptography;
using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Strict remote heads and snapshot-bound pagination, never a local cache of remote names.</summary>
internal static class GitRemoteReferences
{
	private const int MaximumCursorCharacters = 1024;

	internal static GitCommitId? ReadHead(byte[] output, string branch)
	{
		SourceControlInputPolicy.ValidateBranch(branch);
		if (output.Length == 0)
		{
			return null;
		}
		try
		{
			var text = new UTF8Encoding(false, true).GetString(output);
			var parts = text[..^1].Split('\t');
			if (!text.EndsWith('\n') || parts.Length != 2 || parts[1] != "refs/heads/" + branch)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			var head = new GitCommitId(parts[0]);
			if (head.Value.All(character => character == '0'))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			return head;
		}
		catch (ArgumentException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}

	internal static void ValidatePage(int pageSize, string? cursor, SourceControlLimits limits)
	{
		if (pageSize <= 0 || pageSize > limits.MaxPageSize || cursor?.Length > MaximumCursorCharacters)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
	}

	internal static SourceControlPage<string> ReadPage(byte[] output, RepositoryBinding binding,
		int pageSize, string? cursor, SourceControlLimits limits, CancellationToken cancellationToken)
	{
		ValidatePage(pageSize, cursor, limits);
		string text;
		try
		{
			text = new UTF8Encoding(false, true).GetString(output);
		}
		catch (DecoderFallbackException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		if (text.Length != 0 && !text.EndsWith('\n'))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		var heads = new SortedDictionary<string, string>(StringComparer.Ordinal);
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
		{
			cancellationToken.ThrowIfCancellationRequested();
			var parts = line.Split('\t');
			if (parts.Length != 2 || !parts[1].StartsWith("refs/heads/", StringComparison.Ordinal))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			var name = SourceControlInputPolicy.ValidateBranch(parts[1]["refs/heads/".Length..]);
			string oid;
			try
			{
				oid = new GitCommitId(parts[0]).Value;
			}
			catch (ArgumentException)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			if (!names.Add(name))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			heads.Add(name, oid);
		}
		// Changing ANY head invalidates a continuation, including a reset retaining branch names.
		var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
			binding.Id.ToString("N") + "\n" + binding.TenantId + "\n" + binding.Remote.AbsoluteUri + "\n"
			+ string.Join('\n', heads.Select(head => head.Key + "\t" + head.Value)))));
		var prefix = fingerprint + ":";
		var ordered = heads.Keys.ToArray();
		var offset = 0;
		if (cursor is not null)
		{
			if (!cursor.StartsWith(prefix, StringComparison.Ordinal))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
			}
			var previous = Array.IndexOf(ordered, cursor[prefix.Length..]);
			if (previous < 0)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
			}
			offset = previous + 1;
		}
		var page = ordered.Skip(offset).Take(pageSize).ToArray();
		return new(page, offset + page.Length < ordered.Length ? prefix + page[^1] : null);
	}
}
