using System;
using System.Collections.Generic;

namespace VertexBPMN.Core.Domain
{
    public class SimulationRequest
    {
        public string ProcessDefinitionId { get; set; }
        public Dictionary<string, object> Variables { get; set; }
        public int? MaxSteps { get; set; }
        public string TenantId { get; set; }
    }
}
