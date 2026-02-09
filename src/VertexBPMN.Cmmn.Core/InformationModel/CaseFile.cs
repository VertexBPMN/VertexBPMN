using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.InformationModel;

public sealed class CaseFile : CmmnElement
{
    public Collection<CaseFileItem> RootItems { get; } = new();
}