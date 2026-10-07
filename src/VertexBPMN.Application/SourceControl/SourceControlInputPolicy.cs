using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>Lexical/content guards only; filesystem and connected-peer checks belong to the adapter.</summary>
public static partial class SourceControlInputPolicy
{
	public static string ValidateRelativePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path) || path.Length > 240 || path != path.Trim()
			|| path.Any(c => char.IsControl(c) || "\\:%*?\"<>|".Contains(c)) || path.StartsWith('/'))
		{
			Reject();
		}

		foreach (var part in path.Split('/'))
		{
			if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
				|| part.Equals(".git", StringComparison.OrdinalIgnoreCase)
				|| part.Equals(".gitmodules", StringComparison.OrdinalIgnoreCase)
				|| DeviceName().IsMatch(part.Split('.')[0]))
			{
				Reject();
			}
		}
		return path;
	}

	public static void DemandModelPath(string path, IReadOnlyList<string> modelRoots)
	{
		ValidateRelativePath(path);
		if (modelRoots.Count == 0)
		{
			Reject();
		}

		var inside = false;
		foreach (var root in modelRoots)
		{
			ValidateRelativePath(root);
			if (path.StartsWith(root + "/", StringComparison.Ordinal))
			{
				inside = true;
			}
		}
		if (!inside || !path.EndsWith(".bpmn", StringComparison.OrdinalIgnoreCase))
		{
			Reject();
		}
	}

	public static string ValidateBranch(string branch)
	{
		if (string.IsNullOrWhiteSpace(branch) || branch.Length > 240 || branch.StartsWith('-')
			|| branch is "@" or "HEAD" || branch.EndsWith('.') || branch.Contains("..", StringComparison.Ordinal)
			|| branch.Contains("@{", StringComparison.Ordinal)
			|| branch.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || "~^:?*[\\".Contains(c)))
		{
			Reject();
		}

		foreach (var part in branch.Split('/'))
		{
			if (part.Length == 0 || part.StartsWith('.') || part.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
			{
				Reject();
			}
		}

		return branch;
	}

	public static string WorkBranch(Guid sessionId)
	{
		if (sessionId == Guid.Empty)
		{
			Reject();
		}

		return $"vertex/{sessionId:N}";
	}

	public static void DemandWorkBranch(RepositoryBinding binding, EditSession session, string branch)
	{
		ValidateBranch(branch);
		if (session.RepositoryId != binding.Id || session.TenantId != binding.TenantId
			|| branch != session.WorkBranch || branch != WorkBranch(session.Id)
			|| branch == binding.DefaultBranch || branch == binding.ReleaseBranch)
		{
			Reject();
		}
	}

	public static void DemandSafeBpmn(ModelSnapshot snapshot, RepositoryBinding binding, SourceControlLimits limits)
	{
		if (snapshot.Kind != SourceModelKind.Bpmn)
		{
			Reject();
		}

		DemandModelPath(snapshot.Path, binding.ModelRoots);
		if (limits.MaxModelBytes <= 0 || snapshot.ContentLength > limits.MaxModelBytes)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge);
		}

		var bytes = snapshot.CopyContent();
		try
		{
			using var stream = new MemoryStream(bytes, writable: false);
			using var reader = XmlReader.Create(stream, new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Prohibit,
				XmlResolver = null,
				MaxCharactersInDocument = limits.MaxModelBytes,
				MaxCharactersFromEntities = 0
			});
			var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
			if (document.Root?.Name != XName.Get("definitions", "http://www.omg.org/spec/BPMN/20100524/MODEL"))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}

			var xml = document.ToString(SaveOptions.DisableFormatting);
			if (xml.Contains(ModelExportRedaction.Marker, StringComparison.Ordinal)
				|| document.Root.DescendantsAndSelf().Attributes().Any(a => a.Name.LocalName == "redacted" && a.Value == "true")
				|| !string.Equals(xml, ModelExportRedaction.Redact(xml), StringComparison.Ordinal))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
		}
		catch (XmlException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		// Validation never replaces/serializes the snapshot: commit original bytes only.
	}

	private static void Reject() => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);

	[GeneratedRegex("^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex DeviceName();
}
