using VertexBPMN.Core.Cmmn;

namespace VertexBPMN.Core.Domain;

public interface IAiDecisionService
{
    Task<PlanItem> GenerateAdHocSubprocessAsync(string caseId, Dictionary<string, object> caseFile, CancellationToken cancellationToken = default);
}
