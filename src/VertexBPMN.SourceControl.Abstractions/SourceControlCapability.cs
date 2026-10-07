namespace VertexBPMN.SourceControl.Abstractions;

[Flags]
public enum SourceControlCapability
{
    None = 0,
    Read = 1,
    Commit = 2,
    Push = 4,
    PullRequest = 8,
    VerifyRelease = 16,
    DeleteFile = 32,
    RenameFile = 64
}
