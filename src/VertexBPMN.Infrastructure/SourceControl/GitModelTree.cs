using System.Globalization;
using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal static class GitModelTree
{
	private const int MaximumCursorCharacters = 1024;
	internal static SourceControlPage<RepositoryFile> ReadPage(byte[] tree, RepositoryBinding binding,
		TreeRequest request, SourceControlLimits limits, CancellationToken cancellationToken)
	{
		var files = ReadFiles(tree, binding, limits, cancellationToken);
		var selected = files.Where(file => file.Path.StartsWith(request.Root + "/", StringComparison.Ordinal))
			.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
		var prefix = $"{binding.Id:N}:{request.Commit.Value}:{request.Root}:";
		var start = 0;
		if (request.Cursor is not null)
		{
			if (request.Cursor.Length > MaximumCursorCharacters || !request.Cursor.StartsWith(prefix, StringComparison.Ordinal))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
			}
			var lastPath = request.Cursor[prefix.Length..];
			start = Array.FindIndex(selected, file => file.Path == lastPath) + 1;
			if (start == 0)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
			}
		}
		var page = selected.Skip(start).Take(request.PageSize).ToArray();
		var next = start + page.Length < selected.Length ? prefix + page[^1].Path : null;
		return new(page, next);
	}

	private static List<RepositoryFile> ReadFiles(byte[] tree, RepositoryBinding binding,
		SourceControlLimits limits, CancellationToken cancellationToken)
	{
		var text = DecodeTree(tree);
		var files = new List<RepositoryFile>();
		var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var entry in text.Split('\0', StringSplitOptions.RemoveEmptyEntries))
		{
			cancellationToken.ThrowIfCancellationRequested();
			var tab = entry.IndexOf('\t');
			if (tab < 0)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			var path = entry[(tab + 1)..];
			if (!binding.ModelRoots.Any(root => path.Equals(root, StringComparison.OrdinalIgnoreCase)
				|| path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}
			SourceControlInputPolicy.ValidateRelativePath(path);
			var metadata = entry[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (metadata.Length != 4 || !paths.Add(path)
				|| metadata[0] is not ("040000" or "100644" or "100755"))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			if (metadata[0] == "040000" || !path.EndsWith(".bpmn", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			SourceControlInputPolicy.DemandModelPath(path, binding.ModelRoots);
			if (metadata[1] != "blob" || !long.TryParse(metadata[3], NumberStyles.None, CultureInfo.InvariantCulture, out var size)
				|| size <= 0 || size > limits.MaxModelBytes)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
			}
			files.Add(new(path, SourceModelKind.Bpmn, size));
			if (files.Count > limits.MaxModelFiles)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
			}
		}
		return files;
	}

	private static string DecodeTree(byte[] tree)
	{
		try
		{
			return new UTF8Encoding(false, true).GetString(tree);
		}
		catch (DecoderFallbackException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}
}
