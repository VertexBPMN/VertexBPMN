using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class PlanningTable : CmmnElement
{
    public Collection<TableItem> TableItems { get; } = new();
}