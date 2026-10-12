namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Fixed read operations; no arbitrary Git commands or revision expressions.</summary>
public enum GitReadQueryKind
{
	ListFiles = 1,
	File = 2,
	History = 3,
	Diff = 4,
	Branches = 5,
	ResolveBranch = 6
}
