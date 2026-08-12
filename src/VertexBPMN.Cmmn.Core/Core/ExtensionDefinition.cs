using System.Collections.ObjectModel;

namespace VertexBPMN.Domain.Model.Cmmn.Core;

public sealed class ExtensionDefinition : CmmnElement
{
    public Collection<ExtensionAttributeDefinition> Attributes { get; } = new();
}