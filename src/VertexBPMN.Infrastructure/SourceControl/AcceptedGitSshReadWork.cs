namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record AcceptedGitSshReadWork(AcceptedGitSshExecution Execution, AcceptedGitRevisionRead Request);
