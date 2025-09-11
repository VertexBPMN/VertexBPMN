using System;
using System.Collections.Generic;

namespace VertexBPMN.Domain.Modeling;

public record HistoricalCaseData(
    string CaseId,
    Dictionary<string, object> CaseFile,
    List<string> CompletedPlanItems,
    DateTime Timestamp
);