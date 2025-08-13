using System.Collections.Generic;
namespace VertexBPMN.Core.Domain
{
    public class ProcessMigrationPlan
    {
        public string SourceProcessDefinitionId { get; set; }
        public string TargetProcessDefinitionId { get; set; }
        public Dictionary<string, string> ActivityMappings { get; set; } = new(); // oldActivityId -> newActivityId
    }
}
