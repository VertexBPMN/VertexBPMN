namespace VertexBPMN.SourceControl.Abstractions;
public enum SourceControlOperationState
{
    Queued, Running, CommittedLocal, Pushed, Succeeded, Conflict, Failed,
    Cancelled, ResultUnknown, Reconciling
}
