using System.Diagnostics.CodeAnalysis;

// Public client contract types are consumed by the Blazor razor UI in this assembly and by the
// VertexBPMN.Tests test project, so they are intentionally public (CA1515) and small DTO/record
// groups share a file adjacent to the other released-runtime contracts (MA0048).
[assembly: SuppressMessage("Design", "CA1515", Justification = "Public client contract types consumed by the razor UI and the test project.")]
[assembly: SuppressMessage("Design", "MA0048", Justification = "Small client contract records grouped per file adjacent to the released-runtime contracts.")]
