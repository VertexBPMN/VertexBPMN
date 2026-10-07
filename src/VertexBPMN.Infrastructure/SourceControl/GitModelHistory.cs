using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Bounded complete history metadata; never silently promotes a shallow graph to complete.</summary>
internal static class GitModelHistory
{
	internal const int MaximumSupportedCommits = 10_000;

	internal static void ValidateLimit(SourceControlLimits limits)
	{
		if (limits.MaxHistoryCommits is <= 0 or > MaximumSupportedCommits)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
	}

	internal static SourceControlPage<CommitSummary> ReadPage(byte[] output, RepositoryBinding binding,
		HistoryRequest request, SourceControlLimits limits, CancellationToken cancellationToken)
	{
		ValidateLimit(limits);
		GitRemoteReferences.ValidatePage(request.PageSize, request.Cursor, limits);
		string text;
		try
		{
			text = new UTF8Encoding(false, true).GetString(output);
		}
		catch (DecoderFallbackException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		var fields = text.Split('\0');
		if (fields[^1].Length != 0 || (fields.Length - 1) % 3 != 0)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		var count = (fields.Length - 1) / 3;
		if (count > limits.MaxHistoryCommits)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
		}
		var commits = new List<CommitSummary>(count);
		var seen = new HashSet<GitCommitId>();
		for (var offset = 0; offset < count * 3; offset += 3)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var subject = fields[offset + 2];
			if (subject.Any(char.IsControl) || !long.TryParse(fields[offset + 1], NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture, out var timestamp))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			if (subject.Length > limits.MaxCommitMessageCharacters)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
			}
			try
			{
				var commit = new GitCommitId(fields[offset]);
				if (!seen.Add(commit))
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
				}
				commits.Add(new(commit, subject, DateTimeOffset.FromUnixTimeSeconds(timestamp)));
			}
			catch (ArgumentException)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
		}
		var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
			binding.Id.ToString("N") + "\n" + binding.TenantId + "\n" + binding.Remote.AbsoluteUri + "\n"
			+ request.Commit.Value + "\n" + request.Path)));
		var prefix = fingerprint + ":";
		var start = 0;
		if (request.Cursor is not null)
		{
			if (!request.Cursor.StartsWith(prefix, StringComparison.Ordinal))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
			}
			var previous = commits.FindIndex(commit => commit.Commit.Value == request.Cursor[prefix.Length..]);
			if (previous < 0)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
			}
			start = previous + 1;
		}
		var page = commits.Skip(start).Take(request.PageSize).ToArray();
		return new(page, start + page.Length < commits.Count ? prefix + page[^1].Commit.Value : null);
	}
}
