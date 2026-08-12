using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Common;

namespace VertexBPMN.Domain.Model.Cmmn.Core;

public abstract class CmmnElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? Name { get; set; }
    public Collection<Documentation> Documentation { get; } = new();
    public Collection<ExtensionAttributeValue> ExtensionValues { get; } = new();
}
