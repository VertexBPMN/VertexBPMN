using System.Text.Json.Serialization;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record AcceptedGitReadQuery([property: JsonRequired] GitReadQueryKind Kind)
{
	public string? Path { get; init; }
	public int PageSize { get; init; }
	public string? Cursor { get; init; }
	public Guid Generation { get; init; }
	public StoredReadSnapshot? Snapshot { get; init; }
	internal bool IsRemoteQuery => Kind is GitReadQueryKind.Branches or GitReadQueryKind.ResolveBranch;

	internal void Validate(RepositoryBinding binding, GitCommitId? revision, SourceControlLimits limits)
	{
		if (!Enum.IsDefined(Kind) || IsRemoteQuery != (revision is null)
			|| (Kind != GitReadQueryKind.File && Generation != Guid.Empty)
			|| (Kind != GitReadQueryKind.Diff && Snapshot is not null))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		if (Kind is GitReadQueryKind.ListFiles or GitReadQueryKind.History or GitReadQueryKind.Branches)
		{
			GitRemoteReferences.ValidatePage(PageSize, Cursor, limits);
		}
		else if (PageSize != 0 || Cursor is not null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		switch (Kind)
		{
			case GitReadQueryKind.ListFiles:
				SourceControlInputPolicy.ValidateRelativePath(Path!);
				if (!binding.ModelRoots.Any(root => Path == root || Path!.StartsWith(root + "/", StringComparison.Ordinal)))
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
				}
				break;
			case GitReadQueryKind.File:
				SourceControlInputPolicy.DemandModelPath(Path!, binding.ModelRoots);
				if (Generation == Guid.Empty)
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
				}
				break;
			case GitReadQueryKind.History:
				GitModelHistory.ValidateLimit(limits);
				SourceControlInputPolicy.DemandModelPath(Path!, binding.ModelRoots);
				break;
			case GitReadQueryKind.Diff:
				if (Path is not null || Snapshot is null)
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
				}
				SourceControlInputPolicy.DemandSafeBpmn(Snapshot.Restore(), binding, limits);
				break;
			case GitReadQueryKind.ResolveBranch:
				SourceControlInputPolicy.ValidateBranch(Path!);
				break;
			case GitReadQueryKind.Branches when Path is not null:
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
	}
}
