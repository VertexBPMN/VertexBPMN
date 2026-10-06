namespace VertexBPMN.SourceControl.Abstractions;

[Flags]
public enum RepositoryPermission
{
    None = 0,
    Read = 1,
    Commit = 2,
    Push = 4,
    PullRequest = 8,
    Deploy = 16,
    Manage = 32
}
