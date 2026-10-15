namespace VertexBPMN.Studio.Services;

// Studio wire contract stays additive and independent of persistence entities.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public Blazor component parameter wire contract.")]
public sealed class StudioUserTask : VertexBPMN.Domain.Entities.UserTask
{
    // Snapshot fields (FormVersion/FormSchemaHash/FormRevisionId) are inherited
    // from the additive domain task contract; keep a Studio-specific wire type.
}
