namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>
/// Git protocol boundary. Each call requires an authorized, server-resolved binding/context.
/// Implementations must also reject context/binding tenant mismatches. No shell command endpoint.
/// </summary>
public interface IModelSourceControlProvider
{
    Task<SourceControlAvailability> GetAvailabilityAsync(CancellationToken cancellationToken);
    Task<SourceControlPage<string>> ListBranchesAsync(SourceControlContext context,
        RepositoryBinding binding, int pageSize, string? cursor, CancellationToken cancellationToken);
    Task<RevisionSelection> ResolveBranchAsync(SourceControlContext context,
        RepositoryBinding binding, string branch, CancellationToken cancellationToken);
    Task<SourceControlPage<RepositoryFile>> ListFilesAsync(SourceControlContext context,
        RepositoryBinding binding, TreeRequest request, CancellationToken cancellationToken);
    Task<ModelSnapshot> ReadFileAsync(SourceControlContext context,
        RepositoryBinding binding, FileReadRequest request, Guid documentGeneration,
        CancellationToken cancellationToken);
    Task<SourceControlPage<CommitSummary>> ReadHistoryAsync(SourceControlContext context,
        RepositoryBinding binding, HistoryRequest request, CancellationToken cancellationToken);
    Task<ModelDiff> CompareAsync(SourceControlContext context,
        RepositoryBinding binding, DiffRequest request, CancellationToken cancellationToken);
    Task<CommitReceipt> CommitAsync(SourceControlContext context,
        RepositoryBinding binding, CommitCommand command, CancellationToken cancellationToken);
    Task<PushReceipt> PushAsync(SourceControlContext context,
        RepositoryBinding binding, PushCommand command, CancellationToken cancellationToken);
}
