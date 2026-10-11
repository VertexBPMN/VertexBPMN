using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Config;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type GenerateAdHocSubprocessRequest retained for exposed HTTP action signatures and controller activation (GenerateAdHocSubprocessRequest).")]
public record GenerateAdHocSubprocessRequest(string CaseId);
