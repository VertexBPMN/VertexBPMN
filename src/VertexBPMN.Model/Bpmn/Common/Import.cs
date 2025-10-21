using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Represents an import (stub for spec completeness).
/// </summary>
public record Import(string Namespace, string Location, string ImportType) : BaseElement;