using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Cmmn.Diagram;

#nullable enable


/// <summary>
/// CMMN DI root (Figure 7.1).
/// Extension: Enhanced with shared/local styles.
/// </summary>
public record CMMNDI(
    List<CMMNStyle> Styles = null!,
    List<CMMNDiagram> Diagrams = null!
);
