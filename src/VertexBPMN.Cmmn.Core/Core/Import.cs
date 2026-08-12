using VertexBPMN.Domain.Model.Cmmn.Common;

namespace VertexBPMN.Domain.Model.Cmmn.Core;

public sealed record Import(string? ImportType, UriString? Location, string? Namespace);