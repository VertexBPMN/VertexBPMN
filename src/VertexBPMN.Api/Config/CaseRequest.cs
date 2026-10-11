using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Config;

internal record CaseRequest(string CaseId, string CmmnXml);
