using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Config;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type CaseFileUpdateRequest retained for exposed HTTP action signatures and controller activation (CaseFileUpdateRequest).")]
public record CaseFileUpdateRequest(string CaseId, string CaseFileItemId, object NewValue);
